using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>
/// Persisted video metadata store.
/// Key = JellyTube video ID (generated at upload-init time = session ID string).
/// </summary>
public class VideoMetaStore
{
    public Dictionary<string, VideoMeta> Videos { get; set; } = new();
}

public class VideoMeta
{
    /// <summary>JellyTube video ID — matches the upload session ID.</summary>
    public string   VideoId         { get; set; } = string.Empty;
    /// <summary>Set by admin after Jellyfin library scan associates the file with an item.</summary>
    public string?  JellyfinItemId  { get; set; }
    public string   FileName        { get; set; } = string.Empty;
    public string   Title           { get; set; } = string.Empty;
    public string   Description     { get; set; } = string.Empty;
    public List<string> Tags        { get; set; } = new();
    public string   Category        { get; set; } = string.Empty;
    public bool     IsSwipeShow     { get; set; }
    public bool     AllowComments   { get; set; } = true;
    /// <summary>public | unlisted | private | scheduled</summary>
    public string   Visibility      { get; set; } = "public";
    public DateTime? ScheduledAt    { get; set; }
    public Guid     UploadedBy      { get; set; }
    public string   UploadedByName  { get; set; } = string.Empty;
    public DateTime UploadedAt      { get; set; } = DateTime.UtcNow;
}

public class SaveVideoMetaRequest
{
    /// <summary>The session ID returned by POST /AIO/Upload/Init.</summary>
    public string   SessionId   { get; set; } = string.Empty;
    public string   FileName    { get; set; } = string.Empty;
    public string   Title       { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public List<string> Tags    { get; set; } = new();
    public string   Category    { get; set; } = string.Empty;
    public bool     IsSwipeShow   { get; set; }
    public bool     AllowComments { get; set; } = true;
    public string   Visibility    { get; set; } = "public";
    public DateTime? ScheduledAt  { get; set; }
}

public class AssociateJellyfinItemRequest
{
    public string JellyfinItemId { get; set; } = string.Empty;
}
