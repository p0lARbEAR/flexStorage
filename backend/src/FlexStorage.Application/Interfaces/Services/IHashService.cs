namespace FlexStorage.Application.Interfaces.Services;

/// <summary>
/// Service for calculating file hashes for deduplication.
/// </summary>
public interface IHashService
{
    /// <summary>
    /// Calculates SHA256 hash of a stream.
    /// </summary>
    /// <returns>Hash in format "sha256:..."</returns>
    Task<string> CalculateSha256Async(
        Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies that a stream matches the expected hash.
    /// </summary>
    Task<bool> VerifyHashAsync(
        Stream stream,
        string expectedHash,
        CancellationToken cancellationToken = default);
}
