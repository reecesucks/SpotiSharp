using System.Globalization;
using SpotiSharpBackend;
using SpotiSharpBackend.AudioBookshelf;

namespace SpotiSharp.Models;

/// <summary>Keeps a binged AudioBookshelf show's next few episodes downloading/processing on the server
/// ahead of time, so the listener never hits a gap. Entirely a client-side orchestration of AudioBookshelf's
/// own native endpoints - no new server-side code. Reuses <see cref="RadioAudiobookshelfConfigModel"/>'s
/// existing <c>BingeShows</c> dictionary; only meaningful while the "podcast source" switch is set to
/// AudioBookshelf, since otherwise those ids are Spotify show ids.
///
/// Critically, the library item's own episode list (GET /api/items/:id?expanded=1 -> media.episodes) only
/// ever holds episodes AudioBookshelf has already downloaded - it never contains anything still sitting
/// unfetched in the feed, so it can never reveal what's "missing ahead". Finding that requires re-reading
/// the show's own RSS feed (POST /api/podcasts/feed) for the full back-catalog and cross-matching it
/// against the downloaded list (by guid/enclosure URL, same as AudioBookshelf itself does) to see which
/// feed episodes are not yet on the server.</summary>
internal static class RadioAudiobookshelfBingeAheadModel
{
    private const int EPISODES_AHEAD = 4;

    internal static async Task EnsureAheadAsync()
    {
        // Not gated on the Radio page's "podcast source" switch - a show can be pinned as binged
        // from the standalone AudioBookshelf Podcasts page's long-press regardless of what Radio is
        // currently sourcing podcasts from, so BingeShows can hold AudioBookshelf ids either way.
        if (!AudiobookshelfApiClient.IsConfigured) return;

        var showIds = RadioAudiobookshelfConfigModel.GetAllBingeShowIds();
        if (showIds.Count == 0) return;

        foreach (var showId in showIds)
        {
            try
            {
                await EnsureAheadForShowAsync(showId);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"[Radio] binge-ahead check failed for {showId}: {ex.Message}");
            }
        }
    }

    private static async Task EnsureAheadForShowAsync(string libraryItemId)
    {
        var (ordered, currentIndex) = await FindBingePositionAsync(libraryItemId);
        if (currentIndex < 0) return;

        var missing = ordered
            .Skip(currentIndex)
            .Take(EPISODES_AHEAD)
            .Where(episode => !episode.IsDownloaded)
            .ToList();
        if (missing.Count == 0) return;

        var titles = string.Join(", ", missing.Select(episode => $"#{episode.EpisodeNumber ?? "?"} \"{episode.Title}\""));
        DiagnosticLog.Write($"[Radio] binge-ahead: queuing {missing.Count} episode(s) for {libraryItemId}: {titles}");
        await AudiobookshelfApiClient.DownloadEpisodesAsync(libraryItemId, missing.Select(episode => episode.FeedEpisode!).ToList());
    }

    /// <summary>Builds a <see cref="BingeProgress"/> for pinning an AudioBookshelf show as binged from the
    /// Radio settings page - there's no "current playback" to read a position from the way the Spotify
    /// pin does (<see cref="BingeProgressModel.CreateFromCurrentPlayback"/>), so this uses the same
    /// oldest-unfinished-episode rule <see cref="EnsureAheadForShowAsync"/> itself runs on every check,
    /// purely for the status label - the ahead-download always recomputes this fresh, it never reads
    /// the stored progress back.</summary>
    internal static async Task<BingeProgress> CreateBingeAsync(string libraryItemId)
    {
        var (ordered, currentIndex) = await FindBingePositionAsync(libraryItemId);
        if (currentIndex < 0) return null;

        return new BingeProgress
        {
            LastFinishedIndexFromOldest = currentIndex - 1,
            NextEpisodeName = ordered[currentIndex].Title,
            LastActivityUtc = DateTime.UtcNow
        };
    }

    /// <summary>Builds a <see cref="BingeProgress"/> anchored to a specific episode the listener picked
    /// directly (the AudioBookshelf Podcasts page's "Binge from here" long-press) - no network round
    /// trip needed since the episode is already in hand, and no guessing from progress data either:
    /// <see cref="FindBingePositionAsync"/> uses this anchor verbatim once it's pinned. The anchor is
    /// always a downloaded episode, since that's the only kind the Podcasts page can show rows for.</summary>
    internal static BingeProgress CreateBingeFromEpisode(AudiobookshelfEpisode episode)
    {
        return new BingeProgress
        {
            LastTrackedEpisodeId = episode.EpisodeId,
            NextEpisodeName = episode.EpisodeName,
            LastActivityUtc = DateTime.UtcNow
        };
    }

    /// <summary>One feed episode, cross-matched against the library item's downloaded episode list so
    /// ordering and "is this one already on the server" can both be answered from a single list that -
    /// unlike the library item's own episode list - actually spans the whole back-catalog.</summary>
    private class CatalogEpisode
    {
        public string? LibraryEpisodeId;
        public bool IsDownloaded;
        public string Title = string.Empty;
        public string? PubDate;
        public long? PublishedAtMs;
        public string? EpisodeNumber;
        public AudiobookshelfFeedEpisodeDto? FeedEpisode;
    }

    private static async Task<List<CatalogEpisode>> GetFullCatalogAsync(string libraryItemId)
    {
        var item = await AudiobookshelfApiClient.GetLibraryItemAsync(libraryItemId);
        var downloaded = item?.Media?.Episodes ?? new List<AudiobookshelfEpisodeDto>();
        var feedUrl = item?.Media?.Metadata?.FeedUrl;

        if (string.IsNullOrWhiteSpace(feedUrl))
        {
            // No feed URL on record - fall back to the downloaded-only list, so an anchor pin still
            // works (just with no "ahead" episodes ever found beyond what's already on the server).
            DiagnosticLog.Write($"[Radio] binge-ahead: no feed URL for {libraryItemId}, falling back to downloaded-only episode list");
            return downloaded.Select(episode => new CatalogEpisode
            {
                LibraryEpisodeId = episode.Id,
                IsDownloaded = episode.AudioFile != null,
                Title = episode.Title,
                PubDate = episode.PubDate,
                PublishedAtMs = episode.PublishedAtMs,
                EpisodeNumber = episode.Episode
            }).ToList();
        }

        var feedEpisodes = await AudiobookshelfApiClient.GetPodcastFeedEpisodesAsync(feedUrl);
        return feedEpisodes.Select(feedEpisode =>
        {
            var match = downloaded.FirstOrDefault(d => MatchesFeedEpisode(d, feedEpisode));
            return new CatalogEpisode
            {
                LibraryEpisodeId = match?.Id,
                IsDownloaded = match != null,
                Title = feedEpisode.Title,
                PubDate = feedEpisode.PubDate,
                PublishedAtMs = feedEpisode.PublishedAtMs,
                EpisodeNumber = feedEpisode.Episode,
                FeedEpisode = feedEpisode
            };
        }).ToList();
    }

    /// <summary>Same rule AudioBookshelf itself uses to recognize a feed episode it already has
    /// (server/models/PodcastEpisode.js's checkMatchesGuidOrEnclosureUrl): a guid match, or failing
    /// that an enclosure URL match.</summary>
    private static bool MatchesFeedEpisode(AudiobookshelfEpisodeDto downloaded, AudiobookshelfFeedEpisodeDto feedEpisode)
    {
        if (!string.IsNullOrEmpty(downloaded.Guid) && !string.IsNullOrEmpty(feedEpisode.Guid))
            return downloaded.Guid == feedEpisode.Guid;

        return !string.IsNullOrEmpty(downloaded.Enclosure?.Url) && downloaded.Enclosure?.Url == feedEpisode.Enclosure?.Url;
    }

    private static async Task<(List<CatalogEpisode> ordered, int currentIndex)> FindBingePositionAsync(string libraryItemId)
    {
        var catalog = await GetFullCatalogAsync(libraryItemId);
        var ordered = OrderOldestFirst(catalog);
        if (ordered.Count == 0) return (ordered, -1);

        // An explicit pick ("Binge from here") always wins - it's a direct statement of where the
        // listener is, not a guess from progress data.
        var anchorEpisodeId = RadioAudiobookshelfConfigModel.GetBinge(libraryItemId)?.LastTrackedEpisodeId;
        if (!string.IsNullOrEmpty(anchorEpisodeId))
        {
            int anchorIndex = ordered.FindIndex(episode => episode.LibraryEpisodeId == anchorEpisodeId);
            if (anchorIndex >= 0)
            {
                DiagnosticLog.Write($"[Radio] binge position for {libraryItemId}: pinned anchor index {anchorIndex} of {ordered.Count} (\"{ordered[anchorIndex].Title}\")");
                return (ordered, anchorIndex);
            }
        }

        var progressByEpisodeId = (await AudiobookshelfApiClient.GetMediaProgressAsync())
            .Where(progress => progress.LibraryItemId == libraryItemId)
            .ToDictionary(progress => progress.EpisodeId ?? string.Empty);

        // Fallback: the oldest episode not yet finished. If everything known is finished,
        // there's nothing ahead to stay ahead of (yet) - a new episode arriving is handled by the
        // normal episode-refresh path, not this check.
        int currentIndex = ordered.FindIndex(episode =>
            episode.LibraryEpisodeId == null ||
            !progressByEpisodeId.TryGetValue(episode.LibraryEpisodeId, out var progress) || !progress.IsFinished);

        DiagnosticLog.Write($"[Radio] binge position for {libraryItemId}: index {currentIndex} of {ordered.Count}" +
            (currentIndex >= 0 ? $" (\"{ordered[currentIndex].Title}\", episode #{ordered[currentIndex].EpisodeNumber ?? "?"}, publishedAt {ordered[currentIndex].PublishedAtMs?.ToString() ?? "null"})" : ""));

        return (ordered, currentIndex);
    }

    /// <summary>AudioBookshelf has no reliable per-episode library index (its own <c>index</c> field is
    /// left null when episodes are imported from the feed), so this picks the best available signal:
    /// the server's own already-parsed publish date (<c>publishedAt</c>) when every episode has one -
    /// more reliable than re-parsing the raw <c>pubDate</c> string ourselves. Numbered episodes (the
    /// normal case for a serial show someone is bingeing through in order) are the next-best signal,
    /// explicit and feed-provided. Raw <c>pubDate</c> string parsing is the last resort.</summary>
    private static List<CatalogEpisode> OrderOldestFirst(List<CatalogEpisode> episodes)
    {
        if (episodes.Count > 0 && episodes.All(episode => episode.PublishedAtMs.HasValue))
            return episodes.OrderBy(episode => episode.PublishedAtMs!.Value).ToList();

        var numbered = episodes
            .Select(episode => (episode, number: ParseEpisodeNumber(episode)))
            .ToList();

        if (numbered.Count > 0 && numbered.All(entry => entry.number.HasValue))
            return numbered.OrderBy(entry => entry.number.Value).Select(entry => entry.episode).ToList();

        return episodes.OrderBy(ParsePubDate).ToList();
    }

    private static double? ParseEpisodeNumber(CatalogEpisode episode)
    {
        return double.TryParse(episode.EpisodeNumber, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static DateTime ParsePubDate(CatalogEpisode episode)
    {
        return DateTime.TryParse(episode.PubDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTime.MinValue;
    }
}
