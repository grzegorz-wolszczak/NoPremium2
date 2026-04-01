using System.Net;
using Flurl.Http;
using Microsoft.Extensions.Logging;

namespace Main.NoPremium;

public enum VoucherConsumptionErrorCode
{
   NotLoggedIn = 0,
   InvalidCode = 1,
   CodeWasAlreadyUsed = 2,
   ConsumeResponseBodyInvalid = 3,
   CaptchaDetected = 4
}

public class VoucherConsumerException : Exception
{
   public readonly VoucherConsumptionErrorCode ErrorCode;

   public VoucherConsumerException(VoucherConsumptionErrorCode errorCode, string? message) : base(message)
   {
      ErrorCode = errorCode;
   }
}

public class Credentials
{
   public Credentials(string? userName, string? userPassword)
   {
      ThrowIfNullOrEmpty(userName, nameof(userName));
      ThrowIfNullOrEmpty(userPassword, nameof(userPassword));
      UserName = userName!;
      UserPassword = userPassword!;
   }

   private static void ThrowIfNullOrEmpty(string? argValue, string argName)
   {
      if (string.IsNullOrEmpty(argValue))
      {
         throw new ArgumentNullException(argName, $"'{argName}' is null or empty");
      }
   }

   public string UserName { get; }
   public string UserPassword { get; private set; }
}

public class VoucherConsumer
{
   private const string BaseUrl = "https://www.nopremium.pl";
   private const string LoginUrl = $"{BaseUrl}/login";
   private const string LogoutUrl = $"{BaseUrl}/logout";
   private const string VoucherUrl = $"{BaseUrl}/voucher";
   private const string SumbitParameterValue = "Doładuj";
   private const string SuccessfullLoggedInRecognitionSubstring = "Zalogowany jako";
   private const string VoucherIsInvalidErrorText = "Wprowadzony kod nie istnieje";
   private const string VoucherWasSuccessfullyConsumedInformation = "Konto doładowano pomyślnie";
   private const string VoucherWasAlreadUsedErrorText = "Wprowadzony kod został już wykorzystany";
   private const string VoucherExpired= "Wprowadzony kod stracił ważność";
   private const string CaptchaDetectedErrorText = "Przepisz kod z obrazka:";
   private readonly ILogger _logger;
   private CookieJar? _cookieJar = null;
   private readonly string _userName;
   private readonly string _nopremiumUserPassoword;

   public VoucherConsumer(ILogger logger, Credentials credentials)
   {
      _logger = logger;
      _userName = credentials.UserName;
      _nopremiumUserPassoword = credentials.UserPassword;
   }


   public async Task Consume(string voucherCode)
   {
      _cookieJar = await Login(LoginUrl);
      await ConsumeVoucher(voucherCode);
   }


   private async Task ConsumeVoucher(string voucherValue)
   {
      var response = await VoucherUrl
         .WithCookies(_cookieJar)
         .WithHeader("Content-Type", "application/x-www-form-urlencoded")
         .PostUrlEncodedAsync(new
         {
            voucher = voucherValue,
            submit = SumbitParameterValue,
         });

      if (response.StatusCode != (int) HttpStatusCode.OK)
      {
         throw new ApplicationException($"Invalid response: {response.ResponseMessage.ReasonPhrase}");
      }

      var body = await response.GetStringAsync();

      VerifyVoucherWasAdded(body);
   }

   private void VerifyVoucherWasAdded(string body)
   {
      const string CouldNotConsumeVoucherLogPrefix = "Could not consume voucher: ";
      if (!body.Contains(SuccessfullLoggedInRecognitionSubstring))
      {
         throw new VoucherConsumerException(VoucherConsumptionErrorCode.NotLoggedIn,
            $"{CouldNotConsumeVoucherLogPrefix} You are not logged in");
      }


      if (body.Contains(CaptchaDetectedErrorText))
      {
         throw new VoucherConsumerException(VoucherConsumptionErrorCode.CaptchaDetected,
            "Automatic voucher consumer failed. Captcha detected");
      }

      if (body.Contains(VoucherIsInvalidErrorText))
      {
         throw new VoucherConsumerException(VoucherConsumptionErrorCode.InvalidCode,
            $"{CouldNotConsumeVoucherLogPrefix} Voucher code does not exist (is incorrect)");
      }

      if (body.Contains(VoucherWasAlreadUsedErrorText))
      {
         throw new VoucherConsumerException(
            VoucherConsumptionErrorCode.CodeWasAlreadyUsed,
            $"{CouldNotConsumeVoucherLogPrefix} Voucher code was already used");
      }

      if (body.Contains(VoucherExpired, StringComparison.InvariantCultureIgnoreCase))
      {
         _logger.LogWarning("Voucher expired");
         return;
      }

      if (body.Contains(VoucherWasSuccessfullyConsumedInformation))
      {
         _logger.LogInformation("OK. Voucher was consumed on nopremium site");
         return;
      }



      throw new VoucherConsumerException(
         VoucherConsumptionErrorCode.ConsumeResponseBodyInvalid,
         $"{CouldNotConsumeVoucherLogPrefix} Could not find text '{VoucherWasSuccessfullyConsumedInformation}' in page body '{body}'");
   }

   private async Task<CookieJar> Login(string loginUrl)
   {
      var response = await loginUrl
         .WithCookies(out var cookieJar)
         .WithHeader("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9")
         .WithHeader("Content-Type", "application/x-www-form-urlencoded")
         //.WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/106.0.0.0 Safari/537.36 OPR/92.0.0.0")
         .PostUrlEncodedAsync(new
         {
            login = _userName,
            password = _nopremiumUserPassoword,
            remember = "off"
         });

      var body = await response.GetStringAsync();

      if (response.StatusCode != (int) HttpStatusCode.OK)
      {
         Console.WriteLine($"No premium return body: {body}");
         throw new ApplicationException($"Invalid response: {response.ResponseMessage.ReasonPhrase}");
      }



      VerifyLoggedIn(body);
      return cookieJar;
   }

   private void VerifyLoggedIn(string body)
   {
      if (!body.Contains(SuccessfullLoggedInRecognitionSubstring))
      {
         Console.WriteLine($"No premium return body: {body}");
         throw new ApplicationException("Could not log to nopremium site");
      }

      _logger.LogInformation("Login to nopremium site was successful");
   }
}