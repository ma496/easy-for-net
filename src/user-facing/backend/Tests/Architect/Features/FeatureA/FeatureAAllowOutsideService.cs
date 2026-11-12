using Backend.Attributes;

namespace Backend.Tests.Architect.Features.FeatureA;

// ReSharper disable UnusedVariable


/// <summary>
/// Interface for a service that is allowed to be used outside its feature, decorated with <see cref="AllowOutsideAttribute"/>.
/// </summary>
[AllowOutside]
public interface IFeatureAAllowOutsideService
{
    /// <summary>
    /// Accepts an <see cref="FeatureAOneAllowOutsideModel"/> parameter.
    /// </summary>
    void MethodOne(FeatureAOneAllowOutsideModel allowOutsideModel);
    /// <summary>
    /// Returns an <see cref="FeatureATwoAllowOutsideModel"/> instance.
    /// </summary>
    FeatureATwoAllowOutsideModel MethodTwo();
    /// <summary>
    /// No-op method for testing dependency resolution.
    /// </summary>
    void MethodThree();
}

/// <summary>
/// Implementation of <see cref="IFeatureAAllowOutsideService"/> marked with <see cref="AllowOutsideAttribute"/>.
/// Used to verify that AllowOutside types are not flagged as forbidden dependencies.
/// </summary>
[AllowOutside]
public class FeatureAAllowOutsideService : IFeatureAAllowOutsideService 
{
    /// <summary>
    /// Accepts an <see cref="FeatureAOneAllowOutsideModel"/> parameter.
    /// </summary>
    public void MethodOne(FeatureAOneAllowOutsideModel allowOutsideModel)
    {
    }

    /// <summary>
    /// Returns an <see cref="FeatureATwoAllowOutsideModel"/> instance.
    /// </summary>
    public FeatureATwoAllowOutsideModel MethodTwo()
        => throw new NotImplementedException();

    /// <summary>
    /// Creates a local <see cref="FeatureAOneModel"/> instance inside the method body.
    /// Used to test method body dependency resolution.
    /// </summary>
    public void MethodThree()
    {
        var x = new FeatureAOneModel();
    }
}
