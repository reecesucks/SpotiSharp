using SpotiSharp.Enums;

namespace SpotiSharp.Models
{
    public class PlaylistSectionType
    {
        public PlaylistSectionEnum SectionType { get; set; }
        public string SectionTypeDescription { get; set; }

        public PlaylistSectionType(PlaylistSectionEnum SectionType, string SectionTypeDescription)
        {
            this.SectionType = SectionType;
            this.SectionTypeDescription = SectionTypeDescription;
        }
    }
}
