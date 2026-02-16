namespace Mud9Bot.Services.Interfaces;

public interface IHttpService
{
    // Basic GET returning string
    Task<string?> GetStringAsync(string url, CancellationToken ct = default);
    
    // Basic GET returning bytes (for images like traffic snapshots)
    Task<byte[]?> GetBytesAsync(string url, CancellationToken ct = default);
    
    // POST request (if needed later)
    Task<string?> PostAsync(string url, HttpContent content, CancellationToken ct = default);
}