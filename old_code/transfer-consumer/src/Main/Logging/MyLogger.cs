using Serilog.Core;

namespace Main.Logging;

public class MyLogger : IMyLogger
{
   private readonly Logger _logger;

   public MyLogger(Logger logger)
   {
      _logger = logger;
   }

   public void LogError(string message)
   {
      _logger.Error(message);
   }

   public void LogInformation(string message)
   {
      _logger.Information(message);
   }

   public void LogWarning(string message, Exception ex)
   {
      _logger.Warning(ex, message);
   }

   public void LogWarning(string message)
   {
      _logger.Warning(message);
   }
}
