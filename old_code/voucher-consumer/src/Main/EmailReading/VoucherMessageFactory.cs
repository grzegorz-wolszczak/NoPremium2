using MailKit;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;

namespace Main.EmailReading;

public class VoucherMessageFactory
{
   private readonly ImapClient _imapClient;
   private readonly ILogger _logger;

   public VoucherMessageFactory(ImapClient imapClient, ILogger logger)
   {
      _imapClient = imapClient;
      _logger = logger;
   }

   public INoPremiumVoucher Create(string voucherCode, UniqueId messageUniqueId)
   {
      return new NoPremiumVoucher(voucherCode, messageUniqueId, _imapClient.Inbox, _logger);
   }
}
