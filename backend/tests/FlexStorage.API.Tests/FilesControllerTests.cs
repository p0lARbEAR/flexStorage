
using Xunit;

using Moq;

using FlexStorage.API.Controllers;

using FlexStorage.Application.Services;

using Microsoft.Extensions.Logging;

using System.Threading.Tasks;

using System.Threading;

using Microsoft.AspNetCore.Mvc;

using FlexStorage.Domain.ValueObjects;

using System;

using FlexStorage.Domain.Entities;

using FlexStorage.Application.Interfaces.Repositories;

using FlexStorage.Application.Interfaces.Services;

using FlexStorage.Domain.DomainServices;

namespace FlexStorage.API.Tests;

public class FilesControllerTests
{
    private readonly Mock<IFileUploadService> _fileUploadServiceMock;
    private readonly Mock<IFileRetrievalService> _fileRetrievalServiceMock;
    private readonly Mock<ILogger<FilesController>> _loggerMock;
    private readonly FilesController _controller;

    public FilesControllerTests()
    {
        _fileUploadServiceMock = new Mock<IFileUploadService>();
        _fileRetrievalServiceMock = new Mock<IFileRetrievalService>();
        _loggerMock = new Mock<ILogger<FilesController>>();

        _controller = new FilesController(
            _fileUploadServiceMock.Object, 
            _fileRetrievalServiceMock.Object, 
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetFileMetadata_WhenFileExists_ShouldReturnOkObjectResult()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileEntity = FlexStorage.Domain.Entities.File.Create(
            UserId.From(Guid.NewGuid()),
            FileMetadata.Create("test.jpg", "sha256:hash", DateTime.UtcNow),
            FileSize.FromBytes(1024),
            FileType.FromMimeType("image/jpeg")
        );

        _fileRetrievalServiceMock
            .Setup(s => s.GetFileMetadataAsync(It.IsAny<FileId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileEntity);

        // Act
        var result = await _controller.GetFileMetadata(fileId, CancellationToken.None);

        // Assert
        var actionResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(fileEntity, actionResult.Value);
    }

    [Fact]
    public async Task GetFileMetadata_WhenFileDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        _fileRetrievalServiceMock
            .Setup(s => s.GetFileMetadataAsync(It.IsAny<FileId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.File?)null);

        // Act
        var result = await _controller.GetFileMetadata(fileId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }
}
