using Main.EmailReading;
using Main.NoPremium;
using Microsoft.Extensions.Logging;

namespace Main;

public interface INoPremiumVoucherProcessor : IDisposable
{
   Task ProcessNoPremiumVouchers();
}

public class NoPremiumVoucherProcessor : INoPremiumVoucherProcessor
{
   private readonly ILogger _logger;
   private readonly VoucherConsumer _voucherConsumer;
   private readonly VoucherProvider _voucherProvider;

   public NoPremiumVoucherProcessor(VoucherProvider voucherProvider, VoucherConsumer voucherConsumer, ILogger logger)
   {
      _voucherProvider = voucherProvider;
      _voucherConsumer = voucherConsumer;
      _logger = logger;
   }

   public async Task ProcessNoPremiumVouchers()
   {
      var vouchers = await _voucherProvider.GetNonConsumedVouchers();
      _logger.LogInformation("Found {VoucherCount} vochers to consume", vouchers.Count);
      foreach (var voucher in vouchers)
      {
         try
         {
            await _voucherConsumer.Consume(voucher.Code);
            voucher.MarkAsSeen();
         }

         catch (VoucherConsumerException ex) when (ex.ErrorCode is VoucherConsumptionErrorCode.CodeWasAlreadyUsed)
         {
            _logger.LogWarning("Voucher with code '{Code}' was already used", voucher.Code);
            voucher.MarkAsSeen();
         }
         catch (VoucherConsumerException ex) when (ex.ErrorCode is VoucherConsumptionErrorCode.InvalidCode)
         {
            _logger.LogWarning(ex, "Error while consuming voucher with code '{Code}' (Invalid code)", voucher.Code);
            voucher.MarkAsSeen();
            //throw;
         }
         catch (Exception e)
         {
            _logger.LogError(e, "Error while consuming voucher with code '{Code}'", voucher.Code);
            throw;
         }
      }
   }

   public void Dispose()
   {
      _voucherProvider.Dispose();
   }
}
