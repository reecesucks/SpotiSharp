using SpotiSharp.ViewModels;

namespace SpotiSharp.Selectors;

public class RadioSettingsTemplateSelector : DataTemplateSelector
{
    public DataTemplate SourceTemplate { get; set; }
    public DataTemplate AlbumTemplate { get; set; }

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        return item is RadioAlbumListViewModel ? AlbumTemplate : SourceTemplate;
    }
}
