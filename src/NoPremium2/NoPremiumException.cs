namespace NoPremium2;

public class NoPremiumException : Exception
{
   public NoPremiumException(string? message) : base(message)
   {
   }

   public NoPremiumException(string? message, Exception? innerException) : base(message, innerException)
   {
   }
}