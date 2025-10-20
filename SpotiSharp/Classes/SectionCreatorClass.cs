using SpotifyAPI.Web;
using SpotiSharp.Enums;
using SpotiSharp.Models;
using SpotiSharp.ViewModels;
using SpotiSharpBackend;

namespace SpotiSharp.Classes
{
    public class SectionCreatorClass
    {
        private List<String>  _playlistURIs { get; set; }

        public SectionCreatorClass(List<String> playlist) 
        {
            _playlistURIs = playlist;
        }

        #region "Add music"
        private void AddSectionEntirePlaylist(List<FullTrack> fullPlaylist)
        {
            AddTracksToPlaylist(fullPlaylist);
        }

        private void AddSectionPercentageRandom(List<FullTrack> fullPlaylist, int numericVal)
        {
            Random r = new Random();

            var soungAmount = (numericVal * fullPlaylist.Count) /100;

            var filteredList = new List<FullTrack>();

            while (filteredList.Count < soungAmount) {
                int rInt = r.Next(0, fullPlaylist.Count-1);
                if (!filteredList.Contains(fullPlaylist[rInt])) 
                    filteredList.Add(fullPlaylist[rInt]);
            }

            AddTracksToPlaylist(filteredList);
        }

        private void AddSectionFixedAmountSelected(List<FullTrack> selectedTracks)
        {
            AddTracksToPlaylist(selectedTracks);
        }

        private void AddSectionFixedAmountRandom(List<FullTrack> fullPlaylist, int numericVal)
        {
            Random r = new Random();
            var filteredList = new List<FullTrack>();

            while (filteredList.Count < numericVal)
            {
                int rInt = r.Next(0, fullPlaylist.Count - 1);
                if (!filteredList.Contains(fullPlaylist[rInt]))
                    filteredList.Add(fullPlaylist[rInt]);
            }

            AddTracksToPlaylist(filteredList);
        }

        private async Task<List<FullTrack>> GetPlaylistFullTracks(string playlistId)
        {
            var songListModel = new SongsListModel(playlistId);
            var apiCallerInstance = await APICaller.WaitForRateLimitWindowInstance;
            return apiCallerInstance?.GetMultipleTracksByTrackId(songListModel.Songs.Select(s => s.SongId).ToList());
        }
        private void AddTracksToPlaylist(List<FullTrack> newTracks)
        {
            newTracks.ForEach(track =>
            {
                if (!_playlistURIs.Contains(track.Uri))
                    _playlistURIs.Add(track.Uri);
            });
        }
        #endregion
        public async void CreateMusicSection(PlaylistSectionSectionCreatorViewModel vm)
        {
            if (vm.SelectedPlaylist is null || vm.SelectedSectionType is null) return;

            var sectionType = (PlaylistSectionEnum)vm.SelectedSectionType.SectionType;
            var playlistId = vm.SelectedPlaylist.PlayListId;
            var fullTracks = await GetPlaylistFullTracks(playlistId);
            var numericValue = vm.NumericValue;

            switch (sectionType)
            {
                case PlaylistSectionEnum.EntirePlaylist:
                    AddSectionEntirePlaylist(fullTracks);
                    break;
                case PlaylistSectionEnum.PercentageOfNewPlaylistRandom:
                    AddSectionPercentageRandom(fullTracks, numericValue);
                    break;
                case PlaylistSectionEnum.FixedAmountSelected:
                    AddSectionFixedAmountSelected(vm.MultiPickerSelections);
                    break;
                case PlaylistSectionEnum.FixedAmountRandom:
                    AddSectionFixedAmountRandom(fullTracks, numericValue);
                    break;
            }
        }

        public async void CreatePodcastSection(PlaylistSectionSectionCreatorViewModel vm)
        {
            if (vm.SelectedSectionTypePod is null || vm.SelectedSavedShow is null) return;

            var sectionType = (PodcastSectionEnum)vm.SelectedSectionTypePod.SectionType;
            var savedShowId = vm.SelectedSavedShow.Id;
            var numericValue = vm.NumericValue;

            switch (sectionType)
            {
                case PodcastSectionEnum.NewestUnplayed:
                    AddPodcastFirstUnplayed(vm.SelectedFullEpisodes);
                    break;
                case PodcastSectionEnum.RandomUnplayed:
                    AddRandomUnplayedPodcast(vm.SelectedFullEpisodes);
                    break;
                case PodcastSectionEnum.SelectEpisodes:
                    break;
            }
        }

        #region "Add podcasts"
        private async void AddPodcastFirstUnplayed(List<SimpleEpisode> episodes)
        {
            var eps = episodes.OrderByDescending(e => e.ReleaseDate).ToList();
            var ep = eps.FirstOrDefault();

            while (ep.ResumePoint.FullyPlayed || (ep.DurationMs - ep.ResumePoint.ResumePositionMs) <= ep.DurationMs * 0.05)
            {
                ep = eps[eps.IndexOf(ep) + 1];
            }

            if (!_playlistURIs.Contains(ep.Uri))
                _playlistURIs.Add(ep.Uri);
        }

        private async void AddRandomUnplayedPodcast(List<SimpleEpisode> episodes)
        {
            var eps = episodes.OrderByDescending(e => e.ReleaseDate).ToList();

            Random r = new Random();
            bool epsiodeSelected = false;

            while (!epsiodeSelected)
            {
                int rInt = r.Next(0, eps.Count - 1);
                var ep = eps[rInt];
            
                if (!ep.ResumePoint.FullyPlayed && (ep.DurationMs - ep.ResumePoint.ResumePositionMs) >= ep.DurationMs * 0.05)
                {
                    if (!_playlistURIs.Contains(ep.Uri))
                        _playlistURIs.Add(ep.Uri);
                    epsiodeSelected = true;
                }
            }
        }
        #endregion
    }
}
