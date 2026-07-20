using Image = SpotifyAPI.Web.Image;

namespace SpotiSharp.Helpers;

public static class ImageHelper
{
    private const int TARGET_THUMBNAIL_SIZE = 160;

    public static string Thumbnail(IEnumerable<Image> images, int targetSize = TARGET_THUMBNAIL_SIZE)
    {
        if (images == null) return string.Empty;

        var candidates = images.Where(image => image != null && !string.IsNullOrEmpty(image.Url)).ToList();
        if (candidates.Count == 0) return string.Empty;

        var suitable = candidates.Where(image => image.Width >= targetSize).OrderBy(image => image.Width).FirstOrDefault();
        return (suitable ?? candidates.OrderByDescending(image => image.Width).First()).Url;
    }
}
