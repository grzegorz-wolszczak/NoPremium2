using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Email;

public sealed class VoucherEmail
{
    public UniqueId Uid { get; init; }
    public string Code { get; init; } = "";
}

public interface IEmailService
{
    /// <summary>Connects to IMAP, finds unread messages with voucher codes, disconnects.</summary>
    Task<List<VoucherEmail>> GetUnreadVouchersAsync(CancellationToken ct = default);

    /// <summary>Marks the given message UIDs as seen (consumed).</summary>
    Task MarkAsSeenAsync(IReadOnlyList<UniqueId> uids, CancellationToken ct = default);
}

public sealed class EmailService : IEmailService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly VoucherCodeExtractor _extractor;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        string host, int port,
        string username, string password,
        VoucherCodeExtractor extractor,
        ILogger<EmailService> logger)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
        _extractor = extractor;
        _logger = logger;
    }

    public async Task<List<VoucherEmail>> GetUnreadVouchersAsync(CancellationToken ct = default)
    {
        var results = new List<VoucherEmail>();

        using var client = CreateClient();
        await ConnectAndAuthenticateAsync(client, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        _logger.LogInformation("Total messages: {Total}, Recent: {Recent}", inbox.Count, inbox.Recent);

        var notSeenUids = await inbox.SearchAsync(SearchQuery.NotSeen, ct);
        _logger.LogInformation("Found {Count} unread messages", notSeenUids.Count);

        foreach (var uid in notSeenUids)
        {
            ct.ThrowIfCancellationRequested();
            var message = await inbox.GetMessageAsync(uid, ct);

            var body = message.TextBody ?? message.HtmlBody;
            var code = _extractor.ExtractFrom(body);

            if (code is not null)
            {
                _logger.LogInformation("Extracted voucher code '{Code}' from message UID {Uid}", code, uid);
                results.Add(new VoucherEmail { Uid = uid, Code = code });
            }
            else
            {
                _logger.LogDebug("Message UID {Uid} has no voucher code, skipping", uid);
            }
        }

        await client.DisconnectAsync(quit: true, ct);
        return results;
    }

    public async Task MarkAsSeenAsync(IReadOnlyList<UniqueId> uids, CancellationToken ct = default)
    {
        if (uids.Count == 0) return;

        using var client = CreateClient();
        await ConnectAndAuthenticateAsync(client, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, ct);

        foreach (var uid in uids)
        {
            ct.ThrowIfCancellationRequested();
            await inbox.AddFlagsAsync(uid, MessageFlags.Seen, silent: true, ct);
            _logger.LogInformation("Marked message UID {Uid} as seen", uid);
        }

        await client.DisconnectAsync(quit: true, ct);
    }

    private async Task ConnectAndAuthenticateAsync(ImapClient client, CancellationToken ct)
    {
        _logger.LogInformation("Connecting to IMAP server {Host}:{Port}...", _host, _port);
        await client.ConnectAsync(_host, _port, useSsl: true, ct);

        _logger.LogInformation("Authenticating as {Username}...", _username);
        await client.AuthenticateAsync(_username, _password, ct);

        _logger.LogInformation("Opening inbox...");
    }

    private ImapClient CreateClient()
    {
        var client = new ImapClient();
        // Accept self-signed certs (GMX uses valid certs, but be lenient)
        client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        return client;
    }
}
