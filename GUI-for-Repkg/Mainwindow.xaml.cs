using GUI_for_Repkg.Models;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using static GUI_for_Repkg.Models.ProcessLauncher;
using static GUI_for_Repkg.Models.ThumbnailLoader;

namespace GUI_for_Repkg
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double ShadowDisableThreshold = 1500;
        private bool _isShadowEnabled = true;

        string repkgExePath = @"RePKG.exe";
        private List<ThumbnailInfo> allThumbnails = new List<ThumbnailInfo>();

        private static readonly SolidColorBrush NativeGreen = new SolidColorBrush(Color.FromRgb(6, 176, 37));  // 正常绿
        private static readonly SolidColorBrush NativeYellow = new SolidColorBrush(Color.FromRgb(255, 189, 0)); // 暂停黄
        private static readonly SolidColorBrush NativeRed = new SolidColorBrush(Color.FromRgb(204, 0, 0));    // 错误红

        public List<int> ThreadNumbers { get; set; } = new List<int>();

        public bool isMultiSelectMode = false;

        private const double MinItemWidth = 150;

        private CancellationTokenSource _cts;
        private ManualResetEventSlim _pauseEvent;
        private bool _isPaused = false;

        public MainWindow()
        {
            InitializeComponent();

            ReadRePKGversion(repkgExePath);
            PopulateThreadCount();
            ReadWallpaperEngineAdress();
            DefaultOutputPathSetting();

            this.Loaded += MainWindow_Loaded;
            this.SizeChanged += MainWindow_SizeChanged;
            ThumbnailScrollViewer.SizeChanged += (s, e) => UpdateThumbnailSize();
        }
        private void ReadRePKGversion(string RePKGPath)
        {
            if (!System.IO.File.Exists(RePKGPath))
            {
                RePKGVersion.Text = "未找到RePKG.exe";
                RePKGdeveloper.Text = "notscuffed";
            }
            else
            {
                RePKGVersion.Text = FileVersionInfo.GetVersionInfo(RePKGPath).ProductVersion;
                RePKGdeveloper.Text = "notscuffed";
            }
        }

        private void UpdateProgressState(string state)
        {
            switch (state)
            {
                case "Normal":
                    ConversionProgressBar1.Foreground = NativeGreen;
                    TaskBarProgress.ProgressState = TaskbarItemProgressState.Normal;
                    break;
                case "Paused":
                    ConversionProgressBar1.Foreground = NativeYellow;
                    TaskBarProgress.ProgressState = TaskbarItemProgressState.Paused;
                    break;
                case "Error":
                    ConversionProgressBar1.Foreground = NativeRed;
                    TaskBarProgress.ProgressState = TaskbarItemProgressState.Error;
                    break;
            }
        }

        private void UpdateThumbnailSize()
        {
            if (ThumbnailScrollViewer == null || ThumbnailPanel == null) return;

            // 获取中间区域可用的实际宽度
            // 注意：需要减去 ScrollBar 的宽度预留空间 (大约 20px) 
            double availableWidth = ThumbnailScrollViewer.ActualWidth - 20;

            if (availableWidth <= MinItemWidth) return;

            // 计算一行能放下多少个最小尺寸的图片
            int columns = (int)Math.Floor(availableWidth / MinItemWidth);
            if (columns < 1) columns = 1;

            // 计算每个图片的新宽度 (总宽度 / 列数)
            // Math.Floor 防止小数像素导致换行
            double newItemSize = Math.Floor(availableWidth / columns);

            // 直接设置 WrapPanel 的 ItemWidth 和 ItemHeight
            // 这样 WrapPanel 里的所有 Grid 都会自动变为这个大小
            ThumbnailPanel.ItemWidth = newItemSize;
            ThumbnailPanel.ItemHeight = newItemSize; // 保持正方形
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                // 处理阴影逻辑保持不变
                bool shouldEnableShadow = this.ActualWidth < ShadowDisableThreshold;
                if (shouldEnableShadow != _isShadowEnabled)
                {
                    _isShadowEnabled = shouldEnableShadow;
                    if (_isShadowEnabled) EnableAllThumbnailShadows();
                    else DisableAllThumbnailShadows();
                }

                // 调用新的自适应图片大小方法
                UpdateThumbnailSize();
            }
        }

        private void DisableAllThumbnailShadows()
        {
            foreach (Grid grid in ThumbnailPanel.Children.OfType<Grid>())
            {
                if (grid.Children.OfType<Border>().FirstOrDefault() != null)
                {
                    grid.Children.OfType<Border>().FirstOrDefault().Effect = null;
                }
            }
        }
        private void EnableAllThumbnailShadows()
        {
            foreach (Grid grid in ThumbnailPanel.Children.OfType<Grid>())
            {
                if (grid.Children.OfType<Border>().FirstOrDefault() != null)
                {
                    grid.Children.OfType<Border>().FirstOrDefault().Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 315,
                        ShadowDepth = 4,
                        BlurRadius = 4,
                        Opacity = 0.5
                    };
                }

            }
        }

        private void ThumbnailItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Grid grid)) return;

            // 关键修正：如果用户直接点击的是 CheckBox，不仅不要处理 Grid 点击，
            // 还要让 CheckBox 自己的 Click 事件去处理。
            // CheckBox 在 VisualTree 中是 Grid 的子元素。
            if (e.OriginalSource is DependencyObject originalSource)
            {
                // 向上查找，看点击源是否是 CheckBox 或其子元素（如 CheckBox 的文字/图形）
                var parent = originalSource;
                while (parent != null && parent != grid)
                {
                    if (parent is CheckBox) return; // 如果点的是复选框，直接退出，交给 ThumbnailCheckBox_Click
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            var checkBox = grid.Children.OfType<CheckBox>().FirstOrDefault();
            if (checkBox == null) return;

            string folderPath = checkBox.Tag as string;
            if (string.IsNullOrEmpty(folderPath)) return;

            if (isMultiSelectMode)
            {
                // 多选模式下，点击图片区域 = 切换选中状态
                checkBox.IsChecked = !checkBox.IsChecked;
                LoadDetail(folderPath);
                UpdateSelectedCountDisplay();
            }
            else
            {
                // 单选模式下：
                // 1. 清除其他所有选中
                foreach (Grid otherGrid in ThumbnailPanel.Children.OfType<Grid>())
                {
                    var otherCb = otherGrid.Children.OfType<CheckBox>().FirstOrDefault();
                    if (otherCb != null && otherGrid != grid)
                    {
                        otherCb.IsChecked = false;
                        // 同时隐藏未选中的复选框（因为鼠标不在那些上面）
                        otherCb.Visibility = Visibility.Collapsed;

                        // 清除边框高亮
                        var otherBorder = otherGrid.Children.OfType<Border>().FirstOrDefault();
                        if (otherBorder != null)
                        {
                            otherBorder.BorderThickness = new Thickness(0);
                            otherBorder.BorderBrush = Brushes.Transparent;
                        }
                    }
                }

                // 2. 选中当前项
                checkBox.IsChecked = true;

                // 加载详情
                LoadDetail(folderPath);
            }
            // 更新当前项的视觉（边框等）
            UpdateItemVisual(grid);

            e.Handled = true;
        }

        private void ThumbnailCheckBox_Click(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            if (!isMultiSelectMode && checkBox.IsChecked == true)
            {
                isMultiSelectMode = true;
                MultipleChoice.Content = "单选";

                UpdateAllItemVisuals();
            }
            string folderPath = checkBox.Tag as string;
            LoadDetail(folderPath);
            UpdateSelectedCountDisplay();
            e.Handled = true;
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Loaded -= MainWindow_Loaded;

            if (LoadImageWhenStart.IsChecked == true)
            {
                await LoadThumbnailsAutomatically();
            }
        }

        private async Task LoadThumbnailsAutomatically()
        {
            string rootPath = WallpapersFile.Text.Trim();

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                SituationPresentation.Text = "所选壁纸路径无壁纸,请在设置中设置路径";
                ThumbnailPanel.Children.Clear();
                return;
            }

            try
            {
                var thumbnails = await Task.Run(() =>
            ThumbnailLoader.ScanWallpapersAsync(rootPath,
                progress: new Progress<string>(msg =>
                    Dispatcher.Invoke(() => SituationPresentation.Text = msg))));

                allThumbnails = thumbnails;

                // 回到 UI 线程创建控件
                await Dispatcher.InvokeAsync(() =>
                {
                    ThumbnailLoader.CreateThumbnailControls(
                        allThumbnails,
                        ThumbnailPanel,
                        isMultiSelectMode,
                        ThumbnailItem_PreviewMouseLeftButtonDown,
                        ThumbnailCheckBox_Click);

                    _isShadowEnabled = this.ActualWidth < ShadowDisableThreshold;
            if (_isShadowEnabled)
                EnableAllThumbnailShadows();
            else
                DisableAllThumbnailShadows();
                    int count = thumbnails.Count;
                    SituationPresentation.Text = count == 0 ? "未找到壁纸，请重新设置壁纸路径" : $"已找到 {count} 个场景壁纸";
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    SituationPresentation.Text = $"加载出错：{ex.Message}";
                });
            }
        }

        private void SearchBox_Changed(object sender, TextChangedEventArgs e)
        {
            if (WallpaperSearchBox.Text == "")
            {
                SearchButton_Click(sender, e);
            }
            else return;
        }

        private void TextBox_EnterDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SearchButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void UpdateAllItemVisuals()
        {
            foreach (Grid grid in ThumbnailPanel.Children.OfType<Grid>())
            {
                UpdateItemVisual(grid);
            }
            UpdateSelectedCountDisplay();
        }

        private void UpdateItemVisual(Grid grid)
        {
            var checkBox = grid.Children.OfType<CheckBox>().FirstOrDefault();
            var border = grid.Children.OfType<Border>().FirstOrDefault();

            if (checkBox == null || border == null) return;

            if (checkBox.IsChecked == true || isMultiSelectMode)
            {
                checkBox.Visibility = Visibility.Visible;
            }
            else
            {
                if (!grid.IsMouseOver)
                {
                    checkBox.Visibility = Visibility.Collapsed;
                }
            }

            // 根据选中状态设置边框高亮
            if (checkBox.IsChecked == true)
            {
                border.BorderThickness = new Thickness(5);
                border.BorderBrush = Brushes.DodgerBlue;
                border.CornerRadius = new CornerRadius(0);
            }
            else
            {
                border.BorderThickness = new Thickness(0);
                border.BorderBrush = Brushes.Transparent;
            }
        }

        private void MultipleChoice_Click(object sender, RoutedEventArgs e)
        {
            isMultiSelectMode = !isMultiSelectMode;
            MultipleChoice.Content = isMultiSelectMode ? "单选" : "多选";
            foreach (Grid anyGrid in ThumbnailPanel.Children.OfType<Grid>())
            {
                var anyCb = anyGrid.Children.OfType<CheckBox>().FirstOrDefault();
                anyCb.IsChecked = false;
            }
            // 切换模式后更新所有视觉状态（主要是复选框可见性）
            UpdateAllItemVisuals();
        }

        private void InverseSelect_Click(object sender, RoutedEventArgs e)
        {
            MultipleChoice.Content = "单选";
            isMultiSelectMode = true;
            foreach (Grid grid in ThumbnailPanel.Children.OfType<Grid>())
            {
                grid.Children.OfType<CheckBox>().FirstOrDefault().IsChecked = !grid.Children.OfType<CheckBox>().FirstOrDefault().IsChecked;
            }
            UpdateAllItemVisuals();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            MultipleChoice.Content = "单选";
            isMultiSelectMode = true;
            foreach (Grid grid in ThumbnailPanel.Children.OfType<Grid>())
                grid.Children.OfType<CheckBox>().FirstOrDefault().IsChecked = true;
            UpdateAllItemVisuals();
        }

        private void LoadDetail(string folderPath)
        {
            try
            {
                string jsonPath = Path.Combine(folderPath, "project.json");
                if (!File.Exists(jsonPath)) return;

                JObject json = JObject.Parse(File.ReadAllText(jsonPath));

                string title = json["title"]?.ToString() ?? "无标题";
                string description = json["description"]?.ToString() ?? "无描述";
                string previewFile = json["preview"]?.ToString();

                DetailTitle.Text = title;
                DetailWorkshopID.Text = new DirectoryInfo(folderPath).Name;

                if (!string.IsNullOrEmpty(previewFile))
                {
                    string fullPreviewPath = Path.Combine(folderPath, previewFile);
                    if (File.Exists(fullPreviewPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(fullPreviewPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        DetailPreview.Source = bitmap;
                        DetailPreviewShadow.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        DetailPreview.Source = null;
                    }
                }
                else
                {
                    DetailPreview.Source = null;
                }
            }
            catch
            {
                DetailTitle.Text = "加载详情失败";
                DetailPreview.Source = null;
            }
        }

        private void UpdateSelectedCountDisplay()
        {
            int count = ThumbnailPanel.Children.OfType<Grid>()
                .Count(g => g.Children.OfType<CheckBox>().FirstOrDefault()?.IsChecked == true);

            if (isMultiSelectMode)
            {
                SelectedCount.Visibility = Visibility.Visible;
            }
            else
            {
                SelectedCount.Visibility = Visibility.Collapsed;
            }
                SelectedCount.Text = $"已选中: {count}";
        }

        private void UnenabledSettingCheckbox()
        {
            UseProjectNameForFile.IsEnabled = (!UseProjectNameForFile.IsEnabled);
            JustSaveImages.IsEnabled = (!JustSaveImages.IsEnabled);
            DontTransformTexFiles.IsEnabled = (!DontTransformTexFiles.IsEnabled);
            PutAllFilesInOneDirectory.IsEnabled = (!PutAllFilesInOneDirectory.IsEnabled);
            CoverAllFiles.IsEnabled = (!CoverAllFiles.IsEnabled);
            CopyProjectJson.IsEnabled = (!CopyProjectJson.IsEnabled);
            multithreading.IsEnabled = (!multithreading.IsEnabled);
        }

        private async void StartProcess_Click(object sender, RoutedEventArgs e)
        {
            UpdateProgressState("Normal");

            var selectedItems = ThumbnailPanel.Children.OfType<Grid>().Select(g => new
            {
                Grid = g,
                CheckBox = g.Children.OfType<CheckBox>().FirstOrDefault()
            })
                .Where(x => x.CheckBox?.IsChecked == true && x.CheckBox.Tag is string path && Directory.Exists(path))
                .Select(x => x.CheckBox.Tag as string)
                .ToList();

            if (!File.Exists(repkgExePath))
            {
                MessageBox.Show($"找不到{repkgExePath}，请检查路径是否正确", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!selectedItems.Any())
            {
                MessageBox.Show("请选择一个场景壁纸！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string outputPath = OutputPathBox.Text.Trim();

            if (!Directory.Exists(outputPath))
            {
                try
                {
                    Directory.CreateDirectory(outputPath);
                }
                catch
                {
                    MessageBox.Show("无法创建输出目录，请检查权限或手动创建。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            
            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true); // true 表示初始状态为“运行中”（非暂停）
            _isPaused = false;

            ToggleControlButtons(true);
            UnenabledSettingCheckbox();

            int threadCount = multithreading.IsChecked == true ? (int)cmbThreadCount.SelectedItem : 1;

            SituationPresentation.Text = "准备启动处理...";

            var progressReport = new Progress<ProcessProgressReport>(report =>
            {
                // Progress<T> 会自动在 UI 线程上调用，这里不需要再Dispatcher.Invoke
                SituationPresentation.Text = report.Message; 
                ConversionProgressBar1.Value = report.Percentage;
                TaskBarProgress.ProgressValue = report.Percentage / 100;
                SelectedCount.Text = $"已完成: {report.CompletedCount}/{report.TotalCount}";
            });

            try
            {
                await ProcessLauncher.LaunchAsync(
                    selectedItems,
                    repkgExePath,
                    outputPath,
                    threadCount,
                    _cts.Token,
                    _pauseEvent,
                    progressReport);

                MessageBox.Show("所有壁纸处理完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                UnenabledSettingCheckbox();
            }
            catch (OperationCanceledException)
            {
                SituationPresentation.Text = "提取已停止";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理过程中发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                SituationPresentation.Text = "处理失败";
                UpdateProgressState("Error");
                UnenabledSettingCheckbox();
            }
            finally
            {
                UnenabledSettingCheckbox();
                ToggleControlButtons(false);
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_pauseEvent != null && !_isPaused)
            {
                _pauseEvent.Reset();
                _isPaused = true;
                SituationPresentation.Text = " 任务已暂停...";

                BtnPause.IsEnabled = false;
                BtnResume.IsEnabled = true;
                UpdateProgressState("Paused");
            }
        }

        private void BtnResume_Click(object sender, RoutedEventArgs e)
        {
            if (_pauseEvent != null && _isPaused)
            {
                _pauseEvent.Set();
                _isPaused = false;
                SituationPresentation.Text = "任务已恢复...";

                BtnPause.IsEnabled = true;
                BtnResume.IsEnabled = false;
                UpdateProgressState("Normal");
            }
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_cts == null || _cts.IsCancellationRequested) return;

            if (MessageBox.Show("确定要停止当前任务吗？", "停止", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    _cts.Cancel();

                    if (_isPaused && _pauseEvent != null)
                    {
                        _pauseEvent.Set();
                    }
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { MessageBox.Show($"停止时发生错误：{ex.Message}"); }
                UpdateProgressState("Error");
            }

        }

        private void ToggleControlButtons(bool isRunning)
        {
            StartProcessButton.IsEnabled = !isRunning;

            BtnStop.IsEnabled = isRunning;
            BtnPause.IsEnabled = isRunning;
            BtnResume.IsEnabled = false;
        }

        private void PopulateThreadCount()
        {
            int logicalCores = Environment.ProcessorCount;

            if (logicalCores > 1)
            {
                multithreading.IsChecked = true;

                int max = Math.Max(logicalCores, 2);
                ThreadNumbers = Enumerable.Range(2, max - 1).ToList();

                cmbThreadCount.ItemsSource = ThreadNumbers;
                cmbThreadCount.SelectedItem = max;
            }
            else
            {
                multithreading.IsChecked = false;
                multithreading.IsEnabled = false;
            }
        }

        private static string TruncateToLastChar(string inputStr, string suffixToRemove)
        {
            if (string.IsNullOrEmpty(inputStr) || string.IsNullOrEmpty(suffixToRemove))
                return inputStr;

            if (inputStr.EndsWith(suffixToRemove))
                return inputStr.Substring(0, inputStr.Length - suffixToRemove.Length);

            return inputStr;
        }

        private void ReadWallpaperEngineAdress()
        {
            try
            {
                using (RegistryKey rootKey = Registry.CurrentUser)
                {
                    string[] possibleSubKeys = { @"Software\WallpaperEngine", @"Software\Wallpaper Engine" };

                    foreach (var subKey in possibleSubKeys)
                    {
                        using (RegistryKey weKey = rootKey.OpenSubKey(subKey))
                        {
                            if (weKey == null) continue;

                            object installPath = weKey.GetValue("installPath");
                            if (installPath != null)
                            {
                                WallpapersFile.Text = TruncateToLastChar((string)installPath, "\\common\\wallpaper_engine\\wallpaper64.exe")
                                                      + "\\workshop\\content\\431960";
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ReadWallpaperEngineUserSettings error: {ex}");
            }
        }

        private void DefaultOutputPathSetting()
        {
            try
            {
                OutputPathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "GUI-for-Repkg_Output");
            }
            catch
            {
                OutputPathBox.Text = "";
            }
        }

        private void SettleOutputPathBox(object sender, RoutedEventArgs e)
        {
            try
            {
                var fileDialog = new OpenFileDialog
                {
                    Title = "请选择输出文件夹",
                    InitialDirectory = OutputPathBox.Text,
                    FileName = "请选择文件夹",
                    CheckFileExists = false,
                    CheckPathExists = true,
                    ValidateNames = false
                };

                if (fileDialog.ShowDialog() == true)
                {
                    string selectedFolder = Path.GetDirectoryName(fileDialog.FileName);
                    OutputPathBox.Text = selectedFolder;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择文件夹时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenOutputPathBox_Click(object sender, RoutedEventArgs e)
        {
            string outputPath = OutputPathBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                MessageBox.Show("输出路径为空，请设置路径！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!Directory.Exists(outputPath))
            {
                try
                {
                    Directory.CreateDirectory(outputPath);
                }
                catch
                {
                    MessageBox.Show("输出目录不存在，程序在尝试创建时失败，请自行创建文件夹", "错误", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", outputPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件夹：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnFilterChanged(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            // 如果还没加载过壁纸，直接返回
            if (allThumbnails == null || allThumbnails.Count == 0) return;

            // 1. 获取搜索框文本
            string searchText = WallpaperSearchBox.Text.Trim().ToLower();

            // 2. 获取选中的【分级】 (遍历 Expander 里的 CheckBox)
            // 假设你的 Expander 有个 x:Name 或者我们直接遍历 TagsListPanel 的父级兄弟
            // 为了方便，建议给 年龄分类的 StackPanel 一个 x:Name="AgeFilterPanel"
            // 给 标签分类的 StackPanel 一个 x:Name="TagsListPanel" (XAML里已经有了)

            // 这里假设你在 XAML 里给年龄 StackPanel 加了 x:Name="AgeFilterPanel"
            // 如果没有，你需要手动去 XAML 加上。
            var checkedRatings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (AgeFilterPanel != null)
            {
                foreach (var cb in AgeFilterPanel.Children.OfType<CheckBox>())
                {
                    if (cb.IsChecked == true && cb.Tag != null)
                    {
                        checkedRatings.Add(cb.Tag.ToString());
                    }
                }
            }

            // 3. 获取选中的【标签】
            var checkedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (TagsListPanel != null)
            {
                foreach (var cb in TagsListPanel.Children.OfType<CheckBox>())
                {
                    if (cb.IsChecked == true && cb.Tag != null)
                    {
                        checkedTags.Add(cb.Tag.ToString());
                    }
                }
            }

            // 4. LINQ 查询过滤
            // 4. LINQ 查询过滤
            var filteredList = allThumbnails.Where(item =>
            {
                // A. 搜索文本
                bool matchSearch = string.IsNullOrEmpty(searchText) ||
                                   item.Title.ToLower().Contains(searchText);

                // B. 分级过滤 (强力兼容模式)
                string itemRating = string.IsNullOrEmpty(item.ContentRating) ? "everyone" : item.ContentRating.ToLower();
                // 确保 checkedRatings 里的值也都是小写
                bool matchRating = checkedRatings.Any(r => r.Equals(itemRating, StringComparison.OrdinalIgnoreCase));

                // C. 标签过滤
                bool matchTags = false;
                if (item.Tags == null || item.Tags.Count == 0)
                {
                    // 没标签的壁纸，只要勾选了“未指定”就显示
                    matchTags = checkedTags.Any(t => t.Equals("Unspecified", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    // 只要壁纸拥有的标签中，有一个是在已选列表里的，就返回 true
                    matchTags = item.Tags.Any(t => checkedTags.Contains(t));
                }

                return matchSearch && matchRating && matchTags;
            }).ToList();

            // 5. 更新 UI
            // 注意：这里复用了你现有的 CreateThumbnailControls
            ThumbnailLoader.CreateThumbnailControls(
                filteredList,
                ThumbnailPanel,
                isMultiSelectMode,
                ThumbnailItem_PreviewMouseLeftButtonDown,
                ThumbnailCheckBox_Click);

            // 6. 更新底部状态栏文字
            SituationPresentation.Text = $"筛选显示 {filteredList.Count} / {allThumbnails.Count} 个壁纸";

            // 7. 处理阴影 (保持你原有的逻辑)
            if (_isShadowEnabled) EnableAllThumbnailShadows();
            else DisableAllThumbnailShadows();
        }

        private void RestoreDefaultWallpapersPath_Click(object sender, RoutedEventArgs e)
        {
            ReadWallpaperEngineAdress();
        }

        private void Unenable_Left_Column_Click(object sender, RoutedEventArgs e)
        {
            LeftColumn.Width = new GridLength(0);
            EnableLeftColumnButton.Visibility = Visibility.Visible;
            OutputPathBox.Margin = new Thickness(42, 8, 120, 0);
        }

        private void Enable_Left_Column_Click(object sender, RoutedEventArgs e)
        {
            EnableLeftColumnButton.Visibility = Visibility.Collapsed;
            LeftColumn.Width = new GridLength(145);
            OutputPathBox.Margin = new Thickness(0, 8, 120, 0);
        }

        private void Multithreading_Checked(object sender, RoutedEventArgs e)
        {
            cmbThreadCount.IsEnabled = true;
        }

        private void Multithreading_Unchecked(object sender, RoutedEventArgs e)
        {
            cmbThreadCount.IsEnabled = false;
        }

        private void GUIupdateDefault(object sender, RoutedEventArgs e)
        {
            GUIupdateAdress.Text = "123";
        }

        private void RepkgupdateDefault(object sender, RoutedEventArgs e)
        {
            RepkgupdateAdress.Text = "123";
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            SetAllTagsChecked(true);
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            SetAllTagsChecked(false);
        }

        private void SetAllTagsChecked(bool isChecked)
        {
            if (TagsListPanel == null) return;

            bool stateChanged = false;
            foreach (var cb in TagsListPanel.Children.OfType<CheckBox>())
            {
                if (cb.IsChecked != isChecked)
                {
                    cb.IsChecked = isChecked;
                    stateChanged = true;
                }
            }

            if (stateChanged)
            {
                ApplyFilters();
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e) { }
        private void CheckBox_Checked_1(object sender, RoutedEventArgs e) { }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e) { }
        private void Button_Click(object sender, RoutedEventArgs e) { }
        private void Button_Click_1(object sender, RoutedEventArgs e) { }
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void TabControl_SelectionChanged_1(object sender, SelectionChangedEventArgs e) { }
        private void TabControl_SelectionChanged_2(object sender, SelectionChangedEventArgs e) { }
        private void TabControl_SelectionChanged_3(object sender, SelectionChangedEventArgs e) { }
        private void Button_Click_2(object sender, RoutedEventArgs e) { }
        private void Button_Click_3(object sender, RoutedEventArgs e) { }
        private void Button_Click_4(object sender, RoutedEventArgs e) { }
        private void Button_Click_5(object sender, RoutedEventArgs e) { }
        private void SingleChoice_Click(object sender, RoutedEventArgs e) { }

        private void WallpaperSearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {

        }
    }
}
