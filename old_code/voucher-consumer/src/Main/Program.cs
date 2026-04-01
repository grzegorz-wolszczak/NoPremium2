// See https://aka.ms/new-console-template for more information

using System.Reflection;
using MailKit.Net.Imap;
using Main.EmailReading;
using Main.NoPremium;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;

namespace Main;

public class Program
{
   public static async Task Main(string[] args)
   {
      var config = GetConfigBuilder();

      Serilog.Core.Logger serilog = new LoggerConfiguration()
         .ReadFrom.Configuration(config)
         .CreateLogger();


      var noPremiumVoucherProcessor = GetVoucherProcessor(serilog, config);
      await noPremiumVoucherProcessor.ProcessNoPremiumVouchers();
   }

   private static NoPremiumVoucherProcessor GetVoucherProcessor(Logger serilog, IConfigurationRoot config)
   {
      var loggerFactory = new LoggerFactory().AddSerilog(serilog);

      var noPremiumCredentials = GetNoPremiumCredentials(config);
      var emailCredentials = GetMailUserCredentials(config);

      Microsoft.Extensions.Logging.ILogger logger = loggerFactory.CreateLogger<VoucherConsumerService>();
      var imapConnectionDetails = new ImapConnectionDetails(
         emailCredentials.UserName,
         emailCredentials.UserPassword,
         "imap.gmx.com", // server
         993, // port
         true // should use ssl
      );
      var imapClient = new ImapClient();
      imapClient.ServerCertificateValidationCallback = (_, _, _, _) => { return true; };

      var voucherCodeExtractor = new VoucherCodeExtractor();
      var voucherMessageFactory = new VoucherMessageFactory(imapClient, logger);
      var mailboxWithNoPremiumMessages = new MailboxWithNoPremiumMessages(imapClient, imapConnectionDetails, logger);
      VoucherProvider voucherProvider = new VoucherProvider(
         imapClient,
         mailboxWithNoPremiumMessages,
         voucherCodeExtractor,
         voucherMessageFactory,
         logger);

      var noPremiumVoucherConsumer = new VoucherConsumer(logger, noPremiumCredentials);
      var noPremiumVoucherProcessor = new NoPremiumVoucherProcessor(voucherProvider, noPremiumVoucherConsumer, logger);
      return noPremiumVoucherProcessor;
   }

   private static Credentials GetMailUserCredentials(IConfigurationRoot config)
   {
      var emailUserName = config.GetValue<string>("MailUserName");
      var emailUserPassword = config.GetValue<string>("MailUserPassword");
      var emailCredentials = new Credentials(emailUserName, emailUserPassword);
      return emailCredentials;
   }

   private static Credentials GetNoPremiumCredentials(IConfigurationRoot config)
   {
      var noPremiumUser = config.GetValue<string>("NopremiumUserName");
      var noPremiumPassword = config.GetValue<string>("NopremiumUserPassword");
      var noPremiumCredentials = new Credentials(noPremiumUser, noPremiumPassword);
      return noPremiumCredentials;
   }

   private static IConfigurationRoot GetConfigBuilder()
   {
      var configBuilder = new ConfigurationBuilder();
      configBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
      configBuilder.AddEnvironmentVariables();
      configBuilder.AddUserSecrets(Assembly.GetCallingAssembly(), /*optional*/ true );
      const string userSecretId = "41b78a6c-4c45-4dce-906e-1a385dc5618a";
      configBuilder.AddUserSecrets(userSecretId);
      var config = configBuilder.Build();
      return config;
   }
}