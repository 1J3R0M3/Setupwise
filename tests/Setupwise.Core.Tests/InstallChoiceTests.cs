using Setupwise.Core.Packages;

namespace Setupwise.Core.Tests;

/// <summary>Per-app "Install for" / installation mode and the retry options after a failure (R-20).</summary>
public class InstallChoiceTests
{
    private static readonly InstallOptions Settings = new() { Mode = InstallMode.Silent, Scope = InstallScope.Auto, Locale = "de-DE" };

    [Fact]
    public void Empty_choice_keeps_the_settings()
    {
        Assert.Same(Settings, new AppInstallChoice().ApplyTo(Settings));
    }

    [Fact]
    public void Choice_overrides_only_what_was_chosen()
    {
        var options = new AppInstallChoice { Scope = InstallScope.Machine }.ApplyTo(Settings);

        Assert.Equal(InstallScope.Machine, options.Scope);
        Assert.Equal(InstallMode.Silent, options.Mode);
        Assert.Equal("de-DE", options.Locale);

        options = new AppInstallChoice { Mode = InstallMode.Interactive }.ApplyTo(Settings);
        Assert.Equal(InstallScope.Auto, options.Scope);
        Assert.Equal(InstallMode.Interactive, options.Mode);
    }

    [Theory]
    [InlineData(RetryVariant.Plain, false, false, InstallMode.Silent)]
    [InlineData(RetryVariant.Force, true, false, InstallMode.Silent)]
    [InlineData(RetryVariant.SkipDependencies, false, true, InstallMode.Silent)]
    [InlineData(RetryVariant.Interactive, false, false, InstallMode.Interactive)]
    public void Retry_adds_exactly_one_option(RetryVariant variant, bool force, bool skipDependencies, InstallMode mode)
    {
        var options = RetryVariants.Apply(Settings, variant);

        Assert.Equal(force, options.Force);
        Assert.Equal(skipDependencies, options.SkipDependencies);
        Assert.Equal(mode, options.Mode);
        Assert.Equal(Settings.Scope, options.Scope);
    }

    [Fact]
    public void Retry_keeps_the_choice_of_the_app()
    {
        var choice = new AppInstallChoice { Scope = InstallScope.User };
        var options = RetryVariants.Apply(choice.ApplyTo(Settings), RetryVariant.Force);

        Assert.Equal(InstallScope.User, options.Scope);
        Assert.True(options.Force);
    }
}
