using System.Collections.ObjectModel;
using System.Text.Json;
using SpotiSharp.ViewModels;

namespace SpotiSharp.Helpers
{
    public class TemplateStorageHelper
    {
        private string _templateDirectory = "C:\\Users\\reece\\OneDrive\\Documents\\Spotisharp";
        
        public List<string> GetTemplates() {
            var files = new List<string>();
           
            foreach (String file in Directory.GetFiles(_templateDirectory).Select(Path.GetFileName).ToList())
            {
                files.Add(file.Replace(".json", string.Empty));
            }

            return files;
        }

        public async Task<string> GetUserTemplateSelection()
        {
            var files = GetTemplates();

            var selectedFile = await SelectFile(files);
            return GetJson(selectedFile);
        }

        private string GetJson(string fileName)
        {

            string filePath = Path.Combine(_templateDirectory, $"{fileName}.json");
            string jsonString = File.ReadAllText(filePath);

            return jsonString;
        }

        private async Task<string> SelectFile(List<String> files)
        {
            var mainPage = Application.Current.MainPage;
            var currentPage = GetTopPage(mainPage);

            string selected = await currentPage.DisplayActionSheet(
                    "Select a file",
                    "Cancel",     // cancel button
                    null,         // no destructive button
                    files.ToArray()
                );

            return selected;
        }


        private Page GetTopPage(Page page)
        {
            if (page is FlyoutPage flyout)
                return GetTopPage(flyout.Detail);

            if (page is NavigationPage navPage)
                return GetTopPage(navPage.CurrentPage);

            if (page is TabbedPage tabPage)
                return GetTopPage(tabPage.CurrentPage);

            if (page.Navigation.ModalStack.Count > 0)
                return GetTopPage(page.Navigation.ModalStack.Last());

            return page;
        }

        public void SaveTemplate(string templateName, ObservableCollection<PlaylistSectionSectionCreatorViewModel> templateItems) 
        {
            string jsonString = JsonSerializer.Serialize(templateItems, new JsonSerializerOptions { WriteIndented = true });
            string filename = $"{templateName}.json";
            string filePath = Path.Combine(_templateDirectory, filename);

            File.WriteAllText(filePath, jsonString);
        }
    }
}
