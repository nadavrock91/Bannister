using SkiaSharp;

namespace Bannister.Services;

public class AssetThumbnailService
{
    private const int ThumbnailCachePx = 160;
    private const int JpegQuality = 75;

    private readonly string _cacheDir;

    public AssetThumbnailService()
    {
        _cacheDir = Path.Combine(FileSystem.CacheDirectory, "asset_thumbs");
        try { Directory.CreateDirectory(_cacheDir); } catch { }
    }

    public string? GetThumbnailPath(int assetId, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return null;

        var cachePath = Path.Combine(_cacheDir, $"{assetId}.jpg");
        if (File.Exists(cachePath))
        {
            var cacheStamp = File.GetLastWriteTimeUtc(cachePath);
            var sourceStamp = File.GetLastWriteTimeUtc(sourcePath);
            if (cacheStamp >= sourceStamp)
                return cachePath;
        }

        try
        {
            using var source = SKBitmap.Decode(sourcePath);
            if (source == null)
                return null;

            int side = Math.Min(source.Width, source.Height);
            int offsetX = (source.Width - side) / 2;
            int offsetY = (source.Height - side) / 2;

            using var cropped = new SKBitmap(side, side);
            using (var canvas = new SKCanvas(cropped))
            {
                var srcRect = new SKRect(offsetX, offsetY, offsetX + side, offsetY + side);
                var dstRect = new SKRect(0, 0, side, side);
                canvas.DrawBitmap(source, srcRect, dstRect);
            }

            using var resized = cropped.Resize(new SKImageInfo(ThumbnailCachePx, ThumbnailCachePx), SKFilterQuality.Medium);
            if (resized == null)
                return null;

            using var image = SKImage.FromBitmap(resized);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
            File.WriteAllBytes(cachePath, data.ToArray());
            return cachePath;
        }
        catch
        {
            return null;
        }
    }

    public void ClearAll()
    {
        try
        {
            if (Directory.Exists(_cacheDir))
                Directory.Delete(_cacheDir, recursive: true);
            Directory.CreateDirectory(_cacheDir);
        }
        catch { }
    }
}
