
using FlexStorage.Application.DTOs;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Interfaces.Services;

public interface IFileUploadService
{
    Task<UploadFileResult> UploadAsync(
        UserId userId,
        Stream fileStream,
        string fileName,
        string mimeType,
        DateTime capturedAt,
        CancellationToken cancellationToken = default);
}
