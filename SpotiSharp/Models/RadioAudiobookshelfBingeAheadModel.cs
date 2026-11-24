using System.Globalization;
using SpotiSharp.Helpers;
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

    // AudioBookshelf's own "already downloaded" signal for a feed episode (IsDownloaded, matched by
    // guid/enclosure URL against the live-refetched feed - see MatchesFeedEpisode) can't be trusted
    // alone: some feeds re-sign enclosure URLs on every fetch (CDN query params that change each time),
    // which makes an episode that's actually already sitting on the server look "missing" on every
    // single check. Without this, that reads as downloading the same (often newest-ahead-of-position)
    // episodes over and over - exactly the "duplicate downloads" symptom - every time EnsureAheadAsync
    // runs (app start, every binge pin). This remembers what was already requested, independent of
    // whatever the live match says, so a request for the same episode is never repeated within the
    // cooldown below.
    private const string REQUESTED_CACHE_KEY = "audiobookshelf_binge_ahead_requested";
    private static readonly TimeSpan RequestCooldown = TimeSpan.FromHours(6);

    /// <param name="finishedEpisode">The specific episode that was just heard to finish, if this call was
    /// triggered by a finish (rather than app start/pin) - lets the show it belongs to snap its binge
    /// position directly to that episode. Without this, position can only ever crawl forward one step
    /// at a time from whatever it was already tracking, which never catches up to an episode finished
    /// by playing it directly (e.g. from the Podcasts page) rather than through Radio's own binge pick -
    /// exactly what left the tracked position stuck near episode #1 while the listener was actually
    /// finishing episode #419.</param>
    internal static async Task EnsureAheadAsync(string reason = "unspecified", (string libraryItemId, string? guid)? finishedEpisode = null)
    {
        // Not gated on the Radio page's "podcast source" switch - a show can be pinned as binged
        // from the standalone AudioBookshelf Podcasts page's long-press regardless of what Radio is
        // currently sourcing podcasts from, so BingeShows can hold AudioBookshelf ids either way.
        if (!AudiobookshelfApiClient.IsConfigured) return;

        var showIds = RadioAudiobookshelfConfigModel.GetAllBingeShowIds();
        if (showIds.Count == 0) return;

        DiagnosticLog.Write($"[Radio] binge-ahead: EnsureAheadAsync running ({reason}) for {showIds.Count} binged show(s): {string.Join(", ", showIds)}");

        foreach (var showId in showIds)
        {
            try
            {
                var finishedGuid = finishedEpisode?.libraryItemId == showId ? finishedEpisode.Value.guid : null;
                await EnsureAheadForShowAsync(showId, finishedGuid);
            }
            catch (Exception ex)
            {
                DiagnosticLog.Write($"[Radio] binge-ahead check failed for {showId}: {ex.Message}");
            }
        }
    }

    private static async Task EnsureAheadForShowAsync(string libraryItemId, string? finishedEpisodeGuid = null)
    {
        var (ordered, currentIndex) = await FindBingePositionAsync(libraryItemId, finishedEpisodeGuid);
        if (currentIndex < 0) return;

        var window = ordered.Skip(currentIndex).Take(EPISODES_AHEAD).ToList();
        DiagnosticLog.Write($"[Radio] binge-ahead: {libraryItemId} ahead-window from index {currentIndex} is: " +
            string.Join(", ", window.Select(episode => $"#{episode.EpisodeNumber ?? "?"} \"{episode.Title}\" (downloaded={episode.IsDownloaded})")));

        var missing = window.Where(episode => !episode.IsDownloaded).ToList();
        if (missing.Count == 0) return;

        var requested = GetRequestedCache();
        var now = DateTime.UtcNow;
        var toQueue = new List<CatalogEpisode>();
        var skipped = new List<CatalogEpisode>();
        foreach (var episode in missing)
        {
            var key = EpisodeRequestKey(episode);
            if (key != null && requested.TryGetValue(key, out var requestedAtUtc) && now - requestedAtUtc < RequestCooldown)
                skipped.Add(episode);
            else
                toQueue.Add(episode);
        }

        if (skipped.Count > 0)
        {
            var skippedTitles = string.Join(", ", skipped.Select(episode => $"#{episode.EpisodeNumber ?? "?"} \"{episode.Title}\""));
            DiagnosticLog.Write($"[Radio] binge-ahead: {skipped.Count} episode(s) for {libraryItemId} still show as missing but were already requested recently, skipping to avoid a duplicate download: {skippedTitles}");
        }
        if (toQueue.Count == 0) return;

        var titles = string.Join(", ", toQueue.Select(episode => $"#{episode.EpisodeNumber ?? "?"} \"{episode.Title}\""));
        DiagnosticLog.Write($"[Radio] binge-ahead: queuing {toQueue.Count} episode(s) for {libraryItemId}: {titles}");
        await AudiobookshelfApiClient.DownloadEpisodesAsync(libraryItemId, toQueue.Select(episode => episode.FeedEpisode!).ToList());

        foreach (var episode in toQueue)
        {
            var key = EpisodeRequestKey(episode);
            if (key != null) requested[key] = now;
        }
        SaveRequestedCache(requested);
    }

    private static string? EpisodeRequestKey(CatalogEpisode episode) =>
        episode.FeedEpisode?.Guid ?? episode.FeedEpisode?.Enclosure?.Url;

    private static Dictionary<string, DateTime> GetRequestedCache() =>
        DiskCacheHelper.Load<Dictionary<string, DateTime>>(REQUESTED_CACHE_KEY) ?? new Dictionary<string, DateTime>();

    private static void SaveRequestedCache(Dictionary<string, DateTime> requested)
    {
        // Nothing past the cooldown is still useful to remember, and letting this grow forever would
        // mean re-reading (and re-writing) an ever-larger file on every single check.
        var cutoff = DateTime.UtcNow - RequestCooldown;
        var trimmed = requested.Where(entry => entry.Value >= cutoff).ToDictionary(entry => entry.Key, entry => entry.Value);
        DiskCacheHelper.Save(REQUESTED_CACHE_KEY, trimmed);
    }

    /// <summary>Builds a <see cref="BingeProgress"/> for pinning an AudioBookshelf show as binged from the
    /// Radio settings page - there's no "current playback" to read a position from the way the Spotify
    /// pin does (<see cref="BingeProgressModel.CreateFromCurrentPlayback"/>), so this uses the same
    /// oldest-unfinished-episode rule <see cref="FindBingePositionAsync"/> falls back to when nothing is
    /// pinned yet, then anchors on that episode's feed guid so later checks never need to repeat it.</summary>
    internal static async Task<BingeProgress> CreateBingeAsync(string libraryItemId)
    {
        var catalog = await GetFullCatalogAsync(libraryItemId);
        var ordered = OrderOldestFirst(catalog);
        if (ordered.Count == 0) return null;

        int currentIndex = await FindOldestUnfinishedIndexAsync(libraryItemId, ordered);
        if (currentIndex < 0) return null;

        return IndexToBinge(ordered, currentIndex);
    }

    /// <summary>Builds a <see cref="BingeProgress"/> anchored to a specific episode the listener picked
    /// directly (the AudioBookshelf Podcasts page's "Binge from here" long-press) - anchored by feed
    /// guid, not <see cref="AudiobookshelfEpisode.EpisodeId"/>: that id is tied to this specific
    /// downloaded-episode row and stops matching anything the moment AudioBookshelf deletes or
    /// re-imports it, which is exactly what silently broke the old id-based anchor after it had been
    /// pinned "for ages".</summary>
    internal static async Task<BingeProgress?> CreateBingeFromEpisodeAsync(AudiobookshelfEpisode episode)
    {
        if (string.IsNullOrEmpty(episode.Guid))
        {
            DiagnosticLog.Write($"[Radio] binge pin: \"{episode.EpisodeName}\" has no feed guid, falling back to an id-based anchor that won't survive AudioBookshelf deleting this download");
            return new BingeProgress
            {
                LastTrackedEpisodeId = episode.EpisodeId,
                NextEpisodeName = episode.EpisodeName,
                LastActivityUtc = DateTime.UtcNow
            };
        }

        var catalog = await GetFullCatalogAsync(episode.LibraryItemId);
        var ordered = OrderOldestFirst(catalog);
        int index = ordered.FindIndex(candidate => candidate.Guid == episode.Guid);
        if (index < 0)
        {
            DiagnosticLog.Write($"[Radio] binge pin: \"{episode.EpisodeName}\" (guid {episode.Guid}) not found in {episode.LibraryItemId}'s own feed, pinning by id instead");
            return new BingeProgress
            {
                LastTrackedEpisodeId = episode.EpisodeId,
                NextEpisodeName = episode.EpisodeName,
                LastActivityUtc = DateTime.UtcNow
            };
        }

        return IndexToBinge(ordered, index);
    }

    private static BingeProgress IndexToBinge(List<CatalogEpisode> ordered, int index) => new BingeProgress
    {
        LastFinishedIndexFromOldest = index - 1,
        AnchorGuid = ordered[index].Guid,
        NextEpisodeName = ordered[index].Title,
        LastActivityUtc = DateTime.UtcNow
    };

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

        // The podcast feed's own guid - stable across AudioBookshelf deleting and re-downloading this
        // episode, unlike LibraryEpisodeId. Used to anchor/re-find binge position; see BingeProgress.AnchorGuid.
        public string? Guid;
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
                EpisodeNumber = episode.Episode,
                Guid = episode.Guid
            }).ToList();
        }

        var feedEpisodes = await AudiobookshelfApiClient.GetPodcastFeedEpisodesAsync(feedUrl);
        var catalog = feedEpisodes.Select(feedEpisode =>
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
                Guid = feedEpisode.Guid,
                FeedEpisode = feedEpisode
            };
        }).ToList();

        int matchedCount = catalog.Count(episode => episode.IsDownloaded);
        DiagnosticLog.Write($"[Radio] binge-ahead: {libraryItemId} feed has {feedEpisodes.Count} episode(s), {downloaded.Count} already on the server, " +
            $"{matchedCount} of the feed matched to one of those by guid/enclosure URL" +
            (matchedCount != downloaded.Count ? $" - MISMATCH: {downloaded.Count - matchedCount} downloaded episode(s) didn't match anything in the feed, which would make them look missing" : ""));

        return catalog;
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

    /// <summary>Finds the current binge position as a persisted index into the oldest-first catalog,
    /// never by re-deriving "oldest unfinished episode in the whole feed" from live download state.
    ///
    /// That re-derivation (what this used to do on every call) is what broke: AudioBookshelf's own
    /// episode id (<see cref="CatalogEpisode.LibraryEpisodeId"/>) only exists for episodes currently
    /// downloaded, and stops matching anything the instant AudioBookshelf deletes a finished download
    /// (whether its own retention setting, or just this app's own ahead-window rotating files off the
    /// server). Once that happens, every episode before the real position also looks "not finished"
    /// for the exact same reason a never-downloaded future episode does, and a feed-wide search for
    /// "oldest episode that isn't provably finished" lands on episode #1 of however many hundred -
    /// exactly what happened to a show pinned "for ages" once its pinned episode's download was gone.
    ///
    /// Instead, <see cref="BingeProgress.LastFinishedIndexFromOldest"/> is the durable source of truth
    /// (same role it already plays for Spotify binges in <see cref="BingeProgressModel"/>): once known,
    /// position only ever moves forward from it, using live progress just to detect "has the listener
    /// since finished the episode(s) right after it" - never backward into the full back catalog.
    ///
    /// <paramref name="finishedEpisodeGuid"/> is direct evidence overriding all of that: if the listener
    /// is known to have just finished a specific episode (passed in by the "episode finished" trigger
    /// paths), its guid is matched into the catalog and, if that's ahead of whatever was already known,
    /// the position snaps straight there. Without this, finishing an episode played directly (e.g. from
    /// the Podcasts page, well ahead of or simply different from whatever Radio's own binge pick had
    /// last tracked) never moves the stored pointer at all - the forward-advance above only ever
    /// re-checks the episode the pointer already points at, never what was actually just heard.</summary>
    private static async Task<(List<CatalogEpisode> ordered, int currentIndex)> FindBingePositionAsync(string libraryItemId, string? finishedEpisodeGuid = null)
    {
        var catalog = await GetFullCatalogAsync(libraryItemId);
        var ordered = OrderOldestFirst(catalog);
        if (ordered.Count == 0) return (ordered, -1);

        var binge = RadioAudiobookshelfConfigModel.GetBinge(libraryItemId);
        int knownIndex = await ResolveKnownIndexAsync(libraryItemId, ordered, binge);

        if (!string.IsNullOrEmpty(finishedEpisodeGuid))
        {
            int finishedIndex = ordered.FindIndex(episode => episode.Guid == finishedEpisodeGuid);
            int impliedIndex = finishedIndex + 1;
            if (finishedIndex >= 0 && impliedIndex > knownIndex)
            {
                DiagnosticLog.Write($"[Radio] binge position for {libraryItemId}: snapping to just-finished episode #{finishedIndex} (\"{ordered[finishedIndex].Title}\"), ahead of the previously tracked index {knownIndex}");
                knownIndex = Math.Min(impliedIndex, ordered.Count - 1);
                PersistIndex(libraryItemId, ordered, knownIndex);
            }
            else if (finishedIndex < 0)
            {
                DiagnosticLog.Write($"[Radio] binge position for {libraryItemId}: just-finished episode guid {finishedEpisodeGuid} not found in this show's own feed, can't use it to adjust position");
            }
        }

        if (knownIndex < 0) return (ordered, -1);

        var progressByEpisodeId = (await AudiobookshelfApiClient.GetMediaProgressAsync())
            .Where(progress => progress.LibraryItemId == libraryItemId)
            .ToDictionary(progress => progress.EpisodeId ?? string.Empty);

        int currentIndex = knownIndex;
        while (currentIndex < ordered.Count - 1 && ordered[currentIndex].LibraryEpisodeId != null &&
            progressByEpisodeId.TryGetValue(ordered[currentIndex].LibraryEpisodeId!, out var progress) && progress.IsFinished)
        {
            currentIndex++;
        }

        if (currentIndex != knownIndex)
            PersistIndex(libraryItemId, ordered, currentIndex);

        DiagnosticLog.Write($"[Radio] binge position for {libraryItemId}: index {currentIndex} of {ordered.Count}" +
            $" (\"{ordered[currentIndex].Title}\", episode #{ordered[currentIndex].EpisodeNumber ?? "?"}, publishedAt {ordered[currentIndex].PublishedAtMs?.ToString() ?? "null"})");

        return (ordered, currentIndex);
    }

    /// <summary>The persisted index if there is one; otherwise resolves it once (by the stored feed-guid
    /// anchor if present, else the legacy id-based anchor, else the old whole-feed "oldest unfinished"
    /// search as a last resort) and persists it so this expensive, fallible resolution never has to run
    /// again for this show.</summary>
    private static async Task<int> ResolveKnownIndexAsync(string libraryItemId, List<CatalogEpisode> ordered, BingeProgress? binge)
    {
        if (binge == null) return -1;

        int index = binge.LastFinishedIndexFromOldest + 1;
        if (index >= 0 && index < ordered.Count) return index;

        if (!string.IsNullOrEmpty(binge.AnchorGuid))
        {
            int anchorIndex = ordered.FindIndex(episode => episode.Guid == binge.AnchorGuid);
            if (anchorIndex >= 0)
            {
                PersistIndex(libraryItemId, ordered, anchorIndex);
                return anchorIndex;
            }
            DiagnosticLog.Write($"[Radio] binge position for {libraryItemId}: anchor guid {binge.AnchorGuid} not found in the show's own feed");
        }

        if (!string.IsNullOrEmpty(binge.LastTrackedEpisodeId))
        {
            int anchorIndex = ordered.FindIndex(episode => episode.LibraryEpisodeId == binge.LastTrackedEpisodeId);
            if (anchorIndex >= 0)
            {
                DiagnosticLog.Write($"[Radio] binge position for {libraryItemId}: resolved legacy id-based anchor to index {anchorIndex}, switching to a guid anchor");
                PersistIndex(libraryItemId, ordered, anchorIndex);
                return anchorIndex;
            }
            DiagnosticLog.Write($"[Radio] binge position for {libraryItemId}: legacy anchor episode id {binge.LastTrackedEpisodeId} no longer matches any downloaded episode (its download was likely deleted) - its real position can't be recovered from this id alone. Falling back to the oldest-unfinished-in-the-whole-feed search, which may be wrong; re-pin \"Binge from here\" on the correct episode to fix it properly.");
        }

        int fallbackIndex = await FindOldestUnfinishedIndexAsync(libraryItemId, ordered);
        if (fallbackIndex < 0) return -1;

        PersistIndex(libraryItemId, ordered, fallbackIndex);
        return fallbackIndex;
    }

    /// <summary>Last-resort position guess when nothing durable is known yet: the oldest episode that
    /// AudioBookshelf's live progress doesn't show as finished. Only reliable while the relevant
    /// episodes are still downloaded - once used, the caller persists the result as an index so this
    /// never has to run again for the same show.</summary>
    private static async Task<int> FindOldestUnfinishedIndexAsync(string libraryItemId, List<CatalogEpisode> ordered)
    {
        var progressByEpisodeId = (await AudiobookshelfApiClient.GetMediaProgressAsync())
            .Where(progress => progress.LibraryItemId == libraryItemId)
            .ToDictionary(progress => progress.EpisodeId ?? string.Empty);

        return ordered.FindIndex(episode =>
            episode.LibraryEpisodeId == null ||
            !progressByEpisodeId.TryGetValue(episode.LibraryEpisodeId, out var progress) || !progress.IsFinished);
    }

    private static void PersistIndex(string libraryItemId, List<CatalogEpisode> ordered, int index)
    {
        var binge = RadioAudiobookshelfConfigModel.GetBinge(libraryItemId) ?? new BingeProgress();
        binge.LastFinishedIndexFromOldest = index - 1;
        binge.AnchorGuid = ordered[index].Guid;
        binge.NextEpisodeName = ordered[index].Title;
        binge.LastActivityUtc = DateTime.UtcNow;
        RadioAudiobookshelfConfigModel.SetBinge(libraryItemId, binge);
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
        {
            DiagnosticLog.Write($"[Radio] binge-ahead: ordered {episodes.Count} episode(s) by publishedAt (every episode had one)");
            return episodes.OrderBy(episode => episode.PublishedAtMs!.Value).ToList();
        }

        var numbered = episodes
            .Select(episode => (episode, number: ParseEpisodeNumber(episode)))
            .ToList();

        if (numbered.Count > 0 && numbered.All(entry => entry.number.HasValue))
        {
            DiagnosticLog.Write($"[Radio] binge-ahead: ordered {episodes.Count} episode(s) by episode number (publishedAt missing on at least one)");
            return numbered.OrderBy(entry => entry.number.Value).Select(entry => entry.episode).ToList();
        }

        DiagnosticLog.Write($"[Radio] binge-ahead: ordered {episodes.Count} episode(s) by raw pubDate string parsing - the least reliable fallback, " +
            "used because at least one episode had neither publishedAt nor a parseable episode number");
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
