param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $projectRoot "src\FFmpegUtils\FFmpegUtils.csproj"
$output = Join-Path $projectRoot "artifacts\publish\$Runtime"
$projectXml = [xml](Get-Content -LiteralPath $project)
$version = $projectXml.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Missing application version in project file." }
$releaseDirectory = Join-Path $projectRoot 'artifacts\release'
$archive = Join-Path $releaseDirectory "GIF-Utils-v$version-$Runtime.zip"

dotnet restore $project -r $Runtime --ignore-failed-sources
if ($LASTEXITCODE -ne 0) {
    throw "Restore failed with exit code: $LASTEXITCODE"
}

dotnet publish $project -c Release -r $Runtime --self-contained true --no-restore `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $output

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code: $LASTEXITCODE"
}

Write-Output "Published to: $output"

$guide = Join-Path $output '使用说明.txt'
@"
GIF Utils v$version — Windows x64

1. 解压整个压缩包后，双击 GIFUtils.exe。程序自带 .NET 运行环境。
2. 视频处理需要另行安装 FFmpeg。本压缩包不包含 FFmpeg。
   下载：https://ffmpeg.org/download.html
   打开左侧“设置”，选择 ffmpeg.exe，其同目录须有 ffprobe.exe。
3. 左侧可切换 MP4 转 GIF、字幕烧录、X 下载和图片信息。
4. GIF 默认采用高清预设。可直接预览、拖动时间轴截取，并展开高级参数。
5. 设置中可切换浅色、深色或跟随系统主题。
6. X 下载首次使用会联网下载并校验 yt-dlp；图片信息读取无需 FFmpeg。
7. 图片地址查询只有在你确认后才会将经纬度发送到 Photon，不上传图片。

项目与更新：https://github.com/yuudashichi/gif-utils
第三方许可证请见 licenses 文件夹。
"@ | Set-Content -LiteralPath $guide -Encoding utf8

New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null
$packageFiles = @((Join-Path $output 'GIFUtils.exe'), (Join-Path $output 'licenses'), $guide)
Compress-Archive -LiteralPath $packageFiles -DestinationPath $archive -CompressionLevel Optimal -Force
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$digest  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath "$archive.sha256" -Encoding ascii
Write-Output "Release archive: $archive"
Write-Output "SHA-256: $digest"
