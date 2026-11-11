using SpotiSharp.ViewModels;

namespace SpotiSharp.Selectors;

public class RadioAudiobookshelfSettingsTemplateSelector : DataTemplateSelector
{
    public DataTemplate SourceTemplate { get; set; }
    public DataTemplate AlbumTemplate { get; set; }
    public DataTemplate MixTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is RadioAudiobookshelfAlbumListViewModel) return AlbumTemplate;
        if (item is RadioAudiobookshelfMixSettingsViewModel) return MixTemplate;
        return SourceTemplate;
    }
}
