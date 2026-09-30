namespace Backend.Features.Settings.Core;

/// <summary>
/// Marks a property of a setting class as a secret - a password, an API key - so it is encrypted at
/// rest and never leaves the API.
/// </summary>
/// <remarks>
/// A secret is stored encrypted with ASP.NET Data Protection and decrypted only in memory;
/// <c>GET /settings</c> and <c>PUT /settings/{name}</c> answer it with no value, only whether one is
/// set, and <see cref="ISettingProvider"/> hands the plaintext to the code that needs it. A secret is a
/// string: the catalogue refuses, at startup, the attribute on a property of any other type.
/// <para>
/// A secret may name the properties that decide where it is sent (<see cref="BoundTo"/>) - a password
/// is bound to the server and user name it authenticates against. A secret is never inherited apart
/// from them: when any bound property is overridden at a layer above the one the secret comes from, the
/// secret resolves to an empty string, so a tenant that points the server somewhere else does not hand
/// that server the platform's password. It has to set its own.
/// </para>
/// </remarks>
[AllowOutside]
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class SecretSettingAttribute(params string[] boundTo) : Attribute
{
    /// <summary>
    /// The C# names of the properties of the same setting class this secret is bound to - use
    /// <c>nameof</c>. The catalogue refuses, at startup, a name the class does not expose as a setting property.
    /// </summary>
    public IReadOnlyList<string> BoundTo { get; } = boundTo;
}
