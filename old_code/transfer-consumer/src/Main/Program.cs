using Main.Logging;
using Main.Utility;
using Serilog;

namespace Main;

public class Program
{
   static async Task<int> Main(string[] args)
   {
      var configuration = NoPremiumRoutines.GetAppSettingsConfig(args);
      var logger = new LoggerConfiguration()
         .ReadFrom.Configuration(configuration)
         .CreateLogger();
      IMyLogger myLogger = new MyLogger(logger);
      var linksConfigProvider = new LinksConfigProvider();

      var noPremiumConfig = await linksConfigProvider.GetLinksConfig(myLogger);

      try
      {
         var config = NoPremiumRoutines.GetConsumerConfig(configuration, noPremiumConfig);

         var client = new NoPremiumClient(myLogger, new HtmlDataExtractor(myLogger), new RestHelper());
         await client.Login(config.Credentials);

         await NoPremiumRoutines.ConsumeTransfer(config, myLogger, client);
      }
      catch (Exception e)
      {
         logger.Error(e.ToString());
         return 1;
      }

      logger.Information("All done");
      return 0;
   }
}
