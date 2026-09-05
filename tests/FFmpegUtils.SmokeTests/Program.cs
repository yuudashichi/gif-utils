using System.Text;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using FFmpegUtils.Models;
using FFmpegUtils.Services;
using FFmpegUtils.ViewModels;

var failures = new List<string>();

if (args is ["--render-modern", var reviewDirectory])
{
    ModernUiChecks.Run(Check, reviewDirectory);
    return failures.Count == 0 ? 0 : 1;
}

if (args is ["--gif-trim-integration", var trimFfmpeg, var trimDirectory])
{
    await GifTrimChecks.IntegrationAsync(trimFfmpeg, trimDirectory, Check);
    Console.WriteLine(failures.Count == 0 ? "GIF_TRIM_INTEGRATION_OK" : "GIF_TRIM_INTEGRATION_FAILED");
    return failures.Count == 0 ? 0 : 1;
}

if (args is ["--geocode-sample"])
{
    // Public landmark-area coordinates only; never reads user photos for this network diagnostic.
    var address = await ImageGeocodingService.Shared.ResolveAsync(new ImageCoordinates(39.916345, 116.397155), CancellationToken.None);
    Console.WriteLine(address.Region);
    Console.WriteLine(address.NearbyAddress);
    Console.WriteLine(address.Detail);
    return 0;
}

if (args is ["--image-info", var imageInfoInput])
{
    var info = await new ImageMetadataService().ReadAsync(imageInfoInput);
    foreach (var field in info.Dimensions.Concat(info.Shooting).Concat(info.Location))
        Console.WriteLine($"{field.Name}: {field.Value}");
    return 0;
}

if (args is ["--x-url", var xParseUrl])
{
    return await RunXParseIntegrationAsync(xParseUrl);
}

if (args is ["--x-download", var xDownloadUrl, var xOutputDirectory, var xFfmpegPath])
{
    return await RunXDownloadIntegrationAsync(xDownloadUrl, xOutputDirectory, xFfmpegPath);
}

CheckWindowConstruction();
var applicationAssembly = typeof(FFmpegUtils.MainWindow).Assembly;
Check(applicationAssembly.GetName().Name == "GIFUtils"
    && applicationAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title == "GIF Utils"
    && applicationAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product == "GIF Utils",
    "程序名称与文件属性统一为 GIF Utils");
await GifTrimChecks.RunAsync(Check);
await ImageMetadataChecks.RunAsync(Check);
await ImagePreviewChecks.RunAsync(Check);
await ImageGeocodingChecks.RunAsync(Check);
CheckNumericValidation();
CheckXUrlNormalization();
CheckXMediaJsonParsing();
CheckXFileNames();
CheckXProgressParsing();
CheckXFriendlyErrors();

Check(FfmpegProcessRunner.TryGetProgressSeconds("out_time_us", "2500000", out var seconds) && Math.Abs(seconds - 2.5) < 0.001,
    "进度微秒解析");
Check(FfmpegProcessRunner.TryGetProgressSeconds("out_time", "00:00:03.500000", out seconds) && Math.Abs(seconds - 3.5) < 0.001,
    "进度时间解析");

Check(SubtitleVideoEncoderCatalog.TryParseDisplayName(SubtitleVideoEncoderCatalog.NvidiaDisplayName, out var parsedEncoder)
      && parsedEncoder == SubtitleVideoEncoder.Nvidia,
    "字幕视频编码方式解析");
var cpuEncoderArguments = SubtitleBurnService.BuildVideoEncoderArguments(SubtitleVideoEncoder.Cpu, 20);
var nvencArguments = SubtitleBurnService.BuildVideoEncoderArguments(SubtitleVideoEncoder.Nvidia, 20);
var qsvArguments = SubtitleBurnService.BuildVideoEncoderArguments(SubtitleVideoEncoder.Intel, 20);
var amfArguments = SubtitleBurnService.BuildVideoEncoderArguments(SubtitleVideoEncoder.Amd, 20);
Check(cpuEncoderArguments.Contains("libx264") && cpuEncoderArguments.Contains("-crf"), "CPU 字幕编码参数");
Check(nvencArguments.Contains("h264_nvenc") && nvencArguments.Contains("-cq"), "NVIDIA 字幕编码参数");
Check(qsvArguments.Contains("h264_qsv") && qsvArguments.Contains("-global_quality"), "Intel 字幕编码参数");
Check(amfArguments.Contains("h264_amf") && amfArguments.Contains("-qp_i"), "AMD 字幕编码参数");

var escaped = FfmpegFilterEscaper.EscapePath(@"C:\测试 文件\a,b.srt");
Check(escaped.Contains("C\\:/", StringComparison.Ordinal) && escaped.Contains("a\\,b.srt", StringComparison.Ordinal),
    "字幕滤镜路径转义");

var reduced = GifSizeTuner.Reduce(new GifSizeParameters(720, 15, 192), 10_000_000, 4_000_000);
Check(reduced.Width < 720 && reduced.FrameRate <= 15 && reduced.Colors <= 192, "GIF 目标大小降级策略");
var smallSource = GifSizeTuner.Reduce(new GifSizeParameters(180, 10, 128), 1_000_000, 500_000, 180);
Check(smallSource.Width <= 180, "小尺寸视频不会被放大");

var utf8Path = Path.Combine(Path.GetTempPath(), $"ffmpegutils-utf8-{Guid.NewGuid():N}.srt");
try
{
    await File.WriteAllTextAsync(utf8Path, "1\n00:00:00,000 --> 00:00:01,000\n中文字幕\n", new UTF8Encoding(false));
    Check(SubtitleBurnService.DetectEncodingName(utf8Path) == "UTF-8", "字幕 UTF-8 检测");
}
finally
{
    if (File.Exists(utf8Path)) File.Delete(utf8Path);
}

if (args.Length >= 2 && File.Exists(args[0]) && File.Exists(args[1]))
{
    await RunIntegrationAsync(args[0], args[1]);
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"FAILED: {string.Join("; ", failures)}");
    return 1;
}

Console.WriteLine("SMOKE_TESTS_OK");
return 0;

void Check(bool condition, string name)
{
    if (condition)
    {
        Console.WriteLine($"PASS: {name}");
    }
    else
    {
        failures.Add(name);
        Console.Error.WriteLine($"FAIL: {name}");
    }
}

void CheckWindowConstruction() => ModernUiChecks.Run(Check);

void CheckNumericValidation()
{
    var viewModel = new MainViewModel { GifFrameRate = "aaa", SubtitleCrf = "invalid" };
    var gifValidator = typeof(MainViewModel).GetMethod("HasValidGifSettings", BindingFlags.Instance | BindingFlags.NonPublic);
    var subtitleValidator = typeof(MainViewModel).GetMethod("HasValidSubtitleSettings", BindingFlags.Instance | BindingFlags.NonPublic);
    var rejectsInvalidGifValue = gifValidator?.Invoke(viewModel, null) is false;
    var rejectsInvalidSubtitleValue = subtitleValidator?.Invoke(viewModel, null) is false;
    Check(viewModel.GifFrameRate == "aaa" && rejectsInvalidGifValue && rejectsInvalidSubtitleValue,
        "无效数字不会回退到旧值");
}

void CheckXUrlNormalization()
{
    var canonicalInput = "https://mobile.twitter.com/Some_User/status/1891234567890123456/video/1?utm_source=test#fragment";
    var normalized = XPostUrlService.TryNormalizeOfficialStatusUrl(canonicalInput, out var post, out var error);
    Check(normalized
          && error is null
          && post is not null
          && post.CanonicalUri.AbsoluteUri == "https://x.com/Some_User/status/1891234567890123456"
          && post.PostId == "1891234567890123456"
          && post.AccountName == "Some_User",
        "X URL 规范化并移除跟踪参数");

    var hostConfusionRejected = !XPostUrlService.TryNormalizeOfficialStatusUrl(
        "https://x.com.evil.example/alice/status/1234567890",
        out _,
        out var maliciousError)
        && !string.IsNullOrWhiteSpace(maliciousError);
    var userInfoRejected = !XPostUrlService.TryNormalizeOfficialStatusUrl(
        "https://x.com@evil.example/alice/status/1234567890",
        out _,
        out _);
    var nonDefaultPortRejected = !XPostUrlService.TryNormalizeOfficialStatusUrl(
        "https://x.com:8443/alice/status/1234567890",
        out _,
        out _);
    Check(hostConfusionRejected && userInfoRejected && nonDefaultPortRejected, "X URL 拒绝恶意域名、用户信息和非默认端口");

    var oversizedId = new string('9', 80);
    var oversizedIdAccepted = XPostUrlService.TryNormalizeOfficialStatusUrl(
        $"x.com/i/web/status/{oversizedId}",
        out var oversizedPost,
        out _)
        && oversizedPost?.PostId == oversizedId;
    Check(oversizedIdAccepted, "X 超大数字帖子 ID 不发生整数溢出");

    var spacesRejected = !XPostUrlService.TryNormalizeOfficialStatusUrl(
        "https://x.com/i/spaces/1YqKDqExample",
        out _,
        out var spacesError)
        && spacesError?.Contains("Spaces", StringComparison.OrdinalIgnoreCase) == true;
    Check(spacesRejected, "X Spaces 与直播链接被明确拒绝");
}

void CheckXMediaJsonParsing()
{
    const string postId = "1891234567890123456";
    var source = new XPostUrl(
        $"https://twitter.com/alice/status/{postId}",
        new Uri($"https://x.com/alice/status/{postId}"),
        postId,
        "alice");

    const string singleJson = """
        {
          "_type": "video",
          "id": "single-video",
          "display_id": "1891234567890123456",
          "extractor_key": "Twitter",
          "extractor": "twitter",
          "title": "Single video",
          "uploader_id": "alice",
          "duration": 12.5,
          "availability": "public",
          "formats": [
            {
              "format_id": "hls-1080",
              "url": "https://video.twimg.com/ext_tw_video/111/pu/pl/1080.m3u8",
              "protocol": "m3u8_native",
              "ext": "mp4",
              "vcodec": "h264",
              "acodec": "none",
              "audio_ext": "none",
              "width": 1920,
              "height": 1080,
              "tbr": 2200
            },
            {
              "format_id": "http-720",
              "url": "https://video.twimg.com/ext_tw_video/111/pu/vid/1280x720/720.mp4",
              "protocol": "https",
              "ext": "mp4",
              "vcodec": "h264",
              "acodec": "none",
              "audio_ext": "none",
              "width": 1280,
              "height": 720,
              "tbr": 900
            },
            {
              "format_id": "http-1080",
              "url": "https://video.twimg.com/ext_tw_video/111/pu/vid/1920x1080/1080.mp4",
              "protocol": "https",
              "ext": "mp4",
              "vcodec": "h264",
              "acodec": "none",
              "audio_ext": "none",
              "width": 1920,
              "height": 1080,
              "tbr": 1800
            },
            {
              "format_id": "audio-aac",
              "url": "https://video.twimg.com/ext_tw_video/111/pu/audio/audio.m4a",
              "protocol": "https",
              "ext": "m4a",
              "vcodec": "none",
              "acodec": "aac",
              "audio_ext": "m4a",
              "abr": 128
            }
          ]
        }
        """;

    var single = XMediaJsonParser.Parse(singleJson, source);
    Check(!single.IsPlaylist && single.Items.Count == 1 && single.Items[0].DurationSeconds == 12.5,
        "X 单媒体 JSON 解析");
    Check(single.Items[0].QualityOptions.Count == 2
          && single.Items[0].QualityOptions.All(option => !option.IsHls),
        "X 有 MP4 直链时不混入 HLS 画质");
    Check(single.Items[0].SelectedQuality == single.Items[0].QualityOptions[0]
          && single.Items[0].SelectedQuality.Height == 1080
          && single.Items[0].SelectedQuality.DisplayName.Contains("最高", StringComparison.Ordinal),
        "X 默认选择最高画质");
    Check(single.Items[0].QualityOptions.All(option => option.FormatSelector.EndsWith("+bestaudio/best", StringComparison.Ordinal)),
        "X 无音频视频格式选择器合并最佳音频并保留 best 回退");

    const string hlsOnlyJson = """
        {
          "id": "hls-video",
          "display_id": "1891234567890123456",
          "extractor_key": "Twitter",
          "extractor": "twitter",
          "title": "HLS only",
          "uploader_id": "alice",
          "availability": "public",
          "formats": [
            {
              "format_id": "hls-360",
              "url": "https://video.twimg.com/amplify_video/444/pl/360.m3u8",
              "protocol": "m3u8_native",
              "ext": "mp4",
              "vcodec": "h264",
              "acodec": "none",
              "audio_ext": "none",
              "width": 640,
              "height": 360,
              "tbr": 500
            },
            {
              "format_id": "hls-720",
              "url": "https://video.twimg.com/amplify_video/444/pl/720.m3u8",
              "protocol": "m3u8_native",
              "ext": "mp4",
              "vcodec": "h264",
              "acodec": "none",
              "audio_ext": "none",
              "width": 1280,
              "height": 720,
              "tbr": 1200
            }
          ]
        }
        """;
    var hlsOnly = XMediaJsonParser.Parse(hlsOnlyJson, source);
    Check(hlsOnly.Items[0].QualityOptions.Count == 2
          && hlsOnly.Items[0].QualityOptions.All(option => option.IsHls)
          && hlsOnly.Items[0].SelectedQuality.Height == 720,
        "X 无直链时回退 HLS 且仍默认最高画质");

    const string playlistJson = """
        {
          "_type": "playlist",
          "id": "1891234567890123456",
          "extractor_key": "Twitter",
          "extractor": "twitter",
          "title": "Playlist",
          "uploader_id": "alice",
          "availability": "public",
          "entries": [
            {
              "id": "top-video",
              "display_id": "1891234567890123456",
              "extractor_key": "Twitter",
              "extractor": "twitter",
              "uploader_id": "alice",
              "playlist_index": 1,
              "duration": 8,
              "formats": [
                { "format_id": "v1", "url": "https://video.twimg.com/ext_tw_video/111/pu/vid/720.mp4", "protocol": "https", "ext": "mp4", "vcodec": "h264", "acodec": "aac", "audio_ext": "m4a", "width": 1280, "height": 720, "tbr": 900 }
              ]
            },
            {
              "id": "top-gif",
              "display_id": "1891234567890123456",
              "extractor_key": "Twitter",
              "extractor": "twitter",
              "uploader_id": "alice",
              "playlist_index": 2,
              "duration": 3.5,
              "formats": [
                { "format_id": "gif1", "url": "https://video.twimg.com/tweet_video/333/gif.mp4", "protocol": "https", "ext": "mp4", "vcodec": "h264", "acodec": "none", "audio_ext": "none", "width": 480, "height": 270, "tbr": 300 }
              ]
            },
            {
              "id": "quoted-video",
              "display_id": "1891234567890123456",
              "extractor_key": "Twitter",
              "extractor": "twitter",
              "uploader_id": "quoted_account",
              "playlist_index": 3,
              "formats": [
                { "format_id": "q1", "url": "https://video.twimg.com/ext_tw_video/222/pu/vid/720.mp4", "protocol": "https", "ext": "mp4", "vcodec": "h264", "acodec": "aac", "audio_ext": "m4a", "width": 1280, "height": 720 }
              ]
            },
            {
              "id": "expanded-card",
              "display_id": "1891234567890123456",
              "extractor_key": "Youtube",
              "extractor": "youtube",
              "playlist_index": 4,
              "formats": [
                { "format_id": "yt", "url": "https://example.invalid/video.mp4", "protocol": "https", "ext": "mp4", "vcodec": "h264", "acodec": "aac", "audio_ext": "m4a", "width": 1920, "height": 1080 }
              ]
            }
          ]
        }
        """;
    var verifiedProbe = new XTopLevelMediaProbeResult(
        true,
        new Dictionary<string, XTopLevelMediaKind>
        {
            ["111"] = XTopLevelMediaKind.Video,
            ["333"] = XTopLevelMediaKind.AnimatedGif
        });
    var playlist = XMediaJsonParser.Parse(playlistJson, source, verifiedProbe);
    Check(playlist.IsPlaylist
          && playlist.Items.Count == 2
          && playlist.Items.Select(item => item.Id).SequenceEqual(["top-video", "top-gif"]),
        "X playlist 仅保留目标帖子顶层媒体并排除引用及外链条目");
    Check(playlist.Items[1].MediaTypeLabel == "动图（MP4）"
          && playlist.Items[1].Summary.Contains("动图", StringComparison.Ordinal),
        "X animated_gif 标注为循环 MP4 动图");
}

void CheckXFileNames()
{
    var safe = XFileNameHelper.SanitizeBaseName("  CON<>:\"/\\|?*  ");
    var reserved = XFileNameHelper.SanitizeBaseName("CON");
    var trailing = XFileNameHelper.SanitizeBaseName("clip.  ");
    var trimmed = XFileNameHelper.SanitizeBaseName(new string('a', 200));
    Check(safe.Length > 0
          && !safe.EndsWith('.')
          && !safe.EndsWith(' ')
          && safe.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
          && reserved == "_CON"
          && trailing == "clip"
          && trimmed.Length <= 120,
        "X 文件名前缀清理非法字符、保留名与过长输入");

    var directory = Path.Combine(Path.GetTempPath(), "FFmpegUtilsXFileNameSmoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try
    {
        File.WriteAllBytes(Path.Combine(directory, "X_alice_123_01.mp4"), [1]);
        Directory.CreateDirectory(Path.Combine(directory, "X_alice_123_01 (2).mp4"));
        var unique = XFileNameHelper.GetUniquePath(directory, "X_alice_123_01");
        Check(Path.GetFileName(unique) == "X_alice_123_01 (3).mp4" && !File.Exists(unique),
            "X 下载文件不覆盖现有文件或同名目录");

        var itemDirectory = Path.Combine(directory, "中文目录", "item-001");
        Directory.CreateDirectory(itemDirectory);
        var actualOutput = Path.Combine(itemDirectory, "video.mp4");
        File.WriteAllBytes(actualOutput, [1, 2, 3]);
        var garbledReportedPath = Path.Combine(directory, "����Ŀ¼", "item-001", "video.mp4");
        var resolver = typeof(XMediaDownloadService).GetMethod(
            "ResolveDownloadedPath",
            BindingFlags.Static | BindingFlags.NonPublic);
        var resolved = resolver?.Invoke(null, [garbledReportedPath, itemDirectory]) as string;
        Check(string.Equals(resolved, actualOutput, StringComparison.OrdinalIgnoreCase),
            "X 中文路径输出乱码时仅从隔离目录回退解析");
    }
    finally
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

void CheckXProgressParsing()
{
    var numericLine = XMediaDownloadService.ProgressPrefix + "1048576|2097152|NA|524288|2";
    var numeric = XMediaDownloadService.TryParseProgressLine(numericLine, out var numericProgress);
    Check(numeric
          && Math.Abs(numericProgress.Percent - 50) < 0.001
          && numericProgress.DownloadedBytes == 1_048_576
          && numericProgress.TotalBytes == 2_097_152
          && numericProgress.BytesPerSecond == 524_288
          && numericProgress.Detail.Contains("/s", StringComparison.Ordinal),
        "X 下载进度 sentinel 数字解析");

    var estimateLine = XMediaDownloadService.ProgressPrefix + "512|null|1024|NA|NA";
    var estimate = XMediaDownloadService.TryParseProgressLine(estimateLine, out var estimateProgress);
    Check(estimate && estimateProgress.TotalBytes == 1024 && Math.Abs(estimateProgress.Percent - 50) < 0.001,
        "X 下载进度缺少精确总量时使用估算值");

    var nullLine = XMediaDownloadService.ProgressPrefix + "NA|null|NA|null|NA";
    var nullSentinels = XMediaDownloadService.TryParseProgressLine(nullLine, out var nullProgress);
    Check(nullSentinels
          && nullProgress.Percent == 0
          && nullProgress.DownloadedBytes is null
          && nullProgress.TotalBytes is null
          && nullProgress.BytesPerSecond is null,
        "X 下载进度 null/NA sentinel 安全降级");
    Check(!XMediaDownloadService.TryParseProgressLine("ordinary yt-dlp output", out _),
        "X 下载进度忽略非 sentinel 输出");
}

void CheckXFriendlyErrors()
{
    var classifications = new (string Details, bool DuringDownload, XDownloadErrorKind Kind)[]
    {
        ("This tweet is private and login required", false, XDownloadErrorKind.AuthenticationRequired),
        ("This post is age-restricted", false, XDownloadErrorKind.AgeRestricted),
        ("Geo restricted: not available in your country", false, XDownloadErrorKind.GeoRestricted),
        ("HTTP Error 429: Too Many Requests", false, XDownloadErrorKind.RateLimited),
        ("No formats found", false, XDownloadErrorKind.NoVideo),
        ("HTTP Error 404: tweet deleted", false, XDownloadErrorKind.Unavailable),
        ("Postprocessing: ffmpeg not found", true, XDownloadErrorKind.FfmpegMissing),
        ("Unable to download: connection timed out", true, XDownloadErrorKind.Network),
        ("unexpected extractor response", false, XDownloadErrorKind.ParseFailed),
        ("unexpected downloader response", true, XDownloadErrorKind.DownloadFailed)
    };
    var mismatches = classifications
        .Select(test => (Test: test, Actual: XMediaDownloadService.CreateFriendlyException(test.Details, test.DuringDownload).Kind))
        .Where(result => result.Actual != result.Test.Kind)
        .Select(result => $"{result.Test.Kind}->{result.Actual} ({result.Test.Details})")
        .ToArray();
    Check(mismatches.Length == 0,
        mismatches.Length == 0
            ? "X 解析与下载错误映射为友好分类"
            : $"X 错误分类不匹配：{string.Join(", ", mismatches)}");

    var longDetail = new string('x', 7000) + " connection timed out";
    var friendly = XMediaDownloadService.CreateFriendlyException(longDetail, duringDownload: true);
    Check(friendly.Kind == XDownloadErrorKind.Network && friendly.Details.Length <= 6000,
        "X 技术错误详情截断且保留友好消息");
}

async Task<int> RunXParseIntegrationAsync(string url)
{
    try
    {
        Console.WriteLine("X_PARSE_INTEGRATION_START");
        var progress = new SynchronousProgress<XDownloadProgress>(value =>
            Console.WriteLine($"PROGRESS {value.Percent:0.#}% | {value.Stage} | {value.Detail}"));
        var result = await new XMediaDownloadService().ParseAsync(url, progress, CancellationToken.None);
        PrintXParseResult(result);
        Console.WriteLine("X_PARSE_INTEGRATION_OK");
        return 0;
    }
    catch (XDownloadException exception)
    {
        Console.Error.WriteLine($"X_PARSE_INTEGRATION_FAILED [{exception.Kind}]: {exception.Message}");
        if (!string.IsNullOrWhiteSpace(exception.Details))
        {
            Console.Error.WriteLine(exception.Details);
        }

        return 1;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"X_PARSE_INTEGRATION_FAILED: {exception}");
        return 1;
    }
}

async Task<int> RunXDownloadIntegrationAsync(string url, string outputDirectory, string ffmpegPath)
{
    try
    {
        Console.WriteLine("X_DOWNLOAD_INTEGRATION_START");
        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var progress = new SynchronousProgress<XDownloadProgress>(value =>
            Console.WriteLine($"PROGRESS {value.Percent:0.#}% | {value.Stage} | {value.Detail}"));
        var service = new XMediaDownloadService();
        var parsed = await service.ParseAsync(url, progress, CancellationToken.None);
        PrintXParseResult(parsed);
        var first = parsed.Items.First();
        foreach (var item in parsed.Items)
        {
            item.IsSelected = ReferenceEquals(item, first);
        }

        first.SelectedQuality = first.QualityOptions[0];
        Console.WriteLine($"SELECTED {first.DisplayName} | {first.SelectedQuality.DisplayName} | {first.SelectedQuality.FormatSelector}");
        var downloaded = await service.DownloadAsync(
            parsed,
            [first],
            outputDirectory,
            parsed.SuggestedPrefix,
            ffmpegPath,
            progress,
            CancellationToken.None);
        var outputPath = downloaded.OutputPaths.SingleOrDefault();
        if (outputPath is null
            || !Path.GetExtension(outputPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            || !File.Exists(outputPath)
            || new FileInfo(outputPath).Length <= 0)
        {
            Console.Error.WriteLine("X_DOWNLOAD_INTEGRATION_FAILED: 未生成非空 MP4。文件将保留供检查。");
            return 1;
        }

        Console.WriteLine($"OUTPUT {outputPath}");
        Console.WriteLine($"BYTES {new FileInfo(outputPath).Length}");
        Console.WriteLine("X_DOWNLOAD_INTEGRATION_OK");
        return 0;
    }
    catch (XDownloadException exception)
    {
        Console.Error.WriteLine($"X_DOWNLOAD_INTEGRATION_FAILED [{exception.Kind}]: {exception.Message}");
        if (!string.IsNullOrWhiteSpace(exception.Details))
        {
            Console.Error.WriteLine(exception.Details);
        }

        return 1;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"X_DOWNLOAD_INTEGRATION_FAILED: {exception}");
        return 1;
    }
}

void PrintXParseResult(XParseResult result)
{
    Console.WriteLine($"CANONICAL {result.Source.CanonicalUri.AbsoluteUri}");
    Console.WriteLine($"TITLE {result.Title}");
    Console.WriteLine($"ITEMS {result.Items.Count}");
    if (!string.IsNullOrWhiteSpace(result.Warning))
    {
        Console.WriteLine($"WARNING {result.Warning}");
    }

    foreach (var item in result.Items)
    {
        Console.WriteLine($"ITEM {item.Index} | playlist={item.PlaylistIndex} | {item.MediaTypeLabel} | {item.Summary} | id={item.Id}");
        foreach (var quality in item.QualityOptions)
        {
            Console.WriteLine($"QUALITY {item.Index} | {quality.DisplayName} | selector={quality.FormatSelector}");
        }
    }
}

async Task RunIntegrationAsync(string ffmpegPath, string inputVideo)
{
    var locator = new FfmpegLocator();
    var installation = await locator.InspectAsync(ffmpegPath);
    Check(installation.HasGifFilters, "FFmpeg GIF 滤镜可用");
    Check(installation.HasSubtitleFilter, "FFmpeg 字幕滤镜可用");

    var media = await new MediaProbeService().ProbeAsync(installation.FfprobePath, inputVideo);
    Check(media.DurationSeconds > 0 && media.Width > 0, "FFprobe 媒体信息");

    var integrationRoot = Path.Combine(Path.GetTempPath(), "FFmpegUtilsSmoke", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(integrationRoot);
    try
    {
        var runner = new FfmpegProcessRunner();
        var gifPath = Path.Combine(integrationRoot, "smoke.gif");
        var gifOptions = new GifConversionOptions(inputVideo, gifPath, Math.Min(480, media.Width), 15, 192, "bayer", 0.08, 0, Math.Min(1.5, media.DurationSeconds));
        var gif = await new GifConversionService(runner).ConvertAsync(installation, media, gifOptions, null, CancellationToken.None);
        Check(File.Exists(gif.OutputPath) && gif.FileSizeBytes > 0, "实际 GIF 转换");
        Check(gif.Attempts >= 2, "目标大小多轮压缩");

        var subtitlePath = Path.Combine(integrationRoot, "中文字幕.srt");
        await File.WriteAllTextAsync(subtitlePath, "1\n00:00:00,000 --> 00:00:01,000\nGIF Utils 测试\n", new UTF8Encoding(false));
        var burnedPath = Path.Combine(integrationRoot, "burned.mp4");
        var burned = await new SubtitleBurnService(runner).BurnAsync(
            installation,
            media,
            new SubtitleBurnOptions(inputVideo, subtitlePath, burnedPath, "自动", 24, "veryfast", SubtitleVideoEncoder.Auto),
            null,
            CancellationToken.None);
        Check(File.Exists(burned.OutputPath) && burned.FileSizeBytes > 0, "实际字幕烧录");
        Check(!string.IsNullOrWhiteSpace(burned.EncoderName)
              && burned.EncoderName != SubtitleVideoEncoderCatalog.AutoDisplayName,
            $"自动编码器解析为 {burned.EncoderName ?? "未知"}");
    }
    finally
    {
        if (Directory.Exists(integrationRoot)) Directory.Delete(integrationRoot, recursive: true);
    }
}

sealed class CollectingTraceListener : TraceListener
{
    private readonly StringBuilder _builder = new();

    public string Text
    {
        get
        {
            lock (_builder)
            {
                return _builder.ToString();
            }
        }
    }

    public override void Write(string? message)
    {
        lock (_builder)
        {
            _builder.Append(message);
        }
    }

    public override void WriteLine(string? message)
    {
        lock (_builder)
        {
            _builder.AppendLine(message);
        }
    }
}

sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
