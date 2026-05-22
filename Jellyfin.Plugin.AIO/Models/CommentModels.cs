using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.AIO.Models;

/// <summary>Persisted comment store — keyed by Jellyfin item ID string.</summary>
public class CommentStore
{
    public Dictionary<string, List<Comment>> ItemComments { get; set; } = new();
}

public class Comment
{
    public Guid   CommentId    { get; set; } = Guid.NewGuid();
    public Guid   ItemId       { get; set; }
    public Guid   AuthorId     { get; set; }
    public string AuthorName   { get; set; } = string.Empty;
    public string Text         { get; set; } = string.Empty;
    public DateTime PostedAt   { get; set; } = DateTime.UtcNow;
    public bool   IsDeleted    { get; set; }
    public string? DeletedBy   { get; set; }  // admin username who removed it
    public bool   IsPinned     { get; set; }
    public int    Likes        { get; set; }
}

public class PostCommentRequest
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>Safe DTO returned to clients — never exposes AuthorId or internal flags.</summary>
public class CommentDto
{
    public Guid   CommentId  { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Text       { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
    public bool   IsPinned   { get; set; }
    public int    Likes      { get; set; }
    public bool   IsOwn      { get; set; }  // true when the caller is the author
    public bool   CanDelete  { get; set; }  // true for author or admin
}
