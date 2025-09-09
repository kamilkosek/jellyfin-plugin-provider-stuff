using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ProviderStuff.Configuration;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ProviderStuff.Api;

/// <summary>
/// API endpoints for ProviderStuff.
/// </summary>
[ApiController]
[Authorize]
[Route("providerstuff")]
public class ProvidersController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IDtoService _dtoService;
    private readonly IUserManager _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProvidersController"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager for item lookups.</param>
    /// <param name="dtoService">DTO service to map items.</param>
    /// <param name="userManager">User manager for user context.</param>
    public ProvidersController(ILibraryManager libraryManager, IDtoService dtoService, IUserManager userManager)
    {
        _libraryManager = libraryManager;
        _dtoService = dtoService;
        _userManager = userManager;
    }

    /// <summary>
    /// List configured providers.
    /// </summary>
    /// <returns>List of providers with name and ids.</returns>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<ProviderDto>), 200)]
    public ActionResult<IEnumerable<ProviderDto>> GetProviders()
    {
        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null)
        {
            return Ok(Array.Empty<ProviderDto>());
        }

        var list = (cfg.Providers ?? Array.Empty<Provider>())
            .Select(p =>
            {
                string collectionId = string.Empty;
                if (p.CreateCollection)
                {
                    var items = _libraryManager.GetItemList(new InternalItemsQuery
                    {
                        IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                        Name = p.Name,
                        Recursive = true
                    });
                    var existing = items.FirstOrDefault();
                    if (existing is not null)
                    {
                        collectionId = existing.Id.ToString();
                    }
                }

                return new ProviderDto
                {
                    Name = p.Name,
                    ProviderIds = (int[])(p.ProviderIds ?? Array.Empty<int>()),
                    CreateCollection = p.CreateCollection,
                    ProviderLogoUrl = p.ProviderLogoUrl,
                    CollectionId = collectionId
                };
            })
            .ToArray();

        return Ok(list);
    }

    /// <summary>
    /// Get items tagged for a given provider.
    /// </summary>
    /// <param name="providerName">Provider name as configured (case-insensitive).</param>
    /// <param name="userId">Optional user context for DTO shaping.</param>
    /// <param name="includeItemTypes">Optional item kinds to include; defaults to Movie, Series, Episode.</param>
    /// <param name="limit">Optional limit.</param>
    /// <param name="startIndex">Optional zero-based index of the first item to return. Defaults to 0.</param>
    /// <returns>List of provider items.</returns>
    [HttpGet("{providerName}/items")]
    [ProducesResponseType(typeof(QueryResult<BaseItemDto>), 200)]
    public ActionResult<QueryResult<BaseItemDto>> GetProviderItems(
        [FromRoute] string providerName,
        [FromQuery] Guid? userId,
        [FromQuery] BaseItemKind[]? includeItemTypes,
        [FromQuery] int? limit,
        [FromQuery] int? startIndex)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return Ok(new QueryResult<BaseItemDto>());
        }

        var tag = $"provider:{providerName}";
        var kinds = (includeItemTypes is not null && includeItemTypes.Length > 0)
            ? includeItemTypes
            : new[] { BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode };

        var baseQuery = new InternalItemsQuery
        {
            IncludeItemTypes = kinds,
            Recursive = true,
            Tags = new[] { tag }
        };

        // Get total count (could be optimized with dedicated count API if available)
        var totalItems = _libraryManager.GetItemList(baseQuery);
        var total = totalItems.Count;

        // Page query
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = kinds,
            Recursive = true,
            Tags = new[] { tag },
            StartIndex = Math.Max(0, startIndex ?? 0),
            Limit = limit is > 0 ? limit : null
        };

        var pageItems = _libraryManager.GetItemList(query);
        var user = userId.HasValue ? _userManager.GetUserById(userId.Value) : null;
        var dtos = _dtoService.GetBaseItemDtos(pageItems, new DtoOptions(), user).ToArray();
        var result = new QueryResult<BaseItemDto>
        {
            Items = dtos,
            TotalRecordCount = total,
            StartIndex = query.StartIndex ?? 0
        };
        return Ok(result);
    }
}
