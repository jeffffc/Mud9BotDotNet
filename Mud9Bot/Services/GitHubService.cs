using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubService> _logger;
    private readonly string _repo; // Format: "owner/repo"
    private readonly string _token; // GitHub Personal Access Token (Classic)

    public GitHubService(HttpClient httpClient, IConfiguration config, ILogger<GitHubService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _repo = config["GitHub:Repository"] ?? "";
        _token = config["GitHub:PatToken"] ?? "";

        // GitHub API requirement: User-Agent and specific Accept header
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mud9Bot-CI", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        
        if (!string.IsNullOrEmpty(_token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
    }

    public async Task<bool> TriggerDispatchAsync(string eventType, string sha, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_repo) || string.IsNullOrEmpty(_token))
        {
            _logger.LogError("GitHub settings ('GitHub:Repository' or 'GitHub:PatToken') are missing in appsettings.json.");
            return false;
        }

        var url = $"https://api.github.com/repos/{_repo}/dispatches";
        
        // Construct the payload for repository_dispatch
        var payload = new 
        { 
            event_type = eventType, 
            client_payload = new { sha = sha } 
        };
        
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            _logger.LogInformation("Attempting to trigger GitHub event '{Event}' for SHA '{Sha}'...", eventType, sha);
            var response = await _httpClient.PostAsync(url, content, ct);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully dispatched event to GitHub.");
                return true;
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("GitHub API Dispatch failed. Status: {Status}, Error: {Error}", response.StatusCode, error);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while triggering GitHub dispatch.");
            return false;
        }
    }
}