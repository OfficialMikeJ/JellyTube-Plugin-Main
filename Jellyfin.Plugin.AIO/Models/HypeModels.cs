using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>
/// Persisted store of all hype (like) data across all items.
/// </summary>
public class HypeStore
{
    /// <summary>
    /// Maps itemId → set of userIds who have hyped it.
    /// </summary>
    public Dictionary<Guid, HashSet<Guid>> ItemHypes { get; set; } = new();
}

/// <summary>
/// Response returned for a single item's hype state for a specific user.
/// </summary>
public class HypeStatusResponse
{
    /// <summary>Gets or sets the item ID.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets total hype count for this item.</summary>
    public int Count { get; set; }

    /// <summary>Gets or sets whether the requesting user has hyped this item.</summary>
    public bool UserHyped { get; set; }
}
