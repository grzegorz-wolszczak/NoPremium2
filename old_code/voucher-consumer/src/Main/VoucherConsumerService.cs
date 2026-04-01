using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Main;

public class VoucherConsumerService : BackgroundService
{
   private readonly IHostApplicationLifetime _hostApplicationLifetime;
   private readonly ILogger<VoucherConsumerService> _logger;
   private readonly INoPremiumVoucherProcessor _noPremiumVoucherProcessor;

   public VoucherConsumerService(IHostApplicationLifetime hostApplicationLifetime,
      ILogger<VoucherConsumerService> logger,
      INoPremiumVoucherProcessor noPremiumVoucherProcessor)
   {
      _hostApplicationLifetime = hostApplicationLifetime;
      _logger = logger;
      _noPremiumVoucherProcessor = noPremiumVoucherProcessor;
   }

   protected override Task ExecuteAsync(CancellationToken stoppingToken)
   {
      return Task.Run(async () =>
      {
         try
         {
            _logger.LogInformation("Starting consuming NoPremiumVouchers");
            await _noPremiumVoucherProcessor.ProcessNoPremiumVouchers();
            _logger.LogInformation("[Done] consuming NoPremiumVouchers");
         }
         catch (Exception ex) when (False(() => _logger.LogCritical(ex, "Fatal error")))
         {
            _logger.LogCritical(ex, "While consuming nopremium vouchers fatal error happened");
            throw;
         }
         finally
         {
            _hostApplicationLifetime.StopApplication();
         }
      });
   }

   private static bool False(Action action)
   {
      action();
      return false;
   }
}
