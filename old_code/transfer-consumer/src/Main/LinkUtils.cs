using Main;

public static class LinkUtils
{
   public static bool IsValidLink(ContentLinkConfig l)
   {
      return !string.IsNullOrWhiteSpace(l.Name) && !string.IsNullOrWhiteSpace(l.Url);
   }

   public static IList<ContentLinkConfig> Valid(this IList<ContentLinkConfig> input)
   {
      return input.Where(IsValidLink).ToList();
   }
}
