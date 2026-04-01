using MailKit;

namespace Main.EmailReading;

public record EmailMessage
{
   private readonly string _body;

   public EmailMessage(string textBody, string htmlBody, UniqueId uniqueId)
   {
      var isTextBodyEmpty = string.IsNullOrEmpty(textBody);
      var isHtmlBodyEmpty = string.IsNullOrEmpty(htmlBody);

      if (isTextBodyEmpty && isHtmlBodyEmpty)
      {
         throw new ApplicationException(
            $"Trying to construct EmailMessage object, for uniqueId {uniqueId}, but {nameof(textBody)} and {nameof(htmlBody)} arguments are null or empty");
      }

      // prefer textBody,
      _body = textBody;
      if (isTextBodyEmpty)
      {
         // get html body if text body is empty
         _body = htmlBody;
      }
      UniqueId = uniqueId;
   }

   public string Body => _body;



   public UniqueId UniqueId { get; }
}
