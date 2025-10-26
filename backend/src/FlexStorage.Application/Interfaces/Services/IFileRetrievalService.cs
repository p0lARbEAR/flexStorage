
using FlexStorage.Application.DTOs;
using FlexStorage.Domain.DomainServices;
using FlexStorage.Domain.ValueObjects;

namespace FlexStorage.Application.Interfaces.Services;

public interface IFileRetrievalService
{
    Task<Domain.Entities.File?> GetFileMetadataAsync(FileId fileId, CancellationToken cancellationToken = default);

    Task<InitiateRetrievalResult> InitiateRetrievalAsync(FileId fileId, RetrievalTier tier, CancellationToken cancellationToken = default);

    Task<CheckRetrievalStatusResult> CheckRetrievalStatusAsync(string retrievalId, CancellationToken cancellationToken = default);

    Task<DownloadFileResult> DownloadFileAsync(FileId fileId, CancellationToken cancellationToken = default);
}
