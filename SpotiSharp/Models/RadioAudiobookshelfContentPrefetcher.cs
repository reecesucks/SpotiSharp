using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

namespace SpotiSharp.Models;
internal static class RadioAudiobookshelfContentPrefetcher
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

                foreach (var playlistId in RadioAudiobookshelfModel.SourcePlaylistIds())
                    RotationTracksModel.GetTracks(playlistId);

                RecentEpisodesModel.RefreshRecentEpisodesAcrossAllShows();

                // Only warmed when the radio is actually set to use it - no point making AudioBookshelf
                // network calls on every app start for a source that's switched off.
                if (StorageHandler.UseAudiobookshelfPodcasts && AudiobookshelfApiClient.IsConfigured)
                {
                    AudiobookshelfPodcastsModel.RefreshEpisodesAsync().GetAwaiter().GetResult();
                    RadioAudiobookshelfBingeAheadModel.EnsureAheadAsync().GetAwaiter().GetResult();
                }

                DiagnosticLog.Write("[Prefetch] radio content warmed");
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"[Prefetch] warmup failed: {ex.GetType().Name}: {ex.Message}");
            }
        });
    }
}
