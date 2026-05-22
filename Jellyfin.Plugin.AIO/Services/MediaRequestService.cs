using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Plugin.AIO.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.AIO.Services;

/// <summary>
/// Manages media requests and TMDb/IMDb discovery.
/// </summary>
public class MediaRequestService
{
    private readonly ILogger<MediaRequestService> _logger;

    // Single shared HttpClient — safe for long-lived reuse
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private readonly string _dataFilePath;
    private readonly object _fileLock = new();

    private const string TmdbBaseUrl = "https://api.themoviedb.org/3";
    private const string TmdbImageBase = "https://image.tmdb.org/t/p/w500";

    /// <summary>
    /// Initializes a new instance of <see cref="MediaRequestService"/> with a null logger.
    /// </summary>
    public MediaRequestService() : this(NullLogger<MediaRequestService>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="MediaRequestService"/> with a provided logger.
    /// </summary>
    public MediaRequestService(ILogger<MediaRequestService> logger)
    {
        _logger = logger;

        string dataDir = Plugin.Instance!.DataFolderPath;
        Directory.CreateDirectory(dataDir);
        _dataFilePath = Path.Combine(dataDir, "media_requests.json");
    }

    // ─── Persistence ─────────────────────────────────────────────────────────

    private List<MediaRequest> LoadRequests()
    {
        lock (_fileLock)
        {
            if (!File.Exists(_dataFilePath))
                return new List<MediaRequest>();
            var json = File.ReadAllText(_dataFilePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<List<MediaRequest>>(json) ?? new List<MediaRequest>();
        }
    }

    private void SaveRequests(List<MediaRequest> requests)
    {
        lock (_fileLock)
        {
            var json = JsonConvert.SerializeObject(requests, Formatting.Indented);
            File.WriteAllText(_dataFilePath, json, Encoding.UTF8);
        }
    }

    // ─── CRUD ────────────────────────────────────────────────────────────────

    /// <summary>Returns all requests.</summary>
    public List<MediaRequest> GetAll() => LoadRequests();

    /// <summary>Returns requests for a specific user.</summary>
    public List<MediaRequest> GetByUser(Guid userId) =>
        LoadRequests().Where(r => r.RequestedByUserId == userId).ToList();

    /// <summary>Adds a new request.</summary>
    public MediaRequest AddRequest(MediaRequest request)
    {
        var requests = LoadRequests();
        request.Id = Guid.NewGuid();
        request.RequestedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        requests.Add(request);
        SaveRequests(requests);
        _logger.LogInformation("New request #{Id} submitted by {User}: {Title}",
            request.Id, request.RequestedByUsername, request.Title);
        return request;
    }

    /// <summary>Updates the status of a request (admin action).</summary>
    public MediaRequest? UpdateStatus(Guid id, RequestStatus status, string? adminNote)
    {
        var requests = LoadRequests();
        var req = requests.FirstOrDefault(r => r.Id == id);
        if (req is null) return null;

        req.Status = status;
        req.AdminNote = adminNote;
        req.UpdatedAt = DateTime.UtcNow;
        SaveRequests(requests);
        return req;
    }

    /// <summary>Deletes a request.</summary>
    public bool DeleteRequest(Guid id)
    {
        var requests = LoadRequests();
        int removed = requests.RemoveAll(r => r.Id == id);
        if (removed > 0) SaveRequests(requests);
        return removed > 0;
    }

    // ─── TMDb Discovery ──────────────────────────────────────────────────────

    private string TmdbApiKey => Plugin.Instance?.Configuration.TmdbApiKey ?? string.Empty;

    private async Task<JObject?> TmdbGetAsync(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(TmdbApiKey))
        {
            _logger.LogWarning("TMDb API key is not configured.");
            return null;
        }

        string url = $"{TmdbBaseUrl}/{endpoint}&api_key={TmdbApiKey}";

        try
        {
            var response = await HttpClient.GetStringAsync(url).ConfigureAwait(false);
            return JObject.Parse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TMDb request failed: {Url}", url);
            return null;
        }
    }

    /// <summary>Searches TMDb for movies and TV shows.</summary>
    public async Task<JArray> SearchAsync(string query)
    {
        var data = await TmdbGetAsync($"search/multi?query={Uri.EscapeDataString(query)}")
            .ConfigureAwait(false);
        return (data?["results"] as JArray) ?? new JArray();
    }

    /// <summary>Gets currently in-theaters movies.</summary>
    public async Task<JArray> GetNowPlayingAsync()
    {
        var data = await TmdbGetAsync("movie/now_playing?language=en-US&page=1")
            .ConfigureAwait(false);
        return NormalizePosterUrls((data?["results"] as JArray) ?? new JArray());
    }

    /// <summary>Gets upcoming movies.</summary>
    public async Task<JArray> GetUpcomingAsync()
    {
        var data = await TmdbGetAsync("movie/upcoming?language=en-US&page=1")
            .ConfigureAwait(false);
        return NormalizePosterUrls((data?["results"] as JArray) ?? new JArray());
    }

    /// <summary>Gets popular movies.</summary>
    public async Task<JArray> GetPopularMoviesAsync()
    {
        var data = await TmdbGetAsync("movie/popular?language=en-US&page=1")
            .ConfigureAwait(false);
        return NormalizePosterUrls((data?["results"] as JArray) ?? new JArray());
    }

    /// <summary>Gets popular TV shows.</summary>
    public async Task<JArray> GetPopularTvAsync()
    {
        var data = await TmdbGetAsync("tv/popular?language=en-US&page=1")
            .ConfigureAwait(false);
        return NormalizePosterUrls((data?["results"] as JArray) ?? new JArray());
    }

    private static JArray NormalizePosterUrls(JArray arr)
    {
        foreach (var item in arr)
        {
            var posterPath = item["poster_path"]?.ToString();
            if (!string.IsNullOrEmpty(posterPath))
                item["poster_url"] = $"{TmdbImageBase}{posterPath}";
        }

        return arr;
    }
}