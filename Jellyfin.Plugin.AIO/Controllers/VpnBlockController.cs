using System;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// Admin API for managing the VPN / IP blacklist.
/// All endpoints require Jellyfin admin (RequiresElevation).
/// The middleware itself runs on every request automatically — no admin action is needed
/// unless you want to manually whitelist or blacklist a specific IP.
/// </summary>
[ApiController]
[Route("JellyTube/VpnBlock")]
[Authorize(Policy = "RequiresElevation")]
public class VpnBlockController : ControllerBase
{
    /// <summary>
    /// Returns the current blacklist and whitelist.
    /// GET /JellyTube/VpnBlock/List
    /// </summary>
    [HttpGet("List")]
    [ProducesResponseType(typeof(VpnBlockListResponse), StatusCodes.Status200OK)]
    public ActionResult<VpnBlockListResponse> GetList() =>
        Ok(Plugin.Instance!.VpnBlockService.GetList());

    /// <summary>
    /// Manually blacklists an IP.
    /// POST /JellyTube/VpnBlock/Blacklist/{ip}
    /// </summary>
    [HttpPost("Blacklist/{ip}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Blacklist([FromRoute] string ip)
    {
        Plugin.Instance!.VpnBlockService.Blacklist(ip, "Manual admin action");
        return NoContent();
    }

    /// <summary>
    /// Whitelists an IP so it is never blocked (even if it matches a VPN range).
    /// POST /JellyTube/VpnBlock/Whitelist/{ip}
    /// </summary>
    [HttpPost("Whitelist/{ip}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Whitelist([FromRoute] string ip)
    {
        Plugin.Instance!.VpnBlockService.Whitelist(ip);
        return NoContent();
    }

    /// <summary>
    /// Removes an IP from both the blacklist and whitelist (resets to default VPN-check behaviour).
    /// DELETE /JellyTube/VpnBlock/{ip}
    /// </summary>
    [HttpDelete("{ip}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Remove([FromRoute] string ip)
    {
        Plugin.Instance!.VpnBlockService.Remove(ip);
        return NoContent();
    }

    /// <summary>
    /// Checks whether a specific IP would currently be blocked.
    /// GET /JellyTube/VpnBlock/Check/{ip}
    /// </summary>
    [HttpGet("Check/{ip}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult Check([FromRoute] string ip) =>
        Ok(new { ip, blocked = Plugin.Instance!.VpnBlockService.IsBlocked(ip) });
}
