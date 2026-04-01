using MailKit;
using Microsoft.Extensions.Logging;

namespace Main;

public interface INoPremiumVoucher
{
   string Code { get; }
   void MarkAsSeen();
}

public class NoPremiumVoucher : INoPremiumVoucher
{
   private readonly IMailFolder _imapClientInbox;
   private readonly ILogger _logger;
   private readonly UniqueId _messageUniqueId;

   public NoPremiumVoucher(
      string voucherCode,
      UniqueId messageUniqueId,
      IMailFolder imapClientInbox, ILogger logger) // todo: IMailFolder to IMessageDeleter (no need to expose Mailkit
   {
      Code = voucherCode;
      _messageUniqueId = messageUniqueId;
      _imapClientInbox = imapClientInbox;
      _logger = logger;
   }

   public string Code { get; }


   public void MarkAsSeen()
   {
      try
      {
         _logger.LogInformation("Marking message with id: {UniqueId} as seen", _messageUniqueId);
         var flagsRequest = new StoreFlagsRequest(StoreAction.Add, MessageFlags.Seen) {Silent = true}; // do not delete, just make it 'seen'
         _imapClientInbox.Store(_messageUniqueId, flagsRequest);
         _imapClientInbox.Expunge();
      }
      catch (Exception e)
      {
         // ignore exception
         _logger.LogWarning(e, "Error while marking message as seen, id {UniqueId}", _messageUniqueId);
      }
   }
}
