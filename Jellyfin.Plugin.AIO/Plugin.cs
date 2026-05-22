using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.AIO.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.AIO;

/// <summary>
/// JellyTube all-in-one plugin.
/// Adds: chunked uploads, statistics, media requests, hype system,
/// A/B thumbnail testing, content moderation, and registration rate limiting.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>Gets the singleton instance of this plugin.</summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "JellyTube";

    /// <inheritdoc />
    public override string Description => "JellyTube: Uploads, Hype, A/B Thumbnails, Moderation, and Stats.";

    /// <inheritdoc />
    public override Guid Id => new Guid("d3a4f1b2-7c6e-4a90-b831-5e2f0c9d8a71");

    // ── Services ──────────────────────────────────────────────────────────────

    /// <summary>Gets the chunked upload service.</summary>
    public UploadService UploadService { get; }

    /// <summary>Gets the media-request service.</summary>
    public MediaRequestService MediaRequestService { get; }

    /// <summary>Gets the Hype (like) service.</summary>
    public HypeService HypeService { get; }

    /// <summary>Gets the moderation / strike service.</summary>
    public ModerationService ModerationService { get; }

    /// <summary>Gets the A/B thumbnail testing service.</summary>
    public ThumbnailAbService ThumbnailAbService { get; }

    /// <summary>Gets the VPN / IP block service.</summary>
    public VpnBlockService VpnBlockService { get; }

    /// <summary>Gets the TOTP two-factor authentication service.</summary>
    public TotpService TotpService { get; }

    /// <summary>Gets the extended creator image service (banners, high-res thumbnails).</summary>
    public ImageService ImageService { get; }

    /// <summary>Gets the comment service.</summary>
    public CommentService CommentService { get; }

    /// <summary>Gets the video metadata service (title, description, tags, SwipeShow flag, etc.).</summary>
    public VideoMetaService VideoMetaService { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILoggerFactory? loggerFactory = null)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;

        var factory = loggerFactory ?? NullLoggerFactory.Instance;

        UploadService        = new UploadService(factory.CreateLogger<UploadService>());
        MediaRequestService  = new MediaRequestService();
        HypeService          = new HypeService(applicationPaths, factory.CreateLogger<HypeService>());
        ModerationService    = new ModerationService(applicationPaths, factory.CreateLogger<ModerationService>());
        ThumbnailAbService   = new ThumbnailAbService(applicationPaths, factory.CreateLogger<ThumbnailAbService>());
        VpnBlockService      = new VpnBlockService(applicationPaths, factory.CreateLogger<VpnBlockService>());
        TotpService          = new TotpService(applicationPaths, factory.CreateLogger<TotpService>());
        ImageService         = new ImageService(applicationPaths, factory.CreateLogger<ImageService>());
        CommentService       = new CommentService(applicationPaths, factory.CreateLogger<CommentService>());
        VideoMetaService     = new VideoMetaService(applicationPaths, factory.CreateLogger<VideoMetaService>());
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.Configuration.configPage.html",
                    GetType().Namespace)
            }
        };
    }
}
