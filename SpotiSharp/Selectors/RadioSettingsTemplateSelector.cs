using SpotiSharp.ViewModels;

namespace SpotiSharp.Selectors;

public class RadioSettingsTemplateSelector : DataTemplateSelector
{
    public DataTemplate SourceTemplate { get; set; }
    public DataTemplate AlbumTemplate { get; set; }
    public DataTemplate MixTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is RadioAlbumListViewModel) return AlbumTemplate;
        if (item is RadioMixSettingsViewModel) return MixTemplate;
        return SourceTemplate;
    }
}
