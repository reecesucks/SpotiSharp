
using SpotiSharp.Enums;

namespace SpotiSharp.Models
{
    public class PlaylistSectionType
    {
        public int SectionType { get; set; }
        public string SectionTypeDescription { get; set; }

        public PlaylistSectionType(int sectionType, string description)
        {
            SectionType = sectionType;
            SectionTypeDescription = description;
        }
    }
}
