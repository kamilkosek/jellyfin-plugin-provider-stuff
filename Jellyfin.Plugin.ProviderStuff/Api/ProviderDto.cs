using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.ProviderStuff.Api;

/// <summary>
/// Provider DTO exposed via API.
/// </summary>
public sealed class ProviderDto
{
    /// <summary>
    /// Gets or sets the provider display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the TMDb provider IDs that map to this provider.
    /// </summary>
    [SuppressMessage(category: "Performance", checkId: "CA1819", Target = "ArtworkRepos", Justification = "Xml Serializer doesn't support IReadOnlyList")]
    public int[] ProviderIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets a value indicating whether a collection should be created and maintained for this provider.
    /// </summary>
    public bool CreateCollection { get; set; }

    /// <summary>
    /// Gets or sets the ID of the collection associated with this provider.
    /// </summary>
    public string CollectionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL to a provider logo.
    /// </summary>
    public string ProviderLogoUrl { get; set; } = string.Empty;
}
