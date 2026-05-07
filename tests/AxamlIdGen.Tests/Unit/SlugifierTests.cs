using AxamlIdGen.Synthesis;
using Xunit;

namespace AxamlIdGen.Tests.Unit;

public class SlugifierTests
{
    [Theory]
    [InlineData("Sign in", "SignIn")]
    [InlineData("Submit Order!", "SubmitOrder")]
    [InlineData("dark mode", "DarkMode")]
    [InlineData("Email", "Email")]
    [InlineData("Über uns", "UberUns")]
    [InlineData("Save & exit", "SaveExit")]
    public void Slugifies_to_PascalCase(string input, string expected)
    {
        Assert.Equal(expected, Slugifier.Slugify(input));
    }

    [Fact]
    public void Empty_or_whitespace_returns_null()
    {
        Assert.Null(Slugifier.Slugify(""));
        Assert.Null(Slugifier.Slugify(null));
        Assert.Null(Slugifier.Slugify("   "));
    }

    [Fact]
    public void Pure_punctuation_returns_null()
    {
        Assert.Null(Slugifier.Slugify("!!!@@@"));
    }

    [Fact]
    public void Long_input_is_truncated_to_max()
    {
        var input = new string('a', 200);
        var slug = Slugifier.Slugify(input, maxLength: 32);
        Assert.NotNull(slug);
        Assert.Equal(32, slug!.Length);
    }

    [Fact]
    public void Leading_digit_gets_underscore_prefix()
    {
        Assert.Equal("_3Items", Slugifier.Slugify("3 items"));
    }
}
