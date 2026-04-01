namespace Main.Logging;

public interface IMyLogger
{
   void LogError(string message);
   void LogInformation(string message);
   void LogWarning(string message, Exception ex);
   void LogWarning(string message);
}
