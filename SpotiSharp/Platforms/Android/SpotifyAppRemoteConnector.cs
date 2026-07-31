using Com.Spotify.Android.Appremote.Api;
using Com.Spotify.Protocol.Client;
using Com.Spotify.Protocol.Types;
using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.Platforms.Android;

internal static class SpotifyAppRemoteConnector
{
    private static SpotifyAppRemote _appRemote;
    private static string? _clientId;
    private static string? _redirectUri;
    private static TaskCompletionSource<bool>? _pendingConnect;

    internal static void Connect(string clientId, string redirectUri)
    {
        _clientId = clientId;
        _redirectUri = redirectUri;
        PlaybackCommands.WakeSpotify ??= WakeAsync;

        var connectionParams = new ConnectionParams.Builder(clientId)
            .SetRedirectUri(redirectUri)
            .ShowAuthView(true)
            .Build();

        SpotifyAppRemote.Connect(
            global::Android.App.Application.Context,
            connectionParams,
            new ConnectionListener());
    }

    private static async Task<bool> WakeAsync()
    {
        if (_appRemote?.IsConnected == true) return true;
        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_redirectUri)) return false;

        var tcs = new TaskCompletionSource<bool>();
        _pendingConnect = tcs;
        Connect(_clientId, _redirectUri);

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(8)));
        return completed == tcs.Task && tcs.Task.Result;
    }

    private static void OnConnected(SpotifyAppRemote appRemote)
    {
        _appRemote = appRemote;
        DiagnosticLog.Write("[AppRemote] connected");

        PlaybackStateStore.HasActivePushSource = () => _appRemote?.IsConnected == true;
        PlaybackCommands.Pause = () => Pause();
        PlaybackCommands.Resume = () => Resume();
        PlaybackCommands.SkipNext = () => SkipNext();
        PlaybackCommands.SkipPrevious = () => SkipPrevious();
        PlaybackCommands.SetShuffle = shuffle => SetShuffle(shuffle);
        PlaybackCommands.ToggleRepeat = () => ToggleRepeat();
        PlaybackCommands.SeekTo = positionMs => SeekTo(positionMs);

        _appRemote.PlayerApi.SubscribeToPlayerState().SetEventCallback(new PlayerStateCallback());

        _pendingConnect?.TrySetResult(true);
        _pendingConnect = null;
    }

    private static void Pause(bool isRetry = false) =>
        _appRemote?.PlayerApi.Pause()?.SetErrorCallback(new CommandErrorCallback("pause", isRetry ? null : () => Pause(true)));

    private static void Resume(bool isRetry = false) =>
        _appRemote?.PlayerApi.Resume()?.SetErrorCallback(new CommandErrorCallback("resume", isRetry ? null : () => Resume(true)));

    private static void SkipNext(bool isRetry = false) =>
        _appRemote?.PlayerApi.SkipNext()?.SetErrorCallback(new CommandErrorCallback("skip next", isRetry ? null : () => SkipNext(true)));

    private static void SkipPrevious(bool isRetry = false) =>
        _appRemote?.PlayerApi.SkipPrevious()?.SetErrorCallback(new CommandErrorCallback("skip previous", isRetry ? null : () => SkipPrevious(true)));

    private static void SetShuffle(bool shuffle, bool isRetry = false) =>
        _appRemote?.PlayerApi.SetShuffle(shuffle)?.SetErrorCallback(new CommandErrorCallback("set shuffle", isRetry ? null : () => SetShuffle(shuffle, true)));

    private static void ToggleRepeat(bool isRetry = false) =>
        _appRemote?.PlayerApi.ToggleRepeat()?.SetErrorCallback(new CommandErrorCallback("toggle repeat", isRetry ? null : () => ToggleRepeat(true)));

    private static void SeekTo(int positionMs, bool isRetry = false) =>
        _appRemote?.PlayerApi.SeekTo(positionMs)?.SetErrorCallback(new CommandErrorCallback("seek", isRetry ? null : () => SeekTo(positionMs, true)));

    private class ConnectionListener : Java.Lang.Object, IConnector.IConnectionListener
    {
        public void OnConnected(SpotifyAppRemote appRemote) => SpotifyAppRemoteConnector.OnConnected(appRemote);

        public void OnFailure(Java.Lang.Throwable? error)
        {
            DiagnosticLog.Write($"[AppRemote] connect failed: {error?.Message}");
            _pendingConnect?.TrySetResult(false);
            _pendingConnect = null;
        }
    }

    private class CommandErrorCallback : Java.Lang.Object, IErrorCallback
    {
        private readonly string _label;
        private readonly Action? _retry;

        public CommandErrorCallback(string label, Action? retry = null)
        {
            _label = label;
            _retry = retry;
        }

        public void OnError(Java.Lang.Throwable? error)
        {
            DiagnosticLog.Write($"[AppRemote] {_label} failed: {error?.Class?.Name}: {error?.Message}");
            if (_retry == null) return;

            DiagnosticLog.Write($"[AppRemote] retrying {_label}");
            _ = Task.Delay(400).ContinueWith(_ => _retry());
        }
    }

    private class PlayerStateCallback : Java.Lang.Object, Subscription.IEventCallback
    {
        public void OnEvent(Java.Lang.Object? data)
        {
            if (data is not PlayerState state) return;

            if (state.Track == null) return;

            DiagnosticLog.Write(
                $"[AppRemote] state: uri={state.Track?.Uri} isEpisode={state.Track?.IsEpisode} " +
                $"isPaused={state.IsPaused} position={state.PlaybackPosition} duration={state.Track?.Duration}");

            PlaybackStateStore.Instance.Update(
                isPlaying: !state.IsPaused,
                activeDeviceId: PlaybackStateStore.Instance.ActiveDeviceId,
                currentItemUri: state.Track?.Uri,
                progressMs: (int)state.PlaybackPosition,
                durationMs: (int)(state.Track?.Duration ?? 0),
                shuffleOn: state.PlaybackOptions?.IsShuffling ?? PlaybackStateStore.Instance.ShuffleOn);

            RadioConductor.Instance.Tick();
        }
    }
}
