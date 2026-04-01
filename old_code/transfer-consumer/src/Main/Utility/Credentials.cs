namespace Main.Utility;

public record Credentials
{
   public required string UserName { get; init; }

   public required string UserPassword  { get; init; }

   public required ulong BytesToPreserveLimit { get; init; }

}
