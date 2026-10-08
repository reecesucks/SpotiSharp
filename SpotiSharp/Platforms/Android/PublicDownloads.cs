using Android.Content;
using Android.OS;
using Android.Provider;

namespace SpotiSharp.Platforms.Android;

// Bug reports go to the public Download/SpotiSharp folder: unlike the app's own files they
// survive uninstalls and data clears, and can be copied off over USB without adb or a debug build.
internal static class PublicDownloads
{
    private const string Folder = "SpotiSharp";

    internal static string? Save(string fileName, string content)
    {
        var context = global::Android.App.Application.Context;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            var downloads = global::Android.OS.Environment.DirectoryDownloads;

            var values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
            values.Put(MediaStore.IMediaColumns.RelativePath, $"{downloads}/{Folder}");

            var uri = context.ContentResolver?.Insert(MediaStore.Downloads.ExternalContentUri, values);
            if (uri == null) return null;

            using (var stream = context.ContentResolver!.OpenOutputStream(uri))
            {
                if (stream == null) return null;
                using var writer = new StreamWriter(stream);
                writer.Write(content);
            }
            return $"Download/{Folder}/{fileName}";
        }

        // Before Android 10, writing to the public folders needs a storage permission, so fall
        // back to the app's own external folder (Android/data/<package>/files over USB). That one
        // is removed on uninstall.
        var directory = Path.Combine(context.GetExternalFilesDir(null)?.AbsolutePath ?? FileSystem.AppDataDirectory, "bug-reports");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
