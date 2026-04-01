using Flurl.Http;
using Main.Utility;

namespace Main;

public static class RestHelperExtensions
{
   public static IFlurlRequest AddStandardHeaders(this IFlurlRequest request)
   {
      request = request
         .WithHeader("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8")
         // .WithHeader("Accept-Language", "en-US,en;q=0.8")
         .WithHeader("Content-Type", "application/x-www-form-urlencoded")
         .WithHeader("Connection", "keep-alive")
         .WithHeader("Host", "www.nopremium.pl")
         // .WithHeader("Upgrade-Insecure-Requests", "1")
         //.WithHeader("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/54.0.2840.59 Safari/537.36 OPR/41.0.2353.46")

         ;

      return request;
   }
}

public interface IRestHelper
{
   Task<string> GetQueuedFilesAsync(string body);
   Task LogoutAsync();
   Task<string> GetFromFilesUrlAsync();
   Task<string> RemoveFilesFromQueueAsync(string body);
   Task<string> SuggestFilesToDownload(string body);
   Task<string> DownloadFiles(string body);
   Task<string> LoginAsync(Credentials credentials);
}

public class RestHelper : IRestHelper
{
   private const string BaseUrl = "https://www.nopremium.pl";
   private const string LoginUrl = $"{BaseUrl}/login";
   private const string FilesUrl = $"{BaseUrl}/files";
   private const string LogoutUrl = $"{BaseUrl}/logout";

   private CookieJar? _flurlCookieJar = null;

   public async Task<string> LoginAsync(Credentials credentials)
   {
      var response = await LoginUrl
         .WithCookies(out var cookieJar)
         .WithHeader("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9")
         .WithHeader("Content-Type", "application/x-www-form-urlencoded")
         .WithAutoRedirect(true)
         .PostUrlEncodedAsync(new {login = credentials.UserName, password = credentials.UserPassword, remember = "off"});
      _flurlCookieJar = cookieJar;

      var body = await response.GetStringAsync();
      ThrowIfResponseIsNotSuccessful(response);

      return body;
   }

   private static void ThrowIfResponseIsNotSuccessful(IFlurlResponse response)
   {
      if (response.StatusCode < 200 || response.StatusCode > 299)
      {
         throw new ApplicationException($"Invalid response: {response.ResponseMessage.ReasonPhrase}, (status code:{response.StatusCode})");
      }
   }

   public async Task<string> GetQueuedFilesAsync(string body)
   {
      var request = FilesUrl
         .WithCookies(_flurlCookieJar);

      return await PostToFileUrlAsync(request, body);
   }

   public async Task LogoutAsync()
   {
      await LogoutUrl.WithCookies(_flurlCookieJar).GetAsync();
   }

   public async Task<string> GetFromFilesUrlAsync()
   {
      var response = await FilesUrl
         .WithCookies(_flurlCookieJar)
         .GetAsync();
      ThrowIfResponseIsNotSuccessful(response);
      return await response.GetStringAsync();
   }

   public async Task<string> RemoveFilesFromQueueAsync(string body)
   {
      var request = FilesUrl
         .WithCookies(_flurlCookieJar);

      //IFlurlRequest request1 = request;
     // request1 = request1.AddStandardHeaders();
     //request1 = request1.WithHeader("Connection", "keep-alive");
     //request1 = request1.WithHeader("Accept", "*/*");
     //request1 = request1.WithHeader("Cache-Control", "no-cache");
      var response = await request
         .PostUrlEncodedAsync(body);

      ThrowIfResponseIsNotSuccessful(response);
      return await response.GetStringAsync();
   }


   public async Task<string> SuggestFilesToDownload(string body)
   {
      var request = FilesUrl
         .WithCookies(_flurlCookieJar);

      return await PostToFileUrlAsync(request, body);
   }


   public async Task<string> DownloadFiles(string body)
   {
      var request = FilesUrl
         .WithCookies(_flurlCookieJar);

      IFlurlRequest request1 = request;
      var response = await request1
         .PostUrlEncodedAsync(body);

      ThrowIfResponseIsNotSuccessful(response);
      var stringAsync = response.GetStringAsync();
      return await stringAsync;
   }

   private async Task<string> PostToFileUrlAsync(IFlurlRequest request, string body)
   {
      request = request.AddStandardHeaders();
      var response = await request
         .PostUrlEncodedAsync(body);

      ThrowIfResponseIsNotSuccessful(response);
      var stringAsync = response.GetStringAsync();
      return await stringAsync;
   }
}
