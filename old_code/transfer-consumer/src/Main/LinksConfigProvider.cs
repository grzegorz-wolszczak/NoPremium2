using Main.Logging;
using System.Text.Json;

namespace Main;

public class LinksConfigProvider
{
   public async Task<NopremiumConfig> GetLinksConfig(IMyLogger myLogger)
   {
      var config = new NopremiumConfig()
      {
         PreserveTransferBytes = AppConstants.ThreeGbInBytes,
         Links = []
      };

      var links1 = await GetLinksFromJson(myLogger);
      config.Links.AddRange(LinksToDownload.Links.Valid());
      config.Links.AddRange(links1.Links.Valid());
      return config;
   }



   private static async Task<LinksConfig> GetLinksFromJson(IMyLogger myLogger)
   {
      var result = new LinksConfig() {Links = new ()};

      try
      {
         var fileName = "nopremium.config.json";
         myLogger.LogInformation($"Getting config from file {fileName} ..");
         var content = await File.ReadAllTextAsync(fileName);
         JsonSerializerOptions? options = new JsonSerializerOptions() {AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip};
         LinksConfig deseriazlied =  JsonSerializer.Deserialize<LinksConfig>(content, options)!;
         result.Links.AddRange(deseriazlied.Links);
      }
      catch (Exception e)
      {
         myLogger.LogError(e.ToString());
      }


      return result;
   }
}
