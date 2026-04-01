using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Logging;

namespace Main.EmailReading;

public class MailboxWithNoPremiumMessages
{
   private readonly ImapConnectionDetails _emailConnectionInfo;
   private readonly ImapClient _imapClient;
   private readonly ILogger _logger;

   public MailboxWithNoPremiumMessages(ImapClient imapClient, ImapConnectionDetails emailConnectionInfo, ILogger logger)
   {
      _imapClient = imapClient;
      _emailConnectionInfo = emailConnectionInfo;
      _logger = logger;
   }

   // documentation on how to use IMAP is here: https://github.com/jstedfast/MailKit
   public async Task<List<EmailMessage>> GetInboxMessagesFromNoPremiumSite()
   {
      var emails = new List<EmailMessage>();

      await AuthenticateAndConnect();
      var inbox = _imapClient.Inbox;

      var notSeenMessages = await inbox.SearchAsync(SearchQuery.NotSeen);
      _logger.LogInformation("Found {NotSeenCount} not read messages", notSeenMessages.Count);
      foreach (var uid in notSeenMessages)
      {
         var message = await inbox.GetMessageAsync(uid, CancellationToken.None);

         var email = new EmailMessage(message.TextBody, message.HtmlBody, uid);

         emails.Add(email);
      }

      return emails;
   }

   private async Task AuthenticateAndConnect()
   {
      _logger.LogInformation("Connecting to email...");
      await _imapClient.ConnectAsync
      (
         _emailConnectionInfo.Server,
         _emailConnectionInfo.Port,
         _emailConnectionInfo.ShouldUseSSL,
         CancellationToken.None);

      _logger.LogInformation("Authenticating to email...");
      await _imapClient.AuthenticateAsync(_emailConnectionInfo.Username, _emailConnectionInfo.Password);

      // The Inbox folder is always available on all IMAP servers...
      IMailFolder inbox = _imapClient.Inbox;
      _logger.LogInformation("Opening inbox...");

      await inbox.OpenAsync(FolderAccess.ReadWrite);

      _logger.LogInformation("Total messages: {Count}", inbox.Count);
      _logger.LogInformation("Recent messages: {Count}", inbox.Recent);
   }
}
