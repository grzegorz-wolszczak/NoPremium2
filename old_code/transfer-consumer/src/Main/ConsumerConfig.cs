using Main.Utility;

namespace Main;

public record ConsumerConfig
{
   public required Credentials Credentials { get; init; }
   public required NopremiumConfig NoPremiumConfig { get; init; }
}
