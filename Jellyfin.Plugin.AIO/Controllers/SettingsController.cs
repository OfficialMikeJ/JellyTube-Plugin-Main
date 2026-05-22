using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// Returns public-safe JellyTube settings that the web client reads on startup
/// to configure its UI (e.g. whether to show subscriber counts).
/// </summary>
[ApiController]
[Route("JellyTube/Settings")]
public class SettingsController : ControllerBase
{
    /// <summary>
    /// Returns client-visible settings — no auth required.
    /// GET /JellyTube/Settings
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetPublicSettings()
    {
        var cfg = Plugin.Instance?.Configuration;
        return Ok(new
        {
            hideSubscriberCount   = cfg?.HideSubscriberCount ?? false,
            enableVpnBlock        = cfg?.EnableVpnBlock ?? true,
            maxProfileImageSizeMb = cfg?.MaxProfileImageSizeMb ?? 10,
            maxBannerImageSizeMb  = cfg?.MaxBannerImageSizeMb ?? 5,
            maxThumbnailSizeMb    = cfg?.MaxThumbnailSizeMb ?? 20,
            maxUploadSizeMb       = cfg?.MaxUploadSizeMb ?? 10240,
            chunkSizeMb           = cfg?.ChunkSizeMb ?? 10
        });
    }
}
