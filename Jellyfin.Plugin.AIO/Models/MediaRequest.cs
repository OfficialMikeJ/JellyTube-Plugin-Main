using System;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>
/// Represents the status of a media request.
/// </summary>
public enum RequestStatus
{
    /// <summary>Pending admin review.</summary>
    Pending,
    /// <summary>Approved and being sourced.</summary>
    Approved,
    /// <summary>Media has been added to the library.</summary>
    Available,
    /// <summary>Request was denied.</summary>
    Denied
}

/// <summary>
/// Represents a single user media request.
/// </summary>
public class MediaRequest
{
    /// <summary>Gets or sets the unique request ID.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the Jellyfin user ID who submitted the request.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>Gets or sets the display name of the user who submitted the request.</summary>
    public string RequestedByUsername { get; set; } = string.Empty;

    /// <summary>Gets or sets the TMDb ID (if available).</summary>
    public string? TmdbId { get; set; }

    /// <summary>Gets or sets the IMDB ID (if available).</summary>
    public string? ImdbId { get; set; }

    /// <summary>Gets or sets the title of the requested media.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the year of the media.</summary>
    public string? Year { get; set; }

    /// <summary>Gets or sets the media type (movie, tv, etc.).</summary>
    public string MediaType { get; set; } = "movie";

    /// <summary>Gets or sets the poster image URL.</summary>
    public string? PosterUrl { get; set; }

    /// <summary>Gets or sets a short synopsis/overview.</summary>
    public string? Overview { get; set; }

    /// <summary>Gets or sets the current request status.</summary>
    public RequestStatus Status { get; set; } = RequestStatus.Pending;

    /// <summary>Gets or sets the optional admin note.</summary>
    public string? AdminNote { get; set; }

    /// <summary>Gets or sets when the request was submitted.</summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets when the status was last updated.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets optional user notes.</summary>
    public string? UserNote { get; set; }
}