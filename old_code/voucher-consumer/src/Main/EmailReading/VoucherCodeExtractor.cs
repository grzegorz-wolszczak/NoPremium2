using System.Text.RegularExpressions;
using Core.Maybe;

namespace Main.EmailReading;

public class VoucherCodeExtractor
{
   //private const string Pattern = ".*kod doładowujący:\\s+(?<code>\\w+)\\s*";
   private const string Pattern = ".*kod do.adowuj.cy:\\s+.*\\b(?<code>[0-9a-fA-f]{10,})\\b.*";
   private const int ExpectedMatchedGroupCount = 2;
   private static readonly Maybe<string> NoResult = Maybe<string>.Nothing;

   public Maybe<string> ExtractCodeFrom(string emailBody)
   {
      if (string.IsNullOrEmpty(emailBody))
      {
         // warn
         return NoResult;
      }
      var matchResult = Regex.Match(emailBody, Pattern);

      if (matchResult.Groups.Count != ExpectedMatchedGroupCount)
      {
         return NoResult;
      }

      var value = matchResult.Groups[1].Value;
      return value.ToMaybe();
   }
}
