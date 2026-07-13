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
        StorageHandler.ClientId = await SecureStorage.Default.GetAsync("clientId") ?? string.Empty;
        if (string.IsNullOrEmpty(StorageHandler.ClientId)) StorageHandler.ClientId = "1"; // paste key here for simplified debugging
        StorageHandler.RefreshToken = await SecureStorage.Default.GetAsync("refreshToken") ?? string.Empty;
    }
}