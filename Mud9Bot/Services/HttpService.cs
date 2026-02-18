using System.Net.Http; // Required for IHttpClientFactory
using System.Net.Http.Headers;
using Mud9Bot.Interfaces;

namespace Mud9Bot.Services;

public class HttpService(
    IHttpClientFactory httpClientFactory, 
    ILogger<HttpService> logger,
    IErrorReporter errorReporter) : IHttpService
{
    private readonly HttpClient _client = httpClientFactory.CreateClient("Mud9BotClient");

    public async Task<string?> GetStringAsync(string url, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation($"Fetching URL: {url}");
            using var response = await _client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            await errorReporter.ReportErrorAsync(ex, null, ct);
            return null; // Return null on failure so caller can handle graceful degradation
        }
    }

    public async Task<byte[]?> GetBytesAsync(string url, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation($"Fetching Bytes: {url}");
            using var response = await _client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex)
        {
            await errorReporter.ReportErrorAsync(ex, null, ct);
            return null;
        }
    }

    public async Task<string?> PostAsync(string url, HttpContent content, CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation($"Posting to URL: {url}");
            using var response = await _client.PostAsync(url, content, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            await errorReporter.ReportErrorAsync(ex, null, ct);
            return null;
        }
    }
}