using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace FFmpegUtils.Services;

public static class ImagePreviewService
{
    // Decode locally at display resolution, release the file, and freeze for UI-thread handoff.
    // Codec availability must never prevent the separate metadata reader from succeeding.
    public static Task<BitmapSource?> LoadAsync(string path) => Task.Run<BitmapSource?>(() =>
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            var width = frame.PixelWidth;
            var height = frame.PixelHeight;
            var orientation = ReadOrientation(frame.Metadata as BitmapMetadata);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (width >= height) image.DecodePixelWidth = Math.Min(900, width);
            else image.DecodePixelHeight = Math.Min(900, height);
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            var transform = new TransformGroup();
            if (orientation is 2 or 5 or 7) transform.Children.Add(new ScaleTransform(-1, 1));
            if (orientation == 4) transform.Children.Add(new ScaleTransform(1, -1));
            var rotation = orientation switch { 3 => 180, 6 or 7 => 90, 5 or 8 => 270, _ => 0 };
            if (rotation != 0) transform.Children.Add(new RotateTransform(rotation));
            if (transform.Children.Count == 0) return image;
            var oriented = new TransformedBitmap(image, transform);
            oriented.Freeze();
            return oriented;
        }
        catch { return null; }
    });

    private static int ReadOrientation(BitmapMetadata? metadata)
    {
        foreach (var query in new[] { "/app1/ifd/{ushort=274}", "/ifd/{ushort=274}" })
        {
            try { if (metadata?.GetQuery(query) is ushort orientation) return orientation; }
            catch (NotSupportedException) { }
        }
        return 1;
    }
}
