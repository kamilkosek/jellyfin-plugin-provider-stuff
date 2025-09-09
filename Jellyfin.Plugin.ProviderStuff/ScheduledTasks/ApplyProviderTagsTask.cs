using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ProviderStuff.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ProviderStuff.ScheduledTasks;

/// <summary>
/// Scheduled task to apply provider tags.
/// </summary>
public class ApplyProviderTagsTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<ApplyProviderTagsTask> _logger;
    private readonly ProviderService _providerService;
    private readonly IConfigurationManager _config;
    private readonly ICollectionManager _collectionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApplyProviderTagsTask"/> class.
    /// </summary>
    /// <param name="libraryManager">The library manager used to query and update media items.</param>
    /// <param name="logger">The logger to record diagnostic and error messages.</param>
    /// <param name="providerService">The service used to fetch provider IDs from TMDB.</param>
    /// <param name="config">The configuration manager to access plugin settings.</param>
    /// <param name="collectionManager">The collection manager for creating and updating collections.</param>
    public ApplyProviderTagsTask(ILibraryManager libraryManager, ILogger<ApplyProviderTagsTask> logger, ProviderService providerService, IConfigurationManager config, ICollectionManager collectionManager)
    {
        _libraryManager = libraryManager;
        _logger = logger;
        _providerService = providerService;
        _config = config;
        _collectionManager = collectionManager;
        _logger.LogInformation("Got config: {Config}", _config);
    }

    /// <summary>
    /// Gets Name of the task.
    /// </summary>
    public string Name => "ProviderStuff: Apply provider tags";

    /// <summary>
    /// Gets Description of the task.
    /// </summary>
    public string Description => "Fetch providers from TMDB and apply provider:<name> tags to items with TMDB IDs.";

    /// <summary>
    /// Gets Category of the task.
    /// </summary>
    public string Category => "Metadata";

    /// <summary>
    /// Gets Key of the task.
    /// </summary>
    public string Key => "providerstuff.applyprovidertags";

    /// <summary>
    /// Gets a value indicating whether the task is hidden.
    /// </summary>
    public bool IsHidden => false;

/// <summary>
/// Gets a value indicating whether the task is enabled by default.
/// </summary>
    public bool IsEnabled => true;

    /// <summary>
    /// Gets a value indicating whether the task execution is logged.
    /// </summary>
    public bool IsLogged => true;

    /// <summary>
    /// Gets default triggers for the task.
    /// </summary>
    /// <returns>default triggers.</returns>
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfo.TriggerDaily, TimeOfDayTicks = TimeSpan.FromHours(3).Ticks };
    }

    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <param name="progress">progress.</param>
    /// <param name="cancellationToken">ct .</param>
    /// <returns>Task.</returns>
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        // Resolve configuration once
        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null || string.IsNullOrWhiteSpace(cfg.TmdbApiKey) || cfg.Providers is null || cfg.Providers.Length == 0)
        {
            _logger.LogWarning("Plugin not configured. Aborting run.");
            return;
        }

        // Pre-create and cache collections for providers that enable it
        var providersNeedingCollections = cfg.Providers.Where(p => p.CreateCollection).ToArray();
        var collectionIdsByProvider = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var pendingAddsByCollection = new Dictionary<Guid, HashSet<Guid>>();
        if (providersNeedingCollections.Length > 0)
        {
            _logger.LogInformation("Preparing collections for {Count} providers", providersNeedingCollections.Length);
            foreach (var provider in providersNeedingCollections)
            {
                var collectionName = provider.Name;
                var collections = _libraryManager.GetItemList(new InternalItemsQuery
                {
                    IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                    Name = collectionName,
                    Recursive = true
                });

                if (collections.Count > 0 && collections[0] is BoxSet existing)
                {
                    collectionIdsByProvider[provider.Name] = existing.Id;
                    pendingAddsByCollection[existing.Id] = new HashSet<Guid>();
                }
                else
                {
                    var boxSet = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions { Name = collectionName }).ConfigureAwait(false);
                    collectionIdsByProvider[provider.Name] = boxSet.Id;
                    pendingAddsByCollection[boxSet.Id] = new HashSet<Guid>();
                    _logger.LogInformation("Created collection '{Collection}'", collectionName);
                }
            }
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode },
            Recursive = true
        });

        var total = items.Count;
        var done = 0;
        _logger.LogInformation("Starting provider tag application for {Total} items", total);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await ProcessItemAsync(item, cfg, collectionIdsByProvider, pendingAddsByCollection, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing item {Name}", item.Name);
            }

            done++;

            progress.Report(100.0 * done / total);
            // log every 100 items
            if (done % 100 == 0)
            {
                _logger.LogInformation("Processed {Done}/{Total} items", done, total);
            }
        }

        // Batch add accumulated items to their collections
        if (pendingAddsByCollection.Count > 0)
        {
            foreach (var kvp in pendingAddsByCollection)
            {
                var collectionId = kvp.Key;
                var itemIds = kvp.Value;
                if (itemIds.Count == 0)
                {
                    continue;
                }

                try
                {
                    await _collectionManager.AddToCollectionAsync(collectionId, itemIds).ConfigureAwait(false);
                    _logger.LogInformation("Added {Count} items to collection {CollectionId}", itemIds.Count, collectionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to batch add items to collection {CollectionId}", collectionId);
                }
            }
        }
    }

    private async Task ProcessItemAsync(BaseItem item, PluginConfiguration cfg, Dictionary<string, Guid> collectionIdsByProvider, Dictionary<Guid, HashSet<Guid>> pendingAddsByCollection, CancellationToken ct)
    {
        string? tmdbId = null;
        if (item.ProviderIds is not null)
        {
            // Prefer the canonical "Tmdb" key; fall back to case-insensitive lookup
            if (!item.ProviderIds.TryGetValue("Tmdb", out tmdbId))
            {
                tmdbId = item.ProviderIds.FirstOrDefault(kv => string.Equals(kv.Key, "Tmdb", StringComparison.OrdinalIgnoreCase)).Value;
            }
        }

        if (string.IsNullOrWhiteSpace(tmdbId))
        {
            return;
        }

        // cfg validated in ExecuteAsync

        var contentType = item switch
        {
            Movie => "movie",
            Series => "tv",
            Episode => "tv",
            _ => "movie"
        };

        var providerIds = await _providerService.GetProvidersForAsync(tmdbId, contentType, cfg, ct).ConfigureAwait(false);
        if (providerIds.Count == 0)
        {
            return;
        }

        var matched = new List<string>();
        foreach (var p in cfg.Providers)
        {
            if (p.ProviderIds?.Length > 0 && providerIds.Intersect(p.ProviderIds).Any())
            {
                matched.Add(p.Name);
                if (p.CreateCollection && collectionIdsByProvider.TryGetValue(p.Name, out var collectionId))
                {
                    if (pendingAddsByCollection.TryGetValue(collectionId, out var set))
                    {
                        set.Add(item.Id);
                    }
                }
            }
        }

        if (matched.Count == 0)
        {
            return;
        }

        var tags = item.Tags?.ToList() ?? new List<string>();
        var addedAny = false;
        foreach (var name in matched)
        {
            var tag = $"provider:{name}";
            if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(tag);
                addedAny = true;
            }
        }

        if (addedAny)
        {
            item.Tags = tags.ToArray();
            await _libraryManager.UpdateItemAsync(item, item, ItemUpdateType.MetadataEdit, ct).ConfigureAwait(false);
            _logger.LogInformation("Applied provider tags to {Name}: {Tags}", item.Name, string.Join(", ", matched));
        }
    }

    // removed per-item collection lookup helper; collections are prepared up-front in ExecuteAsync
}
