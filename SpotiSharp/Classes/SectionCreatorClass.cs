using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using SpotiSharp.Enums;
using SpotiSharp.Models;
using SpotiSharp.ViewModels;
using SpotiSharpBackend;

namespace SpotiSharp.Classes
{
    public class SectionCreatorClass
    {
        private List<FullTrack>  _playlist { get; set; }

        public SectionCreatorClass(List<FullTrack> playlist) 
        {
            _playlist = playlist;
        }

        //public async void AddSection(PlaylistSectionEnum sectionType, SongsListModel songListModel)
        //{            

        //    List<FullTrack> result = new List<FullTrack>();

        //    switch (sectionType)
        //    {
        //        case PlaylistSectionEnum.EntirePlaylist:
        //            result = await AddSectionEntirePlaylist(songListModel);
        //            break;
        //        case PlaylistSectionEnum.PercentageOfNewPlaylistRandom:
        //            result = await AddSectionPercentageRandom(songListModel);
        //            break;
        //        case PlaylistSectionEnum.FixedAmountSelected:
        //            result = await AddSectionFixedAmountSelected(songListModel);
        //            break;
        //        case PlaylistSectionEnum.FixedAmountRandom:
        //            result = await AddSectionFixedAmountRandom(songListModel);
        //            break;
        //    }

        //    AddTracksToPlaylist(result);
        //}

        private  void AddSectionEntirePlaylist(List<FullTrack> fullPlaylist)
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

        private void AddSectionFixedAmountSelected(List<FullTrack> fullPlaylist)
        {
            //return new List<FullTrack>();
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
        public async void CreateSection(PlaylistSectionSectionCreatorViewModel vm)
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
                    AddSectionFixedAmountSelected(fullTracks);
                    break;
                case PlaylistSectionEnum.FixedAmountRandom:
                    AddSectionFixedAmountRandom(fullTracks, numericValue);
                    break;
            }
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
                if (!_playlist.Contains(track))
                    _playlist.Add(track);
            });
        }
    }
}
