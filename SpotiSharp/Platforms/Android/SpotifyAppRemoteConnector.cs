using Com.Spotify.Android.Appremote.Api;
using Com.Spotify.Protocol.Client;
using Com.Spotify.Protocol.Types;
using SpotiSharp.Models;
using SpotiSharpBackend;

namespace SpotiSharp.Platforms.Android;

internal static class SpotifyAppRemoteConnector
{
    private static SpotifyAppRemote _appRemote;

    internal static void Connect(string clientId, string redirectUri)
    {
        var connectionParams = new ConnectionParams.Builder(clientId)
            .SetRedirectUri(redirectUri)
            .ShowAuthView(true)
            .Build();

        SpotifyAppRemote.Connect(
            global::Android.App.Application.Context,
            connectionParams,
            new ConnectionListener());
    }

    private static void OnConnected(SpotifyAppRemote appRemote)
    {
        _appRemote = appRemote;
        DiagnosticLog.Write("[AppRemote] connected");

        PlaybackStateStore.HasActivePushSource = () => _appRemote?.IsConnected == true;
        PlaybackCommands.Pause = () => _appRemote?.PlayerApi.Pause()?.SetErrorCallback(new CommandErrorCallback("pause"));
        PlaybackCommands.Resume = () => _appRemote?.PlayerApi.Resume()?.SetErrorCallback(new CommandErrorCallback("resume"));
        PlaybackCommands.SkipNext = () => _appRemote?.PlayerApi.SkipNext()?.SetErrorCallback(new CommandErrorCallback("skip next"));
        PlaybackCommands.SkipPrevious = () => _appRemote?.PlayerApi.SkipPrevious()?.SetErrorCallback(new CommandErrorCallback("skip previous"));
        PlaybackCommands.SetShuffle = shuffle => _appRemote?.PlayerApi.SetShuffle(shuffle)?.SetErrorCallback(new CommandErrorCallback("set shuffle"));
        PlaybackCommands.ToggleRepeat = () => _appRemote?.PlayerApi.ToggleRepeat()?.SetErrorCallback(new CommandErrorCallback("toggle repeat"));

        _appRemote.PlayerApi.SubscribeToPlayerState().SetEventCallback(new PlayerStateCallback());
    }

    private class ConnectionListener : Java.Lang.Object, IConnector.IConnectionListener
    {
        public void OnConnected(SpotifyAppRemote appRemote) => SpotifyAppRemoteConnector.OnConnected(appRemote);

        public void OnFailure(Java.Lang.Throwable? error) =>
            DiagnosticLog.Write($"[AppRemote] connect failed: {error?.Message}");
    }

    private class CommandErrorCallback : Java.Lang.Object, IErrorCallback
    {
        private readonly string _label;
        public CommandErrorCallback(string label) => _label = label;

        public void OnError(Java.Lang.Throwable? error) =>
            DiagnosticLog.Write($"[AppRemote] {_label} failed: {error?.Class?.Name}: {error?.Message}");
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
