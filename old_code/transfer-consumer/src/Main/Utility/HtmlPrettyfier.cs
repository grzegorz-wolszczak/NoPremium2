using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace Main.Utility;

public class HtmlPretty
{

   public static string Prettyfy(string html, bool compress = true)
   {
      if (string.IsNullOrWhiteSpace(html))
      {
         ArgumentException.ThrowIfNullOrWhiteSpace(html);
      }

      if (compress)
      {
         html = CompressHtml(html);
      }

      return FormatHtml(html);
   }

   private static string CompressHtml(string html)
   {
      // Usuwanie zbędnych białych znaków i nowych linii
      html = Regex.Replace(html, @"\s+", " ");
      html = Regex.Replace(html, @">\s+<", "><");
      return html.Trim();
   }

   private static string FormatHtml(string html)
   {
      var doc = new HtmlDocument();
      doc.LoadHtml(html);

      using var sw = new StringWriter();
      int indentLevel = 0;
      const string indentString = "    "; // 4 spacje na poziom wcięcia

      foreach (var node in doc.DocumentNode.ChildNodes)
      {
         FormatNode(node, sw, indentLevel, indentString);
      }

      return sw.ToString().Trim();
   }

   private static void FormatNode(HtmlNode node, StringWriter sw, int indentLevel, string indentString)
   {
      if (node.NodeType == HtmlNodeType.Text)
      {
         string text = node.InnerText.Trim();
         if (!string.IsNullOrEmpty(text))
         {
            sw.WriteLine(new string(indentString[0], indentLevel * indentString.Length) + text);
         }
         return;
      }

      if (node.NodeType == HtmlNodeType.Element)
      {
         string indent = new string(indentString[0], indentLevel * indentString.Length);
         sw.WriteLine($"{indent}<{node.OriginalName}{GetAttributes(node)}>");

         foreach (var child in node.ChildNodes)
         {
            FormatNode(child, sw, indentLevel + 1, indentString);
         }

         sw.WriteLine($"{indent}</{node.OriginalName}>");
      }
   }

   static string GetAttributes(HtmlNode node)
   {
      if (!node.Attributes.Any())
         return "";

      var attributes = node.Attributes
         .Select(attr => $"{attr.Name}=\"{attr.Value}\"")
         .Aggregate((curr, next) => $"{curr} {next}");

      return $" {attributes}";
   }
}
