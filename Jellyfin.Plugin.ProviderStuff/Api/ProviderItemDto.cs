using System;

namespace Jellyfin.Plugin.ProviderStuff.Api;

/// <summary>
/// Item returned by provider items endpoint.
/// </summary>
public sealed class ProviderItemDto
{
    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type (BaseItemKind as string).
    /// </summary>
    public string Type { get; set; } = string.Empty;
}
