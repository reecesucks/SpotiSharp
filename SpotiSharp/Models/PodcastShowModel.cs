using SpotifyAPI.Web;
using SpotiSharpBackend;

namespace SpotiSharp.Models
{
    public class PodcastShowModel
    {
        private List<SimpleEpisode> _episodes;

        public List<SimpleEpisode> Episodes
        {
            get { return _episodes;}
            private set => _episodes = value;
        }

        public PodcastShowModel(FullShow savedShow) 
        {
            LoadEpisodes(savedShow.Id);
        }

        private void LoadEpisodes(string showId)
        {
           List<SimpleEpisode> result = new List<SimpleEpisode>();
           var episodes = APICaller.Instance?.GetPodcastEpisodesByPodcastId(showId);
            if (episodes == null) return;
            foreach (var episode in episodes)
            {
                result.Add(episode);
            }
            _episodes = episodes;
        }
    }
}
