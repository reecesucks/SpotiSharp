using SpotiSharp.Enums;
using SpotiSharp.Interfaces;

namespace SpotiSharp.Models
{
    public class PodcastSectionType
    {
        public PodcastSectionEnum SectionType { get; set; }
        public string SectionTypeDescription { get; set; }

        public PodcastSectionType(PodcastSectionEnum sectionType, string description)
        {
            SectionType = sectionType;
            SectionTypeDescription = description;
        }
    }
}
