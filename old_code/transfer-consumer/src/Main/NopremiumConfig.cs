using System.Text.Json.Serialization;

namespace Main;


public class NopremiumConfig
{
   public List<ContentLinkConfig> Links { get; set; } = [];
   public required long PreserveTransferBytes { get; set; }
}

public class LinksConfig
{
   [JsonPropertyName("Links")]
   public required List<ContentLinkConfig> Links { get; set; } = [];

}

public class ContentLinkConfig
{
   [JsonPropertyName("Name")]
   public string Name { get; set; } = default!;

   [JsonPropertyName("Url")]
   public string Url { get; set; } = default!;

   public bool IsValid()
   {
      return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Url);
   }
}
