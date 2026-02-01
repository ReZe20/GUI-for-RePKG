//ThumbnailLoader.cs
using Newtonsoft.Json.Linq;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace GUI_for_Repkg.Models
{
    internal class ThumbnailLoader
    {
        public record ThumbnailInfo
            (
            string FolderPath,
            string Title,
            string PreviewImageFullPath,
            string ContentRating,
            List<string> Tags
            );
        /// <summary>
        /// 在后台线程扫描所有scene类型壁纸，返回缩略图必要信息列表
        /// </summary>

        public static async Task<List<ThumbnailInfo>> ScanWallpapersAsync(string rootPath, IProgress<string>? progress = null)
        {
            var results = new List<ThumbnailInfo>();
            var stack = new Stack<string>();
            stack.Push(rootPath);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                try
                {
                    progress?.Report($"正在扫描: {current}");

                    foreach (var dir in Directory.GetDirectories(current))
                        stack.Push(dir);

                    string jsonPath = Path.Combine(current, "project.json");
                    if (!File.Exists(jsonPath)) continue;

                    string jsonText = await File.ReadAllTextAsync(jsonPath);
                    JObject? json = JObject.Parse(jsonText);

                    if (!string.Equals(json?["type"]?.Value<string>(), "scene", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string? previewFile = json?["preview"]?.Value<string>();
                    if (string.IsNullOrEmpty(previewFile)) continue;

                    string previewFullPath = Path.Combine(current, previewFile);
                    if (!File.Exists(previewFullPath)) continue;

                    string title = json?["title"]?.Value<string>() ?? "无标题";

                    string rating = json?["contentrating"]?.Value<string>() ?? "Everyone";

                    var tagsList = new List<string>();
                    var tagsToken = json?["tags"];
                    if (tagsToken != null)
                    {
                        foreach (var t in tagsToken)
                        {
                            tagsList.Add(t.ToString());
                        }
                    }

                    results.Add(new ThumbnailInfo(current, title, previewFullPath, rating, tagsList));
                }
                catch (Exception ex)
                {
                    //安静失败
                    System.Diagnostics.Debug.WriteLine($"扫描 {current} 失败: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// 在UI线程调用，根据扫描结果创建 Thumbnail UI 控件
        /// </summary>

        private static readonly SemaphoreSlim _loadSemaphore = new SemaphoreSlim(10);

        public static void CreateThumbnailControls(
            List<ThumbnailInfo> items,
            Panel targetPanel,
            bool isMultiSelectMode,
            MouseButtonEventHandler gridHandler,
            RoutedEventHandler checkBoxClickHandler)
        {
            targetPanel.Children.Clear();

            const int thumbnailSize = 300;
            const double normalScale = 1.0;
            const double hoverScale = 170 / 150.0;

            //图片动画
            var enlargeAnimation = new DoubleAnimation
            {
                To = hoverScale,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            var shrinkAnimation = new DoubleAnimation
            {
                To = normalScale,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            //图片阴影动画
            var shadowBlurAnimation = new DoubleAnimation
            {
                To = 50,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            var shadowBlurResetAnimation = new DoubleAnimation
            {
                To = 4,
                Duration = TimeSpan.FromSeconds(0.2),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            //图片标题以及灰色遮罩动画
            var titleBarHideAnimation = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromSeconds(0.1),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            var titleBarShowAnimation = new DoubleAnimation
            {
                To = 1,
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            foreach (var info in items)
            {
                var grid = new Grid
                {
                    Margin = new Thickness(2),
                };

                var dropShadow = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 4,
                    BlurRadius = 4,
                    Opacity = 0.5
                };

                var image = new Image
                {
                    Source = null,
                    Stretch = Stretch.UniformToFill,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                };
                var scaleTransform = new ScaleTransform(normalScale, normalScale);
                image.RenderTransform = scaleTransform;

                var backGroundShadow = new Border
                {
                    Background = new SolidColorBrush(Colors.White),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Effect = dropShadow
                };

                var titleTextBlock = new TextBlock
                {
                    Text = info.Title,
                    Foreground = Brushes.White,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 8, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.Wrap
                };

                var imageBorder = new Border
                {
                    Height = 45,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Opacity = 1,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    Child = titleTextBlock,
                };
                imageBorder.RenderTransform = scaleTransform;

                var checkBox = new CheckBox
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Width = 15,
                    Height = 15,
                    Tag = info.FolderPath,
                    Margin = new Thickness(10),
                    RenderTransformOrigin = new Point(1, 1),
                    Visibility = isMultiSelectMode ? Visibility.Visible : Visibility.Collapsed,
                    RenderTransform = scaleTransform
                };
                checkBox.Click += checkBoxClickHandler;

                grid.Children.Add(backGroundShadow);
                grid.Children.Add(image);
                grid.Children.Add(imageBorder);
                grid.Children.Add(checkBox);

                CheckBox currentcheckBox = checkBox;

                grid.MouseEnter += (sender, e) =>
                {
                    Panel.SetZIndex(grid, 1000);

                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, enlargeAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, enlargeAnimation);

                    imageBorder.BeginAnimation(UIElement.OpacityProperty, titleBarHideAnimation);

                    if (backGroundShadow.Effect is DropShadowEffect currentShadow)
                    {
                        currentShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlurAnimation);
                    }

                    checkBox.Visibility = Visibility.Visible;
                };
                grid.MouseLeave += (sender, e) =>
                {
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, shrinkAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, shrinkAnimation);

                    imageBorder.BeginAnimation(UIElement.OpacityProperty, titleBarShowAnimation);

                    if (backGroundShadow.Effect is DropShadowEffect currentShadow)
                    {
                        currentShadow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, shadowBlurResetAnimation);
                    }

                    Panel.SetZIndex(grid, 0);
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    bool isGlobalMultiMode = mainWindow != null && mainWindow.isMultiSelectMode;
                    if (checkBox.IsChecked == false && !isGlobalMultiMode)
                    {
                        checkBox.Visibility = Visibility.Collapsed;
                    }
                };

                grid.PreviewMouseLeftButtonDown += gridHandler;
                targetPanel.Children.Add(grid);

                _ = Task.Run(async () =>
                        {
                            await _loadSemaphore.WaitAsync();
                            try
                            {
                                WriteableBitmap realBitmap = null;
                                try
                                {
                                    using var originalBitmap = SKBitmap.Decode(info.PreviewImageFullPath);
                                    if (originalBitmap != null)
                                    {
                                        var targetInfo = new SKImageInfo(thumbnailSize, thumbnailSize);
                                        using var targetBitmap = new SKBitmap(targetInfo);

                                        var sampling = new SKSamplingOptions(SKCubicResampler.CatmullRom);

                                        // 如果 Resize 失败，回退到原图
                                        bool success = originalBitmap.ScalePixels(targetBitmap, sampling);
                                        originalBitmap.ScalePixels(targetBitmap, sampling);

                                        realBitmap = targetBitmap.ToWriteableBitmap();
                                        realBitmap.Freeze();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"SkiaSharp 加载失败 {info.PreviewImageFullPath}: {ex.Message}");
                                }
                                if (realBitmap != null)
                                {
                                    await Task.Delay(30);

                                    await Application.Current.Dispatcher.InvokeAsync(() =>
                                    {
                                        image.Source = realBitmap;
                                    });
                                }
                            }
                            finally
                            {
                                _loadSemaphore.Release();
                            }
                        });
            }
        }
    }
}
