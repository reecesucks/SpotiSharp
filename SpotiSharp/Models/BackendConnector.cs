using SpotiSharpBackend;

namespace SpotiSharp.Models;

public class BackendConnector
{
    private static BackendConnector _backendConnector;
    
    public static BackendConnector Instance => _backendConnector ??= new BackendConnector();
    
    private BackendConnector()
    {
        MauiConnector.OnOpenBrowser += OpenBrowser;
        StorageHandler.OnDataChange += StoreInSecureStorage;

        _ = InitializeStorageAsync();
    }

    private static async void OpenBrowser(Uri uri)
    {
        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }

    private static void StoreInSecureStorage(string key, string value)
    {
        SecureStorage.Default.SetAsync(key, value);
    }

    private async Task InitializeStorageAsync()
    {
        StorageHandler.ClientId = await SecureStorage.Default.GetAsync("clientId");
        StorageHandler.RefreshToken = await SecureStorage.Default.GetAsync("refreshToken");
    }
}