using SpotiSharp.Enums;

namespace SpotiSharp.Models
{
    public class PodcastSectionType
    {
        public PodcastSectionEnum SectionType { get; set; }
        public string SectionTypeDescription { get; set; }

        public PodcastSectionType(PodcastSectionEnum SectionType, string SectionTypeDescription)
        {
            this.SectionType = SectionType;
            this.SectionTypeDescription = SectionTypeDescription;
        }
    }
}
