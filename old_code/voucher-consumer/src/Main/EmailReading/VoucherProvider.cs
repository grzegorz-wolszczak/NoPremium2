using Core.Maybe;
using MailKit.Net.Imap;
using Microsoft.Extensions.Logging;

namespace Main.EmailReading;

public class VoucherProvider : IDisposable
{
   private readonly ImapClient _imapClient;
   private readonly ILogger _logger;
   private readonly MailboxWithNoPremiumMessages _mailbox;
   private readonly VoucherCodeExtractor _voucherCodeExtractor;
   private readonly VoucherMessageFactory _voucherMessageFactory;

   public VoucherProvider(ImapClient imapClient, // todo: wrap MyImapClient maybe ?
      MailboxWithNoPremiumMessages mailboxWithNoPremiumMessages,
      VoucherCodeExtractor voucherCodeExtractor,
      VoucherMessageFactory voucherMessageFactory,
      ILogger logger
   )
   {
      _imapClient = imapClient;
      _voucherCodeExtractor = voucherCodeExtractor;
      _voucherMessageFactory = voucherMessageFactory;
      _logger = logger;
      _mailbox = mailboxWithNoPremiumMessages;
   }


   public void Dispose()
   {
      _imapClient.Disconnect(true, CancellationToken.None);
      _imapClient.Dispose();
   }

   public async Task<List<INoPremiumVoucher>> GetNonConsumedVouchers()
   {
      var result = new List<INoPremiumVoucher>();
      var inboxEmails = await _mailbox.GetInboxMessagesFromNoPremiumSite();

      foreach (var email in inboxEmails)
      {
         _logger.LogInformation("Extracting code from email with id '{MessageId}'", email.UniqueId);
         var maybeVoucherCode = _voucherCodeExtractor.ExtractCodeFrom(email.Body);
         if (maybeVoucherCode.IsSomething())
         {
            var voucherCode = maybeVoucherCode.Value();
            _logger.LogInformation("Extracted code '{Code}'", voucherCode);
            var noPremiumVoucher = _voucherMessageFactory.Create(voucherCode, email.UniqueId);
            result.Add(noPremiumVoucher);
         }
         else
         {
            _logger.LogWarning("Could not extract code from email: '{Body}'", email.Body);
         }
      }

      return await Task.FromResult(result);
   }
}
