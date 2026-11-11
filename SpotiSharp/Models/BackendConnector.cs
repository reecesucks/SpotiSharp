using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class BackendConnector
{
    private static BackendConnector _backendConnector;
    
    public static BackendConnector Instance => _backendConnector ??= new BackendConnector();

    public Task StorageLoadTask { get; }

    private BackendConnector()
    {
        MauiConnector.OnOpenBrowser += OpenBrowser;
        StorageHandler.OnDataChange += StoreInSecureStorage;
        RadioContentPrefetcher.Initialize();

        StorageLoadTask = InitializeStorageAsync();
    }

    private static async void OpenBrowser(Uri uri)
    {
        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }

    private static void StoreInSecureStorage(string key, string value)
    {
        SecureStorage.Default.SetAsync(key, value ?? string.Empty);
    }

    private async Task InitializeStorageAsync()
    {
        CacheManager.MigrateIfNeeded();

        StorageHandler.ClientId = await SecureStorage.Default.GetAsync("clientId") ?? string.Empty;
        if (string.IsNullOrEmpty(StorageHandler.ClientId)) StorageHandler.ClientId = "REDACTED_SPOTIFY_CLIENT_ID_ROTATED"; // paste key here for simplified debugging
        StorageHandler.RefreshToken = await SecureStorage.Default.GetAsync("refreshToken") ?? string.Empty;
        StorageHandler.SelectedDeviceId = await SecureStorage.Default.GetAsync("selectedDeviceId") ?? string.Empty;
        StorageHandler.CachedDevices = await SecureStorage.Default.GetAsync("cachedDevices") ?? string.Empty;
        StorageHandler.AudiobookshelfServerUrl = await SecureStorage.Default.GetAsync("audiobookshelfServerUrl") ?? string.Empty;
        if (string.IsNullOrEmpty(StorageHandler.AudiobookshelfServerUrl)) StorageHandler.AudiobookshelfServerUrl = "https://audio.reecesucks.com"; // paste server URL here for simplified debugging
        StorageHandler.AudiobookshelfApiKey = await SecureStorage.Default.GetAsync("audiobookshelfApiKey") ?? string.Empty;
        if (string.IsNullOrEmpty(StorageHandler.AudiobookshelfApiKey)) StorageHandler.AudiobookshelfApiKey = "REDACTED_AUDIOBOOKSHELF_API_KEY_ROTATED"; // paste API key here for simplified debugging
        StorageHandler.UseAudiobookshelfPodcasts = await SecureStorage.Default.GetAsync("useAudiobookshelfPodcasts") == "1";
    }
}