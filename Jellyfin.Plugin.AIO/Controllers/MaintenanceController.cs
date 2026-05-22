using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// Public read + admin write endpoint for the JellyTube maintenance alert.
/// </summary>
[ApiController]
[Route("JellyTube/Maintenance")]
public class MaintenanceController : ControllerBase
{
    public record MaintenanceStatus(bool Enabled, string Message);
    public record SetMaintenanceRequest(bool Enabled, string Message);

    /// <summary>
    /// Returns the current maintenance alert status.
    /// No authentication required — called on every page load by all users.
    /// GET /JellyTube/Maintenance
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(MaintenanceStatus), StatusCodes.Status200OK)]
    public ActionResult<MaintenanceStatus> Get()
    {
        var cfg = Plugin.Instance!.Configuration;
        return Ok(new MaintenanceStatus(cfg.MaintenanceEnabled, cfg.MaintenanceMessage));
    }

    /// <summary>
    /// Sets the maintenance alert message and enabled state.
    /// Admin only.
    /// POST /JellyTube/Maintenance
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequiresElevation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Set([FromBody] SetMaintenanceRequest request)
    {
        var cfg = Plugin.Instance!.Configuration;
        cfg.MaintenanceEnabled = request.Enabled;
        cfg.MaintenanceMessage = request.Message ?? cfg.MaintenanceMessage;
        Plugin.Instance!.SaveConfiguration();
        return NoContent();
    }
}
