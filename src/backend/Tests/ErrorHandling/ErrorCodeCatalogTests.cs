namespace Backend.Tests.ErrorHandling;

using System.Reflection;
using Backend.Features.Localization.Core;

/// <summary>
/// Guards the contract every <see cref="ErrorCodes"/> member depends on: that the code is a resource
/// key, not a message. A member with no shipped <c>error.server.&lt;code&gt;</c> text would leave
/// <c>ThrowError</c> falling back to the bare code string itself - the one thing a call site
/// naming only a code is trusting this catalogue never to do.
/// </summary>
public class ErrorCodeCatalogTests(App app) : AppTestsBase(app)
{
    /// <summary>Every field <see cref="ErrorCodes"/> declares, read by reflection.</summary>
    private static IEnumerable<ErrorCode> DeclaredCodes()
        => typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ErrorCode))
            .Select(field => (ErrorCode)field.GetValue(null)!);

    [Fact]
    public void Every_Declared_Code_Has_A_Non_Empty_Shipped_English_Resource()
    {
        var resourceStore = Service<ILocalizationResourceStore>();
        var declared = DeclaredCodes().ToList();

        declared.Should().NotBeEmpty("the catalogue is expected to declare the application's business errors");

        declared.Should().AllSatisfy(code =>
        {
            var key = $"error.server.{code.Value}";
            resourceStore.EnglishResources.Should().ContainKey(key,
                "{0} has no shipped resource, so a caller naming only this code would be told its bare code " +
                "string instead of a message", code.Value);
            resourceStore.EnglishResources[key].Should().NotBeNullOrWhiteSpace(
                "an empty shipped value reads to a caller exactly as no message at all");
            resourceStore.EnglishResources[key].Should().NotBe(code.Value,
                "the shipped text must be an actual sentence, not the code echoed back as its own message");
        });
    }
}
