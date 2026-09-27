namespace PasswordManager.Maui.Services;

public static class AppSettings
{
    private const string ApiBaseUrlKey = "api_base_url";
    private const string ProductionBaseUrl = "https://passwordmanager.geekinfo.org";

    // Release builds (what ends up in /deploy) point at the real server by default; Debug builds
    // (local dev/testing) point at the developer's own machine instead.
#if DEBUG
    // 10.0.2.2 is the Android emulator's alias for the host machine's localhost.
    private static string DefaultBaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:8080" : "http://localhost:8080";
#else
    private static string DefaultBaseUrl => ProductionBaseUrl;
#endif

    public static string ApiBaseUrl
    {
        get => Preferences.Get(ApiBaseUrlKey, DefaultBaseUrl);
        set => Preferences.Set(ApiBaseUrlKey, value);
    }
}
