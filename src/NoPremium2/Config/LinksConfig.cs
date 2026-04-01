namespace NoPremium2.Config;

public sealed class LinksConfig
{
    public List<LinkEntry> Links { get; init; } = new();
}

public sealed class LinkEntry
{
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    /// <summary>Size string, e.g. "512MB" or "3GB". Used to estimate transfer budget.</summary>
    public string Size { get; init; } = "";
}
