namespace PasswordManager.Maui.Services;

public static class AppSettings
{
    private const string ApiBaseUrlKey = "api_base_url";

    // 10.0.2.2 is the Android emulator's alias for the host machine's localhost.
    private static string DefaultBaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:8080" : "http://localhost:8080";

    public static string ApiBaseUrl
    {
        get => Preferences.Get(ApiBaseUrlKey, DefaultBaseUrl);
        set => Preferences.Set(ApiBaseUrlKey, value);
    }
}
