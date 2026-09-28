namespace Backend.Tests.FeatureManagement;

/// <summary>
/// Pins what each feature value type will accept. The validator is the only thing standing between a
/// text column and a value nothing can read, so every branch of it is worth a case.
/// </summary>
public class FeatureValueTypeTests
{
    [Fact]
    public void Toggle_Accepts_Only_The_Two_Spellings_It_Stores()
    {
        var valueType = new ToggleValueType();

        valueType.Name.Should().Be("Toggle");
        valueType.IsValid("true").Should().BeTrue();
        valueType.IsValid("false").Should().BeTrue();
        valueType.IsValid("TRUE").Should().BeTrue("a value typed in another case is still readable");
        valueType.IsValid("yes").Should().BeFalse();
        valueType.IsValid("1").Should().BeFalse();
        valueType.IsValid(null).Should().BeTrue("clearing an override is always allowed");
    }

    [Fact]
    public void Free_Text_Without_A_Validator_Accepts_Anything()
    {
        var valueType = new FreeTextValueType();

        valueType.Name.Should().Be("FreeText");
        valueType.IsValid("whatever").Should().BeTrue();
        valueType.IsValid(string.Empty).Should().BeTrue();
    }

    [Fact]
    public void Numeric_Validator_Honours_Its_Bounds()
    {
        var valueType = new FreeTextValueType(new NumericValidator(1, 100));

        valueType.IsValid("1").Should().BeTrue();
        valueType.IsValid("100").Should().BeTrue();
        valueType.IsValid("0").Should().BeFalse();
        valueType.IsValid("101").Should().BeFalse();
        valueType.IsValid("ten").Should().BeFalse();
        valueType.IsValid(null).Should().BeTrue();
    }

    [Fact]
    public void Numeric_Validator_Publishes_Its_Bounds_To_The_Editor()
    {
        var validator = new NumericValidator(1, 100);

        validator.Name.Should().Be("Numeric");
        validator.Properties.Should().Contain("minimum", "1").And.Contain("maximum", "100");
    }

    [Fact]
    public void String_Length_Validator_Honours_Length_And_Pattern()
    {
        var valueType = new FreeTextValueType(new StringLengthValidator(5, "^[a-z]+$"));

        valueType.IsValid("abc").Should().BeTrue();
        valueType.IsValid("abcdef").Should().BeFalse("it is longer than the maximum");
        valueType.IsValid("ABC").Should().BeFalse("it does not match the pattern");
    }

    [Fact]
    public void Selection_Accepts_Only_The_Values_On_Offer()
    {
        var valueType = new SelectionValueType(new SelectionItem("basic", "Basic"),
                                               new SelectionItem("premium", "Premium"));

        valueType.Name.Should().Be("Selection");
        valueType.Items.Should().HaveCount(2);
        valueType.IsValid("basic").Should().BeTrue();
        valueType.IsValid("premium").Should().BeTrue();
        valueType.IsValid("gold").Should().BeFalse();
        valueType.IsValid(null).Should().BeTrue();
    }
}
