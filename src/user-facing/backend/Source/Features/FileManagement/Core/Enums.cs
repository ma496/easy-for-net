namespace Backend.Features.FileManagement.Core;

/// <summary>
/// Verdict of resolving one stored file against the caller. A refusal is a distinct value rather
/// than a bare "no", because the caller answers a missing file differently from one that exists but
/// belongs to another account.
/// </summary>
[AllowOutside]
public enum FileAccessStatus
{
    /// <summary>
    /// The file is an application file, or is owned by the calling account, and may be served or
    /// removed.
    /// </summary>
    Granted,

    /// <summary>
    /// No stored file is recorded under that name.
    /// </summary>
    NotFound,

    /// <summary>
    /// The file exists but is owned by another account. This is what a known or guessed stored file
    /// name of someone else's profile image yields instead of its content.
    /// </summary>
    AccessDenied
}
