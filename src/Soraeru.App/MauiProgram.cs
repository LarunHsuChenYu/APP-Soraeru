using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Soraeru.ClientLogic.Notebook;
using Soraeru.Pages;
using Soraeru.Services.Api;
using Soraeru.Services.Interfaces;
using Soraeru.Services.Local;
using TesseractOcrMaui;

namespace Soraeru;

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
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                // Tab reselect on Android does not clear the Shell stack; see SoraeruShellRenderer.
                handlers.AddHandler<Shell, Platforms.Android.SoraeruShellRenderer>();
#endif
            });

        MapAuthInputContrast();

        builder.Services.AddTesseractOcr(files =>
        {
            foreach (var trainedData in TessdataCatalog.AllTrainedDataFiles)
                files.AddFile(trainedData);
        });

        builder.Services.AddSingleton<IAuthSessionStore, PreferencesAuthSessionStore>();
        builder.Services.AddSingleton<IAnalyzeFlowStore, AnalyzeFlowStore>();
        builder.Services.AddSingleton<IOcrSessionStore, OcrSessionStore>();
        builder.Services.AddSingleton<IImageCaptureService, MauiImageCaptureService>();
#if ANDROID
        builder.Services.AddSingleton<IOnDeviceMlKitOcr, Platforms.Android.AndroidMlKitMultiScriptOcr>();
#else
        builder.Services.AddSingleton<IOnDeviceMlKitOcr, UnsupportedOnDeviceMlKitOcr>();
#endif
        builder.Services.AddSingleton<IDeviceOcrService, HybridDeviceOcrService>();
#if ANDROID
        builder.Services.AddSingleton<IOcrImagePreprocessor, Platforms.Android.AndroidOcrImagePreprocessor>();
#else
        builder.Services.AddSingleton<IOcrImagePreprocessor, PassthroughOcrImagePreprocessor>();
#endif
        builder.Services.AddSingleton<ITessdataPackStore>(_ =>
            new TessdataPackStore(
                new HttpClient { Timeout = TimeSpan.FromMinutes(5) },
                Path.Combine(FileSystem.AppDataDirectory, "tessdata-cache")));
        builder.Services.AddSingleton<IFormalTtsService, MauiFormalTtsService>();
        builder.Services.AddSingleton<ILocalWordCardStore>(_ =>
        {
            var appData = FileSystem.AppDataDirectory;
            return new SqliteLocalWordCardStore(
                Path.Combine(appData, "local-wordcards.db"),
                legacyJsonPath: Path.Combine(appData, "local-wordcards.json"));
        });
        builder.Services.AddSingleton<NotebookListRefreshGate>();
        builder.Services.AddSingleton<LocalNotebookService>(sp =>
        {
            var store = sp.GetRequiredService<ILocalWordCardStore>();
            var sessionStore = sp.GetRequiredService<IAuthSessionStore>();
            return new LocalNotebookService(store, async ct =>
            {
                if (!await sessionStore.HasSessionAsync())
                    return LocalSession.Anonymous();

                var userId = await sessionStore.GetUserIdAsync();
                return userId is { } id && id != Guid.Empty
                    ? LocalSession.SignedIn(id)
                    : LocalSession.Anonymous();
            });
        });
#if ANDROID
        builder.Services.AddSingleton<IGoogleSignInService, Platforms.Android.AndroidGoogleSignInService>();
#else
        builder.Services.AddSingleton<IGoogleSignInService, UnsupportedGoogleSignInService>();
#endif
        builder.Services.AddTransient<AuthHeaderHandler>();
        builder.Services.AddHttpClient<ISoraeruApiClient, SoraeruApiClient>(client =>
            {
                // Closed-testing / Release APK → public HTTPS (Railway).
                // Local emulator: Debug Android uses 10.0.2.2; Windows uses localhost.
                // To force cloud API in Debug: define USE_RAILWAY_API (or temporarily point BaseAddress).
                // To force local in Release: define USE_LOCAL_API.
                client.BaseAddress = new Uri(ResolveApiBaseUrl());
                client.Timeout = TimeSpan.FromSeconds(90);
            })
            .AddHttpMessageHandler<AuthHeaderHandler>();
        builder.Services.AddSingleton<ICloudWordCardMirror, HttpCloudWordCardMirror>();
        builder.Services.AddSingleton<NotebookSyncCoordinator>(sp =>
        {
            var store = sp.GetRequiredService<ILocalWordCardStore>();
            var mirror = sp.GetRequiredService<ICloudWordCardMirror>();
            var sessionStore = sp.GetRequiredService<IAuthSessionStore>();
            return new NotebookSyncCoordinator(store, mirror, async ct =>
            {
                if (!await sessionStore.HasSessionAsync())
                    return LocalSession.Anonymous();

                var userId = await sessionStore.GetUserIdAsync();
                return userId is { } id && id != Guid.Empty
                    ? LocalSession.SignedIn(id)
                    : LocalSession.Anonymous();
            });
        });

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<OnboardingPage>();
        builder.Services.AddTransient<LegalDocumentPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<WordInputPage>();
        builder.Services.AddTransient<ImagePickPage>();
        builder.Services.AddTransient<OcrSelectPage>();
        builder.Services.AddTransient<AnalyzingPage>();
        builder.Services.AddTransient<AnalysisResultPage>();
        builder.Services.AddTransient<NotebookListPage>();
        builder.Services.AddTransient<NotebookDetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

    /// <summary>
    /// API BaseAddress for HttpClient. Trailing slash required (relative paths append after it).
    /// </summary>
    internal static string ResolveApiBaseUrl()
    {
#if USE_LOCAL_API
        return LocalApiBaseUrl();
#elif USE_RAILWAY_API
        return ProductionApiBaseUrl;
#elif WINDOWS
        return "http://localhost:5080/";
#elif DEBUG
        // Android emulator → host loopback. Physical device Debug: define USE_RAILWAY_API or use LAN IP.
        return "http://10.0.2.2:5080/";
#else
        // Android Release / closed-testing APK
        return ProductionApiBaseUrl;
#endif
    }

    private const string ProductionApiBaseUrl = "https://soraeru.ash-ben.com/";

    private static string LocalApiBaseUrl() =>
#if WINDOWS
        "http://localhost:5080/";
#else
        "http://10.0.2.2:5080/";
#endif

    /// <summary>
    /// Pages paint light InputBorder fills even when the OS is dark. Autofill overlays can also
    /// force white typed text. Pin native EditText colors to OnSurface / Outline.
    /// </summary>
    static void MapAuthInputContrast()
    {
#if ANDROID
        const int onSurface = unchecked((int)0xFF181C1E);
        const int outline = unchecked((int)0xFF70787E);
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("SoraeruOnSurfaceText", (handler, _) =>
        {
            handler.PlatformView.SetTextColor(new Android.Graphics.Color(onSurface));
            handler.PlatformView.SetHintTextColor(new Android.Graphics.Color(outline));
        });
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("SoraeruOnSurfaceText", (handler, _) =>
        {
            handler.PlatformView.SetTextColor(new Android.Graphics.Color(onSurface));
            handler.PlatformView.SetHintTextColor(new Android.Graphics.Color(outline));
        });
#endif
    }
}
