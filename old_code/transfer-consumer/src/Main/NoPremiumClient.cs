using CSharpFunctionalExtensions;
using Main.Logging;
using Main.Utility;

namespace Main;

public class NoPremiumClient : INopremiumClient
{

   private const string SuccessfullLoggedInRecognitionSubstring = "Zalogowany jako";
   private const string UnsuccessfulLoginStrangeLoginLocation = "Logujesz się z innego miejsca niż zazwyczaj. Potwierdź logowanie klikając w link przesłany na adres e-mail";
   private readonly IMyLogger _logger;
   private readonly IHtmlDataExtractor _htmlDataExtractor;
   private readonly IRestHelper _restHelper;

   public NoPremiumClient(IMyLogger logger,
      IHtmlDataExtractor htmlDataExtractor, IRestHelper restHelper)
   {

      _logger = logger;
      _htmlDataExtractor = htmlDataExtractor;
      _restHelper = restHelper;
   }


   public async Task Login(Credentials credentials)
   {
      _logger.LogInformation("Logging to no premium ..");

      var body = await _restHelper.LoginAsync(credentials);

      if (!body!.Contains(SuccessfullLoggedInRecognitionSubstring))
      {
         // try to dermine what is the reason
         if (body.Contains(UnsuccessfulLoginStrangeLoginLocation))
         {
            throw new ApplicationException($"Could not login to nopremium, found text '{UnsuccessfulLoginStrangeLoginLocation}' in response body");
         }
         Console.WriteLine(body);
         throw new ApplicationException($"Could not login to nopremium, could not find text '{SuccessfullLoggedInRecognitionSubstring}' in response body");
      }

      _logger.LogInformation("Login successful");
   }

   public async Task<List<QueuedItemInfo>> GetQueuedFilesAsync()
   {
      var body = await _restHelper.GetQueuedFilesAsync("loadfiles=1");

      return _htmlDataExtractor.GetQueuedLinks(body);
   }

   public async Task RemoveFilesFromQueueAsync(List<QueuedItemInfo> filesToRemoveFromQueue)
   {
      /*
       * # build post body
    # eg remove=1&type=2&ids[]=20255425&ids[]=20255424&
    # 2019-10-22 update - for one file : remove=1&type=2&ids[]=on&ids[]=30778814&
    #                     for two files  remove=1&type=2&ids[]=on&ids[]=30778866&ids[]=30778865&
    # ids = "ids[]=20255425&ids[]=20255424"
       */

      // var ids = string.Join("&", filesToRemoveFromQueue.Select(x => $"ids[]={x.HashId}"));
      // var body = $"remove=1&type=2&ids[]=on&{ids}&";
      //
      // _logger.LogInformation($"Removing files from queue .. (body: '{body}')");
      // await _restHelper.RemoveFilesFromQueueAsync(body);


      // not sure why when sending remove request with many ids but only first item is removed
      // Postman does it corectly but my code does not ? WTF

      // so for simple workaround I'm sending multiple requests with one file each
      foreach (QueuedItemInfo queuedItemInfo in filesToRemoveFromQueue)
      {
         //var ids = string.Join("&", filesToRemoveFromQueue.Select(x => $"ids[]={x.HashId}"));
         //var body = "remove=1&type=2ids[]=on&" + ids;
         var body = $"remove=1&type=2&ids[]=on&ids[]={queuedItemInfo.HashId}&";

         _logger.LogInformation($"Removing file from queue .. (id: '{queuedItemInfo.HashId}')");
         await _restHelper.RemoveFilesFromQueueAsync(body);

      }

   }

   public async Task<TransferInfo> GetTransferInfoAsync()
   {
      var body =  await _restHelper.GetFromFilesUrlAsync();
      var sizeLeftAsString = _htmlDataExtractor.GetBaseTransferString(body);
      var bransferInBytes = DataSizeConverter.FromHumanReadableToBytes(sizeLeftAsString);
      return new TransferInfo
      {
         RemainingTransferBytes = bransferInBytes,
         RemainingTransferHumanReadable = sizeLeftAsString
      };
   }

   public async Task<Maybe<FilesToDownloadInfo>> GetDownloadDataForFileUrl(string fileUrl)
   {
    // build body:
    // e.g. body = "watchonline=&session=275447&links=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3DlGCnrJ45GRY"
    // WARNING, dont know what the session=<number> means but I need to send it
      var body = $"watchonline=&session=275447&links={fileUrl}";
      var responseBody = await _restHelper.SuggestFilesToDownload(body);

      var html = HtmlPretty.Prettyfy(responseBody);
      var downloadData = _htmlDataExtractor.GetFilesToDownloadData(html);

      if (downloadData.HasNoValue)
      {
         _logger.LogError($"Could not get file hashes from response body '{html}', file Url '{fileUrl}', body: '{body}'");
      }

      return downloadData;
   }

//    public async Task DownloadFileFromLink(string fileUrl)
//    {
//       // build body:
// // e.g. body = "watchonline=&session=275447&links=https%3A%2F%2Fwww.youtube.com%2Fwatch%3Fv%3DlGCnrJ45GRY"
// // WARNING, dont know what the session=<number> means but I need to send it
//       var body = $"watchonline=&session=275447&links={fileUrl}";
//       string responseBody = await _restHelper.SuggestFilesToDownload(body);
//       var fileHashes = _htmlDataExtractor.GetFileHashes(responseBody);
//       if (fileHashes.Count == 0)
//       {
//          _logger.LogError($"Could not get file hashes from response body '{responseBody}', file Url '{fileUrl}', body: '{body}'");
//          return;
//       }
//
//       var downloadData = _htmlDataExtractor.GetFilesToDownloadData(responseBody);
//
//       if (downloadData.HasNoValue)
//       {
//          _logger.LogError($"Could not get file hashes from response body '{responseBody}', file Url '{fileUrl}', body: '{body}'");
//          return;
//       }
//
//       await ConsumeTransferForFileHashes(downloadData.Value.Hashes);
//    }

   public async Task ConsumeTransferForFileHashes(List<string> fileHashes)
   {
      var hashesBody = string.Join("&",fileHashes.Select(h => $"hash[]={h}"));
      var requestBody = $"insert=1&mode=1&{hashesBody}";
      var response = await _restHelper.DownloadFiles(requestBody);
   }

   private async Task Logout()
   {
      try
      {
         _logger.LogInformation("Logging out ..");
         await _restHelper.LogoutAsync();
         _logger.LogInformation("Logout successful");
      }
      catch(Exception e)
      {
         _logger.LogWarning("Logout failed", e);
      }

   }

   public async ValueTask DisposeAsync()
   {
      await Logout();
   }
}

public interface INopremiumClient: IAsyncDisposable
{
   Task<List<QueuedItemInfo>> GetQueuedFilesAsync();
   Task RemoveFilesFromQueueAsync(List<QueuedItemInfo> filesToRemoveFromQueue);

   Task<TransferInfo> GetTransferInfoAsync();
   Task Login(Credentials credentials);
   Task ConsumeTransferForFileHashes(List<string> fileHashes);
   Task<Maybe<FilesToDownloadInfo>> GetDownloadDataForFileUrl(string fileUrl);
}

public record TransferInfo
{
   public required long RemainingTransferBytes { get; init; }
   public required string RemainingTransferHumanReadable { get; init; }
}
