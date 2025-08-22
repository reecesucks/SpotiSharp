using SpotiSharp.Enums;

namespace SpotiSharp.Models
{
    public class PlaylistSectionType
    {
        public PlaylistSectionEnum SectionType { get; set; }
        public string SectionTypeDescription { get; set; }

        public PlaylistSectionType(PlaylistSectionEnum sectionType, string description)
        {
            SectionType = sectionType;
            SectionTypeDescription = description;
        }
    }
}
