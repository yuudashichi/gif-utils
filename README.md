# GIF Utils

<img width="1384" height="992" alt="image" src="https://github.com/user-attachments/assets/751f2425-85dd-40ec-8e39-068d89559a94" />


Windows 桌面工具，提供四项功能：
- **MP4 转 GIF**：截取片段、预览视频、调整 GIF 大小。
- **字幕烧录**：将字幕嵌入视频，支持 CPU / GPU 编码。
- **X 视频下载**：解析公开帖子链接，选择画质并下载。
- **图片信息**：查看尺寸、拍摄参数和 GPS，支持手动查询地址。

## 构建与运行

**直接使用：** 在 [GitHub Releases](https://github.com/yuudashichi/gif-utils/releases/latest) 下载 `GIF-Utils-v1.1.0-win-x64.zip`，解压后运行 `GIFUtils.exe`。无需安装 .NET；FFmpeg 需另行安装。

本仓库提供源码，下面是自行构建的方法。

**构建流程**：
1. 在 Windows 上安装 [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)，选择 **SDK → Windows → x64**，不要只安装 Runtime。
2. 下载并解压源码，进入能看到 `publish.ps1` 的文件夹。在资源管理器的**地址栏**输入 `powershell`，按回车打开命令窗口。
3. 在窗口中输入下面的命令：

   ```powershell
   .\publish.ps1
   ```

   脚本会自动下载依赖、编译并打包，首次构建需要联网。可执行文件位于 `artifacts\publish\win-x64`，发布 ZIP 和 SHA-256 校验文件位于 `artifacts\release`。

4. 打开项目内的 `artifacts\publish\win-x64` 文件夹，双击 `GIFUtils.exe` 启动。生成的是 64 位程序，自带 .NET 运行环境。
5. 使用视频功能前，进入“设置”，点击“选择 FFmpeg”并选中 `ffmpeg.exe`，其同目录须有 `ffprobe.exe`。随后按页面提示选择文件或粘贴链接、设置保存位置并开始处理。图片信息只需选择图片，无需 FFmpeg。

**注意**：源码不包含FFmpeg，FFmpeg下载地址：https://ffmpeg.org/download.html。

第三方许可见 [licenses](licenses)。

## 界面与外观

- 左侧切换 GIF、字幕、X 下载和图片信息；底部显示当前任务进度、取消与导出操作。
- GIF 页默认使用高清预设（960px、20 FPS、256 色），直接展示视频预览和时间轴，详细压缩参数位于“高级参数”。窄窗口自动改为上下布局。
- “设置”提供浅色、深色、跟随系统主题，并集中管理 FFmpeg。主题选择会在下次启动时恢复。
- 图片页增加本地预览；系统无法解码的格式仍可独立读取元数据。
- GIF 和字幕处理完成后，可点击“打开结果”。

开发验证：

```powershell
dotnet build FFmpegUtils.slnx
dotnet run --project tests/FFmpegUtils.SmokeTests --no-build
dotnet run --project tests/FFmpegUtils.SmokeTests --no-build -- --render-modern artifacts/ui-review
```
