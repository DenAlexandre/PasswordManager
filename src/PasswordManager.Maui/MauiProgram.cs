using Microsoft.Extensions.Logging;
using PasswordManager.Maui.Data;
using PasswordManager.Maui.Services;
using PasswordManager.Maui.ViewModels;
using PasswordManager.Maui.Views;

namespace PasswordManager.Maui;

public static class MauiProgram
{
	private static void LogCrash(string source, Exception? ex)
	{
		try
		{
			var path = Path.Combine(Path.GetTempPath(), "maui_crash.log");
			File.AppendAllText(path, $"[{DateTime.Now:O}] {source}: {ex}\n\n");
		}
		catch { /* best effort */ }
	}

	public static MauiApp CreateMauiApp()
	{
		AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
		TaskScheduler.UnobservedTaskException += (s, e) => { LogCrash("TaskScheduler.UnobservedTaskException", e.Exception); e.SetObserved(); };

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton(sp =>
		{
			var http = new HttpClient();
			var api = new ApiClient(http);
			api.SetBaseAddress(AppSettings.ApiBaseUrl);
			return api;
		});
		builder.Services.AddSingleton<VaultSession>();
		builder.Services.AddSingleton<LocalCacheDb>();
		builder.Services.AddSingleton<AuthService>();
		builder.Services.AddSingleton<SyncService>();

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<VaultUnlockViewModel>();
		builder.Services.AddTransient<VaultUnlockPage>();
		builder.Services.AddTransient<VaultTreeViewModel>();
		builder.Services.AddTransient<VaultTreePage>();
		builder.Services.AddTransient<CredentialsViewModel>();
		builder.Services.AddTransient<CredentialsPage>();
		builder.Services.AddTransient<CredentialEditViewModel>();
		builder.Services.AddTransient<CredentialEditPage>();
		builder.Services.AddTransient<AdminUsersViewModel>();
		builder.Services.AddTransient<AdminUsersPage>();
		builder.Services.AddTransient<AdminSiteGroupsViewModel>();
		builder.Services.AddTransient<AdminSiteGroupsPage>();

		return builder.Build();
	}
}
