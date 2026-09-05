using Android.App;
using Android.Content.PM;

namespace Packaged.Maui.Smoke.Platforms.Android;

[Activity(Theme = "@style/Maui.MainTheme.NoActionBar", MainLauncher = true, Exported = true,
    LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
    ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity { }
