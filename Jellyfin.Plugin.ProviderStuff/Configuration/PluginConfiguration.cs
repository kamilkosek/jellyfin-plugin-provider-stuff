using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ProviderStuff.Configuration;

/// <summary>
/// Plugin configuration for the ProviderStuff plugin.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets API Key for TMDB.
    /// </summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets Country code for TMDB.
    /// </summary>
    public string TmdbCountry { get; set; } = "DE";

    /// <summary>
    /// Gets or sets List of providers.
    /// </summary>
    [SuppressMessage(category: "Performance", checkId: "CA1819", Target = "ArtworkRepos", Justification = "Xml Serializer doesn't support IReadOnlyList")]
    public Provider[] Providers { get; set; } = Array.Empty<Provider>();
}
