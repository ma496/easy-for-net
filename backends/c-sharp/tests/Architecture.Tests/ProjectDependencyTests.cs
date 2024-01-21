using FluentAssertions;
using NetArchTest.Rules;

namespace EasyForNet.Architecture.Tests;

public class ProjectDependencyTests
{
    [Test]
    public void Domain_Should_Not_HaveDependencyOnOtherProjects()
    {
        // Arrange
        var assembly = typeof(Domain.Common.BaseEntity).Assembly;
        var otherProjects = new[]
        {
            Projects.Application,
            Projects.Infrastructure,
            Projects.Host,
        };

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAll(otherProjects)
            .GetResult();

        // Assert
        testResult.IsSuccessful.Should().BeTrue();
    }

    [Test]
    public void Application_Should_Not_HaveDependencyOnOtherProjects()
    {
        // Arrange
        var assembly = typeof(Application.ConfigureServices).Assembly;
        var otherProjects = new[]
        {
            Projects.Infrastructure,
            Projects.Host,
        };

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAll(otherProjects)
            .GetResult();

        // Assert
        testResult.IsSuccessful.Should().BeTrue();
    }

    [Test]
    public void Infrastructure_Should_Not_HaveDependencyOnOtherProjects()
    {
        // Arrange
        var assembly = typeof(Infrastructure.ConfigureServices).Assembly;
        var otherProjects = new[]
        {
            Projects.Domain,
            Projects.Host,
        };

        // Act
        var testResult = Types
            .InAssembly(assembly)
            .ShouldNot()
            .HaveDependencyOnAll(otherProjects)
            .GetResult();

        // Assert
        testResult.IsSuccessful.Should().BeTrue();
    }

    //[Test]
    //public void Host_Should_Not_HaveDependencyOnOtherProjects()
    //{
    //    // Arrange
    //    var assembly = typeof(Host.ConfigureServices).Assembly;
    //    var otherProjects = new[]
    //    {
    //        Projects.Domain,
    //    };

    //    // Act
    //    var testResult = Types
    //        .InAssembly(assembly)
    //        .ShouldNot()
    //        .HaveDependencyOnAll(otherProjects)
    //        .GetResult();

    //    // Assert
    //    testResult.IsSuccessful.Should().BeTrue();
    //}
}
