namespace Soraeru.Curator;

public sealed class CuratorOptions
{
    public const string SectionName = "Curator";

    /// <summary>Shared API base URL, e.g. http://localhost:5080</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5080";

    /// <summary>Google Web Client ID for GIS button (same audience as API GoogleAuth).</summary>
    public string GoogleClientId { get; set; } = "";
}
