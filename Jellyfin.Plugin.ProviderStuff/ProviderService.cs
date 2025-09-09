using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.ProviderStuff.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ProviderStuff;

/// <summary>
/// Service to fetch provider info from TMDb.
/// </summary>
public class ProviderService
{
    private readonly HttpClient _http;
    private readonly ILogger<ProviderService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderService"/> class.
    /// </summary>
    /// <param name="http">HTTP client.</param>
    /// <param name="logger">Logger.</param>
    public ProviderService(HttpClient http, ILogger<ProviderService> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Get list of provider IDs for given TMDb ID and content type.
    /// </summary>
    /// <param name="tmdbId">tmdb id.</param>
    /// <param name="contentType">content type.</param>
    /// <param name="config">config object.</param>
    /// <param name="ct">cancellation token.</param>
    /// <returns>Task.</returns>
    public async Task<IReadOnlyList<int>> GetProvidersForAsync(string tmdbId, string contentType, PluginConfiguration config, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.TmdbApiKey))
        {
            _logger.LogWarning("TMDb API key not configured.");
            return Array.Empty<int>();
        }

        var url = $"https://api.themoviedb.org/3/{contentType}/{tmdbId}/watch/providers?api_key={Uri.EscapeDataString(config.TmdbApiKey)}";
        try
        {
            var json = await _http.GetFromJsonAsync<TmdbProviderResponse>(url, ct).ConfigureAwait(false);
            var country = config.TmdbCountry ?? "DE";
            if (json?.Results is null || !json.Results.TryGetValue(country, out var region))
            {
                return Array.Empty<int>();
            }

            var ids = new List<int>();
            void Add(IEnumerable<TmdbProvider>? src)
            {
                if (src == null)
                {
                    return;
                }

                foreach (var p in src)
                {
                    ids.Add(p.Provider_id);
                }
            }

            Add(region?.Flatrate);
            Add(region?.Rent);
            Add(region?.Buy);
            return ids.Distinct().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TMDb provider fetch failed for {TmdbId}", tmdbId);
            return Array.Empty<int>();
        }
    }

    /// <summary>
    /// TMDb provider record.
    /// </summary>
    /// <param name="Provider_id">provider id.</param>
    /// <param name="Provider_name">provider name.</param>
    /// <param name="Logo_path">logo path.</param>
    private sealed record TmdbProvider(int Provider_id, string Provider_name, string? Logo_path);

    /// <summary>
    /// TMDb provider region info.
    /// </summary>
    private sealed class TmdbProviderRegion
    {
        /// <summary>
        /// Gets or sets list of flatrate providers.
        /// </summary>
        public List<TmdbProvider>? Flatrate { get; set; }

        /// <summary>
        /// Gets or sets list of rent providers.
        /// </summary>
        public List<TmdbProvider>? Rent { get; set; }

        /// <summary>
        /// Gets or sets list of buy providers.
        /// </summary>
        public List<TmdbProvider>? Buy { get; set; }
    }

    private sealed class TmdbProviderResponse
    {
        public Dictionary<string, TmdbProviderRegion>? Results { get; set; }
    }
}
