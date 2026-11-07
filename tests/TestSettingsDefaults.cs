// Compiled into every test project by tests/Directory.Build.props: runs before Main, so each suite (and any XI.Host
// it starts) reads settings/default/*.lua only, never the live server's settings/*.lua overrides.

internal static class TestSettingsDefaults
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init() => System.Environment.SetEnvironmentVariable("XI_SETTINGS_DEFAULTS_ONLY", "1");
}
