namespace Main.Utility;

public record QueuedItemInfo
{
   public required WhitespaceInsensitiveString Name { get; init; }
   public required string HashId { get; init; }
}
