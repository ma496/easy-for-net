namespace Backend.Features.FileManagement.Core;

/// <summary>
/// Verdict of resolving one stored file's attribution against the caller and the tenant they are
/// acting in. Every refusal is a distinct value rather than a bare "no", because the caller has to
/// answer a missing file differently from one that exists but belongs to another tenant, and both
/// differently from one whose tenant is no longer being served.
/// </summary>
public enum FileAccessStatus
{
    /// <summary>
    /// The file is attributed to the tenant the caller is acting in, or is owned by the calling
    /// account, and may be served or removed.
    /// </summary>
    Granted,

    /// <summary>
    /// No stored file is recorded under that name, so there is nothing to serve and nothing to say
    /// about who it might have belonged to.
    /// </summary>
    NotFound,

    /// <summary>
    /// The file exists but is attributed to another tenant, or is owned by another account. This is
    /// what a known or guessed stored file name yields instead of another tenant's content.
    /// </summary>
    CrossTenantAccess,

    /// <summary>
    /// The file is attributed to a tenant that is suspended. Its content is retained and simply
    /// stops being served until the tenant is reactivated.
    /// </summary>
    TenantSuspended,

    /// <summary>
    /// The file is attributed to a tenant that no longer exists or has been deleted. As with
    /// suspension, the content is retained and no longer served.
    /// </summary>
    TenantNotFound
}
