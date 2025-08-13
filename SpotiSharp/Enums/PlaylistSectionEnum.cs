
using System.ComponentModel;

namespace SpotiSharp.Enums
{
    public enum PlaylistSectionEnum
    {
        [Description("Fixed Amount Random")]
        FixedAmountRandom,
        [Description("Percentage Of New Playlist Random")]
        PercentageOfNewPlaylistRandom,
        [Description("Entire Playlist")]
        EntirePlaylist,
        [Description("Fixed Amount - Selected")]
        FixedAmountSelected,
    }
}
