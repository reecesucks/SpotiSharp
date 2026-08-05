using SpotiSharpBackend;

namespace SpotiSharp.Models;
internal static class RadioContentPrefetcher
{
    private static bool _started;

    internal static void Initialize()
    {
        Authentication.OnAuthenticate += () => _ = RunOnceAsync();
    }

    private static async Task RunOnceAsync()
    {
        if (_started) return;
        _started = true;

        await Task.Run(() =>
        {
            try
            {
                PlaylistListModel.RefreshPlayLists();
                PlaylistListModel.RefreshSavedShows();

                foreach (var playlistId in RadioModel.SourcePlaylistIds())
                    RotationTracksModel.RefreshTracks(playlistId);

                RecentEpisodesModel.RefreshRecentEpisodesAcrossAllShows();

                DiagnosticLog.Write("[Prefetch] radio content warmed");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"[Prefetch] warmup failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }
}
