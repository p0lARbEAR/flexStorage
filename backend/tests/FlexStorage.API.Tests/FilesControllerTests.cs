
using Xunit;

using Moq;

using FlexStorage.API.Controllers;

using FlexStorage.Application.Services;

using Microsoft.Extensions.Logging;

using System.Threading.Tasks;

using System.Threading;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

using FlexStorage.Domain.ValueObjects;

using System;

using FlexStorage.Domain.Entities;

using FlexStorage.Application.Interfaces.Repositories;

using FlexStorage.Application.Interfaces.Services;
using FlexStorage.Application.DTOs;
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

    [Fact]
    public async Task DownloadFile_WhenFileExists_ShouldReturnFileResult()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var fileStream = new MemoryStream([1, 2, 3, 4, 5]);
        var fileName = "test-file.jpg";
        var contentType = "image/jpeg";
        var fileSize = 5L;

        var downloadResult = DownloadFileResult.SuccessResult(
            fileStream, fileName, contentType, fileSize);

        _fileRetrievalServiceMock
            .Setup(s => s.DownloadFileAsync(It.IsAny<FileId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResult);

        // Act
        var result = await _controller.DownloadFile(fileId, CancellationToken.None);

        // Assert
        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal(fileStream, fileResult.FileStream);
        Assert.Equal(contentType, fileResult.ContentType);
        Assert.Equal(fileName, fileResult.FileDownloadName);
    }

    [Fact]
    public async Task DownloadFile_WhenFileNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var downloadResult = DownloadFileResult.FailureResult("File not found");

        _fileRetrievalServiceMock
            .Setup(s => s.DownloadFileAsync(It.IsAny<FileId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResult);

        // Act
        var result = await _controller.DownloadFile(fileId, CancellationToken.None);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.NotNull(notFoundResult.Value);
        Assert.Contains("File not found", notFoundResult.Value.ToString());
    }

    [Fact]
    public async Task DownloadFile_WhenDownloadFails_ShouldReturnBadRequest()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var downloadResult = DownloadFileResult.FailureResult("Storage service unavailable");

        _fileRetrievalServiceMock
            .Setup(s => s.DownloadFileAsync(It.IsAny<FileId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResult);

        // Act
        var result = await _controller.DownloadFile(fileId, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
        Assert.Contains("Storage service unavailable", badRequestResult.Value.ToString());
    }

    [Fact]
    public async Task DownloadFile_WhenSuccessButStreamIsNull_ShouldReturnInternalServerError()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var downloadResult = new DownloadFileResult
        {
            Success = true,
            FileStream = null, // This should not happen but we test for it
            FileName = "test.jpg",
            ContentType = "image/jpeg"
        };

        _fileRetrievalServiceMock
            .Setup(s => s.DownloadFileAsync(It.IsAny<FileId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(downloadResult);

        // Act
        var result = await _controller.DownloadFile(fileId, CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        Assert.NotNull(statusCodeResult.Value);
        Assert.Contains("File stream is not available", statusCodeResult.Value.ToString());
    }

    [Fact]
    public async Task ListFiles_ShouldReturnFilesList()
    {
        // Arrange
        var userId = UserId.From(Guid.Parse("123e4567-e89b-12d3-a456-426614174000"));
        var files = new List<Domain.Entities.File>
        {
            FlexStorage.Domain.Entities.File.Create(
                userId,
                FileMetadata.Create("file1.jpg", "sha256:hash1", DateTime.UtcNow),
                FileSize.FromBytes(1024),
                FileType.FromMimeType("image/jpeg")
            ),
            FlexStorage.Domain.Entities.File.Create(
                userId,
                FileMetadata.Create("file2.pdf", "sha256:hash2", DateTime.UtcNow),
                FileSize.FromBytes(2048),
                FileType.FromMimeType("application/pdf")
            )
        };

        _fileRetrievalServiceMock
            .Setup(s => s.GetUserFilesAsync(It.IsAny<UserId>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        // Act
        var result = await _controller.ListFiles(1, 50, null, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
        
        // Verify the service was called with correct parameters
        _fileRetrievalServiceMock.Verify(
            s => s.GetUserFilesAsync(userId, 1, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadFile_WithValidFile_ShouldReturnCreated()
    {
        // Arrange
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        var fileName = "test-photo.jpg";
        var contentType = "image/jpeg";
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var fileId = FileId.New();

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(fileContent.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(fileContent));

        var command = new UploadFileCommand
        {
            File = mockFile.Object,
            UserId = userId,
            CapturedAt = DateTime.UtcNow
        };

        var uploadResult = UploadFileResult.SuccessResult(fileId);

        _fileUploadServiceMock
            .Setup(s => s.UploadAsync(
                It.IsAny<UserId>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult);

        // Act
        var result = await _controller.UploadFile(command, CancellationToken.None);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(FilesController.GetFileMetadata), createdResult.ActionName);
        Assert.NotNull(createdResult.RouteValues);
        Assert.Equal(fileId.Value, createdResult.RouteValues["id"]);

        // Verify the upload service was called
        _fileUploadServiceMock.Verify(
            s => s.UploadAsync(
                It.Is<UserId>(u => u.Value == userId),
                It.IsAny<Stream>(),
                fileName,
                contentType,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UploadFile_WithEmptyFile_ShouldReturnBadRequest()
    {
        // Arrange
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns("empty.jpg");
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.Length).Returns(0); // Empty file

        var command = new UploadFileCommand
        {
            File = mockFile.Object,
            UserId = userId
        };

        // Act
        var result = await _controller.UploadFile(command, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
        Assert.Equal("File is empty", badRequestResult.Value);

        // Verify upload service was NOT called
        _fileUploadServiceMock.Verify(
            s => s.UploadAsync(
                It.IsAny<UserId>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadFile_WithNullFile_ShouldReturnBadRequest()
    {
        // Arrange
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        var command = new UploadFileCommand
        {
            File = null, // Null file
            UserId = userId
        };

        // Act
        var result = await _controller.UploadFile(command, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequestResult.Value);
        Assert.Equal("File is empty", badRequestResult.Value);

        // Verify upload service was NOT called
        _fileUploadServiceMock.Verify(
            s => s.UploadAsync(
                It.IsAny<UserId>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadFile_WhenDuplicate_ShouldReturnOkWithDuplicateMessage()
    {
        // Arrange
        var userId = Guid.Parse("123e4567-e89b-12d3-a456-426614174000");
        var fileName = "duplicate-photo.jpg";
        var contentType = "image/jpeg";
        var fileContent = new byte[] { 1, 2, 3, 4, 5 };
        var existingFileId = FileId.New();

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns(contentType);
        mockFile.Setup(f => f.Length).Returns(fileContent.Length);
        mockFile.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(fileContent));

        var command = new UploadFileCommand
        {
            File = mockFile.Object,
            UserId = userId
        };

        // Upload result indicating duplicate
        var uploadResult = UploadFileResult.DuplicateResult(existingFileId);

        _fileUploadServiceMock
            .Setup(s => s.UploadAsync(
                It.IsAny<UserId>(),
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(uploadResult);

        // Act
        var result = await _controller.UploadFile(command, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        // Verify the response contains duplicate message and file ID
        var responseValue = okResult.Value.ToString();
        Assert.NotNull(responseValue);
        Assert.Contains("File already exists", responseValue);
        Assert.Contains(existingFileId.Value.ToString(), responseValue);
    }
}
