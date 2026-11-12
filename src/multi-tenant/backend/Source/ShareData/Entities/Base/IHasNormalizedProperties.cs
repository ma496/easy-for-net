namespace Backend.ShareData.Entities.Base;

/// <summary>
/// Implemented by entities that maintain a normalized 
/// (e.g. UsernameNormalized = Username.Trim().ToLowerInvariant()) version
/// of one or more properties alongside the user-supplied value.
/// </summary>
public interface IHasNormalizedProperties
{
    /// <summary>
    /// Recomputes the normalized property values from their raw counterparts.
    /// (e.g. UsernameNormalized = Username.Trim().ToLowerInvariant())
    /// </summary>
    void NormalizeProperties();
}
