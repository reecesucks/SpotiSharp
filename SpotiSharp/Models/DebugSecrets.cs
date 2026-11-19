namespace SpotiSharp.Models;

/// <summary>Local-only overrides for <see cref="BackendConnector"/>'s "paste key here for simplified
/// debugging" fallbacks, which used to hardcode real secrets directly in source - one of those got
/// committed and merged into this (public) repo. Never put a real value in THIS file; it's committed.
/// Add a gitignored <c>DebugSecrets.Local.cs</c> next to this one, implementing the same partial
/// methods with your actual values, to get the old convenience back on your own machine only.</summary>
internal static partial class DebugSecrets
{
    static partial void GetSpotifyClientId(ref string value);
    static partial void GetAudiobookshelfApiKey(ref string value);

    internal static string SpotifyClientId
    {
        get { var value = string.Empty; GetSpotifyClientId(ref value); return value; }
    }

    internal static string AudiobookshelfApiKey
    {
        get { var value = string.Empty; GetAudiobookshelfApiKey(ref value); return value; }
    }
}
