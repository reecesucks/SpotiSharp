# SpotiSharp

A .NET MAUI minimalist Spotify client, forked from [SpotiSharp](https://github.com/SpotiSharp) and grown well past it. Split by design: **Windows builds playlists, Android listens.**
<p> <img src="https://github.com/user-attachments/assets/65414540-72d4-40ea-bfd0-03c79b4b5fce" width="220" alt="SpotiSharp screenshot 1"> <img src="https://github.com/user-attachments/assets/e0d5d5b5-d34d-42e3-86cf-189ff2acc127" width="220" alt="SpotiSharp screenshot 2"> </p>
## Features

- **Playlist creation (Windows)** — build new playlists from existing ones with filtering criteria, podcast support included.
- **Rotation system** — playlists tagged `#R-<n>` form a cumulative favourites ladder. One-tap **R+/R-** in the player bar promotes/demotes a song a rotation level, turning your listening into a play-frequency dataset stored in Spotify itself.
- **Radio (mobile)** — a generated queue interleaving recent podcast episodes (15-min segments) with shuffled tracks from your Rotation Level (#R) playlists and albums, weighted/configured per source.
- **Keypad navigation** — `QinF25.Input`, a reusable library turning a physical keypad phone's hardware keys into cross-platform input events, so the app is fully navigable without touch.

## Layout

| Project | Purpose |
|---|---|
| `SpotiSharp` | MAUI app — views, viewmodels, themes, keypad UI |
| `SpotiSharpBackend` | Spotify auth, API calls, playback/radio logic |
| `SpotiSharpBackend.Tests` | Backend unit tests |
| `QinF25.Input` | Standalone keypad-input library designed for Qin F25 |

Playback (Android) uses Spotify's App Remote SDK for live state/transport controls, with `SpotifyAPI.Web` still issuing "play this track/episode" commands. Auth is PKCE, no client secret needed.

## Building

```bash
dotnet workload restore
dotnet build SpotiSharp.sln
```

Register your own app at the [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) with redirect URI `http://127.0.0.1:5000/callback`, then enter its Client ID on first launch.
