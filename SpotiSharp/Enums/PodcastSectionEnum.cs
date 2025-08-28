using System.ComponentModel;

namespace SpotiSharp.Enums
{
    public  enum PodcastSectionEnum
    {
        [Description("Newest Unplayed")]
        NewestUnplayed,
        [Description("Random Unplayed")]
        RandomUnplayed,
        [Description("Select Episodes")]
        SelectEpisodes,
    }
}
