using Image = SpotifyAPI.Web.Image;

namespace SpotiSharp.Helpers;

public static class ImageHelper
{
    // list thumbnails are tiny (under a cm), so take spotify's smallest variant
    // (~64px). decoding and holding these costs a fraction of the 300/640px ones,
    // which matters most on low end devices
    private const int TARGET_THUMBNAIL_SIZE = 48;

    public static string Thumbnail(IEnumerable<Image> images, int targetSize = TARGET_THUMBNAIL_SIZE)
    {
        if (images == null) return string.Empty;

        var candidates = images.Where(image => image != null && !string.IsNullOrEmpty(image.Url)).ToList();
        if (candidates.Count == 0) return string.Empty;

        var suitable = candidates.Where(image => image.Width >= targetSize).OrderBy(image => image.Width).FirstOrDefault();
        return (suitable ?? candidates.OrderByDescending(image => image.Width).First()).Url;
    }
}
