using System;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AIO.Controllers;

/// <summary>
/// API for Google Authenticator TOTP two-factor authentication.
/// Setup and management require the user to be authenticated (DefaultAuthorization).
/// Verification is called right after Jellyfin login — also requires DefaultAuthorization.
/// </summary>
[ApiController]
[Route("JellyTube/TOTP")]
[Authorize(Policy = "DefaultAuthorization")]
public class TotpController : ControllerBase
{
    private Guid? CurrentUserId()
    {
        var claim = User.FindFirst("Jellyfin-UserId")?.Value
                 ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private string CurrentToken() =>
        Request.Headers["X-Emby-Token"].ToString()
        ?? Request.Headers["Authorization"].ToString().Replace("MediaBrowser Token=\"", "").TrimEnd('"');

    // ── Status ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns whether TOTP is configured and/or enabled for the calling user.
    /// GET /JellyTube/TOTP/Status
    /// </summary>
    [HttpGet("Status")]
    [ProducesResponseType(typeof(TotpStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<TotpStatusResponse> Status()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        return Ok(Plugin.Instance!.TotpService.GetStatus(userId.Value));
    }

    // ── Setup flow ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a new TOTP secret and returns the otpauth:// URI.
    /// The client renders this as a QR code for the user to scan.
    /// TOTP is NOT yet enabled — call /Enable after scanning.
    /// POST /JellyTube/TOTP/Setup
    /// </summary>
    [HttpPost("Setup")]
    [ProducesResponseType(typeof(TotpSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<TotpSetupResponse> Setup()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var accountName = User.FindFirst("name")?.Value
                       ?? User.FindFirst("preferred_username")?.Value
                       ?? "admin";

        var result = Plugin.Instance!.TotpService.BeginSetup(userId.Value, accountName);
        return Ok(result);
    }

    /// <summary>
    /// Verifies a code from the authenticator app and enables TOTP for the account.
    /// Must be called after /Setup — sends the first code to confirm the secret was saved correctly.
    /// POST /JellyTube/TOTP/Enable
    /// </summary>
    [HttpPost("Enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult Enable([FromBody] TotpVerifyRequest request)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        bool ok = Plugin.Instance!.TotpService.Enable(userId.Value, request.Code);
        if (!ok) return BadRequest("Invalid or expired code. Please check the time on your device and try again.");
        return NoContent();
    }

    /// <summary>
    /// Disables TOTP for the calling user's account.
    /// POST /JellyTube/TOTP/Disable
    /// </summary>
    [HttpPost("Disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult Disable()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        Plugin.Instance!.TotpService.Disable(userId.Value);
        return NoContent();
    }

    // ── Session verification ──────────────────────────────────────────────────

    /// <summary>
    /// Called by the JellyTube web client immediately after Jellyfin login
    /// when the user has TOTP enabled.  Stamps the current session as verified
    /// for 8 hours on success.
    /// POST /JellyTube/TOTP/Verify
    /// </summary>
    [HttpPost("Verify")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult Verify([FromBody] TotpVerifyRequest request)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        bool ok = Plugin.Instance!.TotpService.VerifySession(userId.Value, request.Code, CurrentToken());
        if (!ok) return Unauthorized("Invalid TOTP code.");
        return NoContent();
    }

    /// <summary>
    /// Checks whether the current session has already passed TOTP verification.
    /// Used by the frontend to decide whether to prompt for a code.
    /// GET /JellyTube/TOTP/SessionOk
    /// </summary>
    [HttpGet("SessionOk")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult SessionOk()
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();
        bool ok = Plugin.Instance!.TotpService.IsSessionVerified(userId.Value, CurrentToken());
        return Ok(new { verified = ok });
    }
}
