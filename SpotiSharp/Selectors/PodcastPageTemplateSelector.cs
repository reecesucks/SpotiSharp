using SpotiSharp.ViewModels;

namespace SpotiSharp.Selectors;

public class PodcastPageTemplateSelector : DataTemplateSelector
{
    public DataTemplate GroupedTemplate { get; set; }
    public DataTemplate FlatTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return item switch
        {
            RecentEpisodesFlatViewModel => FlatTemplate,
            _ => GroupedTemplate
        };
    }
}
