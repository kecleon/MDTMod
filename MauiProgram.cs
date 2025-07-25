using MDTadusMod.Data;
using MDTadusMod.Services;
using Microsoft.Extensions.Logging;

namespace MDTadusMod
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                throw (Exception)e.ExceptionObject;
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                throw e.Exception;
            };

#endif
            builder.Services.AddSingleton<AccountService>();
            builder.Services.AddSingleton<RotmgApiService>();
            builder.Services.AddSingleton<AssetService>();
            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddHttpClient();


            return builder.Build();
        }
    }
}
