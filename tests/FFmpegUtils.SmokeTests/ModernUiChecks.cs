using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FFmpegUtils.Models;
using FFmpegUtils.Services;
using FFmpegUtils.ViewModels;

internal static class ModernUiChecks
{
    public static void Run(Action<bool, string> check, string? screenshotDirectory = null)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            FFmpegUtils.MainWindow? window = null;
            var trace = PresentationTraceSources.DataBindingSource;
            var level = trace.Switch.Level;
            var listener = new CollectingTraceListener();
            try
            {
                trace.Listeners.Add(listener);
                trace.Switch.Level = SourceLevels.Warning;
                window = new FFmpegUtils.MainWindow { ShowInTaskbar = false };
                check(window.Width == 1120 && window.Height == 800, "默认工作空间为 1120×800 DIP");
                window.Show();
                Layout(window);
                var tabs = (TabControl)window.FindName("MainTabs");
                var vm = (MainViewModel)window.DataContext;
                var quality = new XQualityOption("720×814 · 2176 kbps · MP4 直链（最高）", "http-2176", 720, 814, 2176, false, true, "http-2176");
                for (var i = 1; i <= 6; i++) vm.XMediaItems.Add(new XMediaItem(i, i, $"sample-{i}", $"媒体 {i}", "", "视频", 6, [quality]));
                typeof(FFmpegUtils.Infrastructure.ObservableObject).GetMethod("OnPropertiesChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(vm, [new[] { "HasXMedia", "XMediaSummary" }]);
                check(window.Icon is not null && window.Title == "GIF Utils", "窗口保留应用名称与图标");
                check(tabs.Items.Count == 5 && tabs.TabStripPlacement == Dock.Left, "四个工具与设置使用左侧键盘可操作导航");
                check(((Expander)window.FindName("GifTrimExpander")).IsExpanded && !((Expander)window.FindName("GifAdvancedExpander")).IsExpanded, "预览前置，高级参数默认折叠");

                foreach (var theme in new[] { AppTheme.Light, AppTheme.Dark })
                {
                    AppearanceService.Apply(window.Resources, theme);
                    for (var page = 0; page < 5; page++)
                    {
                        tabs.SelectedIndex = page;
                        Layout(window);
                        var scroll = (ScrollViewer)((TabItem)tabs.Items[page]).Content;
                        check(scroll.ScrollableWidth < 0.5, $"{theme} 页面 {page + 1} 无横向溢出");
                        var footer = (Border)window.FindName("TaskFooter");
                        check(footer.TranslatePoint(new Point(), window).Y >= scroll.TranslatePoint(new Point(), window).Y + scroll.ActualHeight - 1, "任务栏固定在内容下方");
                        if (page < 4)
                            check(!Children<Button>(window).Any(b => b.IsVisible && Equals(b.Content, "选择 FFmpeg")), "引擎选择集中在设置页");
                        if (screenshotDirectory is not null)
                        {
                            Directory.CreateDirectory(screenshotDirectory);
                            Save(window, Path.Combine(screenshotDirectory, $"{theme}-{page + 1}.png"));
                            if (page == 0)
                            {
                                var advanced = (Expander)window.FindName("GifAdvancedExpander");
                                advanced.IsExpanded = true;
                                Layout(window);
                                Save(window, Path.Combine(screenshotDirectory, $"{theme}-advanced.png"));
                                advanced.IsExpanded = false;
                                Layout(window);
                            }
                            Console.WriteLine($"LAYOUT {theme}/{page}: window={window.ActualWidth}x{window.ActualHeight}, viewport={scroll.ViewportWidth}x{scroll.ViewportHeight}, overflow={scroll.ScrollableHeight}");
                        }
                    }
                }
                tabs.SelectedIndex = 2;
                Layout(window);
                var list = Children<ListBox>(window).Single(b => b.IsVisible);
                check(ScrollViewer.GetVerticalScrollBarVisibility(list) == ScrollBarVisibility.Auto && ScrollViewer.GetHorizontalScrollBarVisibility(list) == ScrollBarVisibility.Disabled, "媒体列表独立滚动");
                var qualityCombo = Children<ComboBox>(list).First();
                qualityCombo.IsDropDownOpen = true;
                Layout(window);
                var popup = (Popup)qualityCombo.Template.FindName("PART_Popup", qualityCombo);
                check(popup.Child is FrameworkElement child && Math.Abs(child.ActualWidth - qualityCombo.ActualWidth) < 1, "画质下拉选项与控件等宽");
                qualityCombo.IsDropDownOpen = false;

                tabs.SelectedIndex = 4;
                Layout(window);
                var engine = (Button)window.FindName("SelectEngineButton");
                window.Activate();
                Keyboard.Focus(engine);
                window.SetCurrentValue(FFmpegUtils.MainWindow.ShowKeyboardFocusCuesProperty, true);
                Layout(window);
                check(engine.IsKeyboardFocused && engine.Template.FindName("FocusBorder", engine) is Border { BorderBrush: SolidColorBrush { Color.A: > 0 } }, "设置按钮保留键盘焦点提示");
                window.SetCurrentValue(FFmpegUtils.MainWindow.ShowKeyboardFocusCuesProperty, false);
                Layout(window);
                check(engine.Template.FindName("FocusBorder", engine) is Border { BorderBrush: SolidColorBrush { Color.A: 0 } }, "鼠标操作不残留键盘焦点框");

                window.Width = 800; window.Height = 600;
                for (var page = 0; page < 5; page++)
                {
                    tabs.SelectedIndex = page;
                    Layout(window);
                    var scroll = (ScrollViewer)((TabItem)tabs.Items[page]).Content;
                    check(scroll.ScrollableWidth < 0.5, $"窄窗口页面 {page + 1} 无横向溢出");
                    scroll.ScrollToBottom(); Layout(window);
                    check(scroll.VerticalOffset >= scroll.ScrollableHeight - 1, "窄窗口仍能访问页面末尾");
                    scroll.ScrollToTop(); Layout(window);
                    if (screenshotDirectory is not null) Save(window, Path.Combine(screenshotDirectory, $"Narrow-{page + 1}.png"));
                }
                check(Grid.GetRow((FrameworkElement)window.FindName("GifSettingsCard")) == 1 && Grid.GetRow((FrameworkElement)window.FindName("ImageDetailsPanel")) == 1, "窄窗口预览与参数自动转为上下布局");
                tabs.SelectedIndex = 0; Layout(window);
                var gifScroll = (ScrollViewer)((TabItem)tabs.Items[0]).Content;
                var combo = Children<ComboBox>(gifScroll).First();
                var selected = combo.SelectedIndex;
                var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
                combo.RaiseEvent(wheel); Layout(window);
                check(wheel.Handled && combo.SelectedIndex == selected && gifScroll.VerticalOffset > 0, "下拉框滚轮滚动页面且不误改参数");
                gifScroll.ScrollToTop();
                var input = Children<TextBox>(gifScroll).First();
                input.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = UIElement.PreviewMouseWheelEvent }); Layout(window);
                check(gifScroll.VerticalOffset > 0, "输入框滚轮可以滚动页面");

                var imageSample = new ImageInfoViewModel(_ => Task.FromResult(ImageMetadataChecks.Sample()));
                imageSample.LoadAsync("sample.jpg").GetAwaiter().GetResult();
                tabs.SelectedIndex = 3;
                var imageScroll = (ScrollViewer)window.FindName("ImageInfoScrollViewer");
                imageScroll.DataContext = imageSample;
                Layout(window);
                var values = Children<TextBox>(imageScroll).ToArray();
                check(values.Length == 22 && values.All(v => v.IsReadOnly), "图片元数据与地址字段保持只读可选择");
                check(values.Any(v => v.Text == "Example Camera") && values.Any(v => v.Text == "3000 × 4000 px"), "图片元数据真实绑定");
                check(imageSample.PreviewImage is null && imageSample.PreviewMessage.Length > 0 && imageSample.Error.Length == 0, "缺少预览解码能力不会阻止元数据展示");
                trace.Flush();
                check(!listener.Text.Contains("Error:") && !listener.Text.Contains("Warning:"), $"所有主题与页面绑定无错误 {listener.Text}");
            }
            catch (Exception ex) { error = ex; }
            finally
            {
                window?.Close(); trace.Listeners.Remove(listener); trace.Switch.Level = level;
            }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        check(error is null, error is null ? "现代界面初始化和交互检查完成" : $"现代界面检查失败：{error}");
    }

    private static void Layout(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Children<T>(child)) yield return descendant;
        }
    }

    private static void Save(Window window, string path)
    {
        var content = (FrameworkElement)window.Content;
        var dpi = VisualTreeHelper.GetDpi(content);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(content.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
