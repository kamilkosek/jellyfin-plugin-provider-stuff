using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Jellyfin.Plugin.ProviderStuff.Configuration;

/// <summary>
/// Provider definition.
/// </summary>
public class Provider
{
    /// <summary>
    /// Gets or sets Name of the provider.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or setsList of provider IDs.
    /// </summary>
    [SuppressMessage(category: "Performance", checkId: "CA1819", Target = "ArtworkRepos", Justification = "Xml Serializer doesn't support IReadOnlyList")]
    public int[] ProviderIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Gets or sets URL of the provider logo.
    /// </summary>
    public string ProviderLogoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to create a collection for this provider.
    /// </summary>
    public bool CreateCollection { get; set; } = false;
}
