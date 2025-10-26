
using System.Security.Cryptography;
using System.Text;
using FlexStorage.Application.Interfaces.Services;

namespace FlexStorage.Infrastructure.Services;

public class HashService : IHashService
{
    public async Task<string> CalculateSha256Async(Stream stream, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return $"sha256:{sb.ToString()}";
    }

    public async Task<bool> VerifyHashAsync(Stream stream, string expectedHash, CancellationToken cancellationToken = default)
    {
        var actualHash = await CalculateSha256Async(stream, cancellationToken);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
