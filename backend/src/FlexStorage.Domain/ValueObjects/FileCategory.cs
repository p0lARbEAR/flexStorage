namespace FlexStorage.Domain.ValueObjects;

/// <summary>
/// Represents the category of a file.
/// </summary>
public enum FileCategory
{
    /// <summary>
    /// Photo/Image files (JPEG, PNG, HEIC, etc.)
    /// </summary>
    Photo,

    /// <summary>
    /// Video files (MP4, MOV, AVI, etc.)
    /// </summary>
    Video,

    /// <summary>
    /// Miscellaneous files (PDF, ZIP, documents, etc.)
    /// </summary>
    Misc
}
