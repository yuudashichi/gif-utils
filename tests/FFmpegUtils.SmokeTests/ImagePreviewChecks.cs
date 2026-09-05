using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FFmpegUtils.Services;

internal static class ImagePreviewChecks
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "FFmpegUtils-Preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var (width, height) in new[] { (40, 20), (60, 1800) })
            {
                var path = Path.Combine(directory, $"预览-{width}.png");
                var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
                using (var file = File.Create(path)) encoder.Save(file);
                var before = await File.ReadAllBytesAsync(path);
                var preview = await ImagePreviewService.LoadAsync(path);
                check(preview is { IsFrozen: true } && preview.PixelWidth <= Math.Min(900, width) && preview.PixelHeight <= Math.Min(900, height), "图片预览限制长边、不放大小图且可跨线程显示");
                var after = await File.ReadAllBytesAsync(path);
                check(before.SequenceEqual(after), "预览不修改图片内容");
                using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                check(exclusive.Length > 0, "预览加载完成即释放图片文件锁");
            }
            var invalid = Path.Combine(directory, "invalid.png");
            await File.WriteAllTextAsync(invalid, "invalid");
            check(await ImagePreviewService.LoadAsync(invalid) is null, "损坏图片预览安全降级");
        }
        finally
        {
            foreach (var path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }
    }
}
