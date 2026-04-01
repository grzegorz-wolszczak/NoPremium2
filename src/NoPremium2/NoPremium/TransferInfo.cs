using NoPremium2.Infrastructure;

namespace NoPremium2.NoPremium;

public sealed record TransferInfo(long TotalBytes, long PremiumBytes, long ExtraBytes)
{
    public override string ToString() =>
        $"Total: {DataSizeConverter.FormatBytes(TotalBytes)} " +
        $"(Premium: {DataSizeConverter.FormatBytes(PremiumBytes)} + " +
        $"Extra: {DataSizeConverter.FormatBytes(ExtraBytes)})";
}
