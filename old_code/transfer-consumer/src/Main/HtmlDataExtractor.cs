using CSharpFunctionalExtensions;
using HtmlAgilityPack;
using Main.Exceptions;
using Main.Logging;
using Main.Utility;

namespace Main;

public class HtmlDataExtractor : IHtmlDataExtractor
{
   private readonly IMyLogger _logger;

   public HtmlDataExtractor(IMyLogger logger)
   {
      _logger = logger;
   }

   public List<QueuedItemInfo> GetQueuedLinks(string htmlContent)
   {
      var result = new List<QueuedItemInfo>();
      var htmlDoc = new HtmlDocument();
      htmlDoc.LoadHtml(htmlContent);
      // find only those 'tr' element that have td/input elements with attr 'name' = =sid[]
      var nodes = htmlDoc.DocumentNode.SelectNodes("//form[@id='downloadFilesArea']/table//tr[td[input[@name='sid[]']]]");
      // no files are queued
      if (nodes == null)
      {
         return result;
      }

      foreach (var htmlNode in nodes)
      {
         var inputElement = htmlNode.SelectSingleNode("td/input");
         if (inputElement is null)
         {
            throw new InternalErrorException(
               "Could not find 'input' element. This should never happen, either xpath is wrong or NoPremium site changed its html strucutre");
         }

         // get noPremium link id
         string? linkId = inputElement.Attributes.SingleOrDefault(x => x.Name == "value")?.Value;
         if (string.IsNullOrEmpty(linkId))
         {
            throw new InternalErrorException(
               "Could not get linkId from input element, wrong xpath or site html structure changed");
         }


         var anchorId = $"action{linkId}";

         string consumedFileName = string.Empty;

         var anchorElement = htmlNode.SelectSingleNode($"td[@id='{anchorId}']/a");
         if (anchorElement is null)
         {
            // todo: fix that
            // to sie moze zdarzyć gdy link jeszcze sie nie zakolejkowal i jest w stanie "oczekuje"
            // wtedy nie będzie 'anchor' element

            var tdElement = htmlNode.SelectSingleNode($"td[@id='{anchorId}']");
            if (tdElement is null)
            {
               throw new InternalErrorException($"Could not find element with id '{anchorId}', wrong xpath or site html structure changed");
            }

            consumedFileName = tdElement.InnerText.Trim();
         }
         else
         {
            consumedFileName = anchorElement.InnerText.Trim();
         }


         // anchorText is a file name with extension e.g. 'file.mkv'  - we don't need the extention
         var withRemovedExtension = Path.GetFileNameWithoutExtension(consumedFileName);

         result.Add(new QueuedItemInfo() {HashId = linkId, Name = new WhitespaceInsensitiveString(withRemovedExtension)});
      }

      return result;
   }

   public string GetBaseTransferString(string content)
   {
      var result = new List<QueuedItemInfo>();
      var htmlDoc = new HtmlDocument();
      htmlDoc.LoadHtml(content);
      var signedInfoNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@id='signed']");
      if (signedInfoNode is null)
      {
         throw new InternalErrorException("Could not find div element with 'signed' id. wrong xpath or site html structure changed");
      }

      return ExtractTransferTextFrom(signedInfoNode.InnerText);
   }

   private static string ExtractTransferTextFrom(string body)
   {
      var prefixString = "w tym ";
      var prefixIndex = body.IndexOf(prefixString, StringComparison.InvariantCultureIgnoreCase);
      if (prefixIndex < 0) throw new InternalErrorException($"could not find prefix text '{prefixString}'");
      var postFixString = " transferu Premium";
      var postfixIndex = body.IndexOf(postFixString, prefixIndex, StringComparison.InvariantCultureIgnoreCase);
      if (postfixIndex < 0) throw new InternalErrorException($"could not find postfix text '{postFixString}'");

      var startIndex = prefixIndex + prefixString.Length;


      return body[startIndex..postfixIndex].Trim();
   }

   public List<string> GetFileHashes(string htmlContent)
   {
      var result = new List<string>();
      var htmlDoc = new HtmlDocument();
      htmlDoc.LoadHtml(htmlContent);

      var inputElements = htmlDoc.DocumentNode.SelectNodes("//table/tbody/tr/td/input[@type='checkbox']");

      if (inputElements is null)
      {
         _logger.LogError($"Did not get html element to read file hashes, html content '{htmlContent}'" );
         return result;
      }

      foreach (var inputNode in inputElements)
      {
         string? fileHash = inputNode.Attributes.SingleOrDefault(x => x.Name == "value")?.Value;
         if (string.IsNullOrEmpty(fileHash))
         {
            throw new InternalErrorException(
               $"Could not get linkId from input element, wrong xpath or site html structure changed, html content '{htmlContent}'");
         }

         result.Add(fileHash);
      }

      return result;
   }

   public Maybe<long> GetNeededTransferHumanReadable(string htmlContent)
   {
      var result = string.Empty;
      var htmlDoc = new HtmlDocument();
      htmlDoc.LoadHtml(htmlContent);

      try
      {
         var sizeNode = htmlDoc.DocumentNode.SelectSingleNode("//div[@class='input']/input[@id='countSize']/@value");
         var value = sizeNode.GetAttributeValue("value", "NOT_FOUND");
         if (value == "NOT_FOUND")
         {
            _logger.LogError("Could not get size from html content, xpath is wrong or site html structure changed");
            return Maybe<long>.None;
         }

         return Maybe.From(DataSizeConverter.FromHumanReadableToBytes(value));
      }
      catch(Exception e)
      {
         _logger.LogError("Could not get size from html content, xpath is wrong or site html structure changed: " + e.ToString());
         return Maybe<long>.None;
      }
   }

   public Maybe<FilesToDownloadInfo> GetFilesToDownloadData(string htmlContent)
   {
      var links = GetFileHashes(htmlContent);
      if (links.Count == 0)
      {
         return Maybe<FilesToDownloadInfo>.None;
      }

      var transfer = GetNeededTransferHumanReadable(htmlContent);
      if (transfer.HasNoValue)
      {
         return Maybe<FilesToDownloadInfo>.None;
      }

      return new FilesToDownloadInfo(links, transfer.Value);
   }
}

public record FilesToDownloadInfo(List<string> Hashes, long SizeBytes);

public interface IHtmlDataExtractor
{
   public List<QueuedItemInfo> GetQueuedLinks(string htmlContent);
   string GetBaseTransferString(string content);
   List<string> GetFileHashes(string htmlContent);

   Maybe<FilesToDownloadInfo> GetFilesToDownloadData(string htmlContent);
}
