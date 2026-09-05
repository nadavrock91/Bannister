using Android.Content;
using Android.OS;
using Android.Provider;
using Uri = Android.Net.Uri;

namespace Bannister.Platforms.Android;

public static class MediaStoreHelper
{
    /// <summary>
    /// Saves PNG bytes to the device gallery (Pictures/BannisterCrops).
    /// Returns the number of files saved, throws on failure.
    /// </summary>
    public static int SavePanelsToGallery(
        Context context,
        IReadOnlyList<(string FileName, byte[] Bytes)> panels)
    {
        int saved = 0;
        foreach (var (fileName, bytes) in panels)
        {
            var values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "image/png");
            values.Put(MediaStore.IMediaColumns.RelativePath,
                "Pictures/BannisterCrops");

            var resolver = context.ContentResolver
                ?? throw new InvalidOperationException("ContentResolver unavailable.");

            Uri? uri = null;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                uri = resolver.Insert(
                    MediaStore.Images.Media.ExternalContentUri!, values);
            }
            else
            {
                // Android 9 and below — write to file then insert
                var dir = System.IO.Path.Combine(
                    global::Android.OS.Environment
                        .GetExternalStoragePublicDirectory(
                            global::Android.OS.Environment.DirectoryPictures)!
                        .AbsolutePath,
                    "BannisterCrops");
                System.IO.Directory.CreateDirectory(dir);
                var path = System.IO.Path.Combine(dir, fileName);
                System.IO.File.WriteAllBytes(path, bytes);
                values.Put(MediaStore.IMediaColumns.Data, path);
                uri = resolver.Insert(
                    MediaStore.Images.Media.ExternalContentUri!, values);
                saved++;
                continue;
            }

            if (uri == null)
                throw new InvalidOperationException(
                    $"MediaStore could not create entry for {fileName}.");

            using var stream = resolver.OpenOutputStream(uri)
                ?? throw new InvalidOperationException(
                    $"Could not open output stream for {fileName}.");
            stream.Write(bytes, 0, bytes.Length);
            saved++;
        }
        return saved;
    }
}
