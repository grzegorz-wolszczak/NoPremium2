using Main.Exceptions;
using Main.Logging;
using Main.Utility;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Main;

public static class NoPremiumRoutines
{
   public static ConsumerConfig GetConsumerConfig(IConfiguration configuration, NopremiumConfig noPremiumConfig)
   {
      var userPassword = configuration[AppConstants.UserPasswordVarName];
      if (string.IsNullOrEmpty(userPassword))
      {
         throw new ConfigurationException($"env var '{AppConstants.UserPasswordVarName}' was not set or is empty");
      }

      var config = new ConsumerConfig
      {
         Credentials =
            new Credentials() {UserName = "regular.p", UserPassword = userPassword, BytesToPreserveLimit = AppConstants.ThreeGbInBytes,},
         NoPremiumConfig = noPremiumConfig
      };
      return config;
   }

   public static IConfiguration GetAppSettingsConfig(string[] args)
   {
      var configuration = new ConfigurationBuilder()
         .AddEnvironmentVariables()
         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
         .AddCommandLine(args)
         .AddUserSecrets(Assembly.GetExecutingAssembly(), true)
         .Build();
      return configuration;
   }

   public static async Task ConsumeTransfer(ConsumerConfig config, IMyLogger logger, NoPremiumClient client)
   {
      await RemoveExistingFilesFromQueue(config, client, logger);

      var transfer = await client.GetTransferInfoAsync();
      var transferToPreserve = config.NoPremiumConfig.PreserveTransferBytes;
      logger.LogInformation($"Remaining transfer   : {transfer.RemainingTransferBytes} B ({transfer.RemainingTransferHumanReadable})");
      logger.LogInformation($"Transfer to preserve : {transferToPreserve} B ({DataSizeConverter.ToDataSize(transferToPreserve)})");
      if (transfer.RemainingTransferBytes <= config.NoPremiumConfig.PreserveTransferBytes)
      {
         logger.LogInformation("Not consuming more transfer, already below value to preserve");
         return;
      }

      await ExecuteTransferConsumption(config, logger, client);
   }

   private static async Task ExecuteTransferConsumption(ConsumerConfig config, IMyLogger logger, INopremiumClient client)
   {
      var transferToPreserve = config.NoPremiumConfig.PreserveTransferBytes;

      foreach (var contentLinkConfig in config.NoPremiumConfig.Links)
      {
         var transferLeft = await client.GetTransferInfoAsync();
         logger.LogInformation($"Processing file {contentLinkConfig.Name}, link : {contentLinkConfig.Url}");
         logger.LogInformation($"Debug: Transfer left: {transferLeft.RemainingTransferBytes} B ({transferLeft.RemainingTransferHumanReadable})");


         if (transferLeft.RemainingTransferBytes <= transferToPreserve)
         {
            logger.LogInformation(
               $"Stop consuming transfer, already below ({transferLeft.RemainingTransferBytes} B) value to preserve ({transferToPreserve} B)");
            break;
         }

         var url = contentLinkConfig.Url;
         var downloadData = await client.GetDownloadDataForFileUrl(url);
         if (downloadData.HasNoValue)
         {
            logger.LogWarning($"Could not use {url}, skipping (None download data)");
            continue;
         }

         var fileToUseSizeBytes = downloadData.Value.SizeBytes;

         var transferLeftAfterConsumptionBytes = transferLeft.RemainingTransferBytes - fileToUseSizeBytes;
         if (transferLeftAfterConsumptionBytes < transferToPreserve)
         {
            logger.LogWarning(
               $"Not consuming transfer for file '{contentLinkConfig.Name}', {DataSizeConverter.ToDataSize(fileToUseSizeBytes)}, it leave only {transferLeftAfterConsumptionBytes} B {DataSizeConverter.ToDataSize(transferLeftAfterConsumptionBytes)} transfer left");
            continue;
         }


         logger.LogInformation(
            $"Consuming file from link '{contentLinkConfig.Name}' .. (url: {url}), size {fileToUseSizeBytes} B ({DataSizeConverter.ToDataSize(fileToUseSizeBytes)})");
         await client.ConsumeTransferForFileHashes(downloadData.Value.Hashes);
      }
   }

   private static bool ShouldConsumeTransfer(ConsumerConfig config, TransferInfo transferInfo, long fileToUseSizeBytes)
   {
      var transferToPreserve = config.NoPremiumConfig.PreserveTransferBytes;
      return transferInfo.RemainingTransferBytes > transferToPreserve ||
             transferInfo.RemainingTransferBytes - fileToUseSizeBytes > transferToPreserve;
      ;
   }

   private static async Task RemoveExistingFilesFromQueue(ConsumerConfig config, INopremiumClient client, IMyLogger logger)
   {
      // find which files from are already in queue
      var filesAlreadyInQueue = await client.GetQueuedFilesAsync();

      var filesToRemoveFromQueue = new List<QueuedItemInfo>();
      var linksFromConfig = config.NoPremiumConfig.Links
         .Where(LinkUtils.IsValidLink)
         .Select(x => new WhitespaceInsensitiveString(x.Name)).ToList();
      foreach (var fileQueued in filesAlreadyInQueue)
      {
         if (linksFromConfig.Contains(fileQueued.Name))
         {
            filesToRemoveFromQueue.Add(fileQueued);
         }
      }

      if (filesToRemoveFromQueue.Count > 0)
      {
         logger.LogInformation($"Found {filesToRemoveFromQueue.Count} files to remove from queue");
         await client.RemoveFilesFromQueueAsync(filesToRemoveFromQueue);
      }
      else
      {
         logger.LogInformation("Debug: No known files are already queued");
      }
   }
}
