using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using FastBindings.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace FastBindings.Tests
{
    /// <summary>The headless application every test runs in: the Fluent theme, so lists make real containers.</summary>
    public class TestApp : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }
}
