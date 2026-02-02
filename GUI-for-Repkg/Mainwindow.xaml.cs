using GUI_for_Repkg.Models;
using GUI_for_RePKG.Models;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
        private bool isInitializing = true;
        private AppSettings _settings;

        private const double ShadowDisableThreshold = 1500;
        private bool _isShadowEnabled = true;

        string repkgExePath = @"RePKG.exe";
        private List<ThumbnailInfo> allThumbnails = new List<ThumbnailInfo>();

        private static readonly SolidColorBrush NativeGreen = new SolidColorBrush(Color.FromRgb(6, 176, 37));  //绿
        private static readonly SolidColorBrush NativeYellow = new SolidColorBrush(Color.FromRgb(255, 189, 0)); //黄
        private static readonly SolidColorBrush NativeRed = new SolidColorBrush(Color.FromRgb(204, 0, 0));    //红

        public event EventHandler ProcessFinished;

        public List<int> ThreadNumbers { get; set; } = new List<int>();

        public bool isMultiSelectMode = false;

        private const double MinItemWidth = 150;

        private CancellationTokenSource _cts;
        private ManualResetEventSlim _pauseEvent;
        private bool _isPaused = false;

        private HashSet<string> _pendingFiles = new HashSet<string>();

        public MainWindow()
        {
            isInitializing = true;
            InitializeComponent();

            PopulateThreadCount();
            LoadConfigToUI();
            ReadGUIversion();
            ReadRePKGversion(repkgExePath);
            ReadWallpaperEngineAdress();
            DefaultOutputPathSetting();

            this.Loaded += MainWindow_Loaded;
            this.SizeChanged += MainWindow_SizeChanged;
            ThumbnailScrollViewer.SizeChanged += (s, e) => UpdateThumbnailSize();

            isInitializing = false;
        }

        private void LoadConfigToUI()
        {
            _settings = ConfigManager.Load();

            UseProjectNameForFile.IsChecked = _settings.UseProjectNameForFile;
            JustSaveImages.IsChecked = _settings.JustSaveImages;
            DontTransformTexFiles.IsChecked = _settings.DontTransformTexFiles;
            PutAllFilesInOneDirectory.IsChecked = _settings.PutAllFilesInOneDirectory;
            CoverAllFiles.IsChecked = _settings.CoverAllFiles;
            CopyProjectJson.IsChecked = _settings.CopyProjectJson;
            WallpapersFile.Text = _settings.WallpapersFile;
        }
        private void SettingChanged_Save(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            if (_settings == null) _settings = new AppSettings();

            _settings.UseProjectNameForFile = UseProjectNameForFile.IsChecked ?? false;
            _settings.JustSaveImages = JustSaveImages.IsChecked ?? false;
            _settings.DontTransformTexFiles = DontTransformTexFiles.IsChecked ?? false;
            _settings.PutAllFilesInOneDirectory = PutAllFilesInOneDirectory.IsChecked ?? false;
            _settings.CoverAllFiles = CoverAllFiles.IsChecked ?? false;
            _settings.CopyProjectJson = CopyProjectJson.IsChecked ?? false;
            _settings.multithreading = multithreading.IsChecked ?? false;
            _settings.multithreadingEnabled = cmbThreadCount.IsEnabled;

            if (cmbThreadCount.SelectedItem is int selected)
            {
                _settings.multithreadingNum = selected;
            }
            else if (ThreadNumbers != null && ThreadNumbers.Count > 0)
            {
                _settings.multithreadingNum = ThreadNumbers.First();
            }
            else
            {
                _settings.multithreadingNum = 1;
            }

            _settings.WallpapersFile = WallpapersFile.Text;

            ConfigManager.Save(_settings);
        }

        private void ReadGUIversion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            GUIVersion.Text = $"{version.Major}.{version.Minor}.{version.Build}";
            GUIdeveloper.Text = "ReZe20";
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

        private void PopulateThreadCount()
        {
            int logicalCores = Environment.ProcessorCount;

            if (logicalCores > 1)
            {
                _settings ??= ConfigManager.Load();
                multithreading.IsChecked = _settings.multithreading;

                int max = Math.Max(logicalCores, 2);

                ThreadNumbers = Enumerable.Range(2, max - 1).ToList();
                cmbThreadCount.ItemsSource = ThreadNumbers;
                if (_settings.multithreadingNum < 2 || _settings.multithreadingNum > max)
                    cmbThreadCount.SelectedItem = max;
                else
                    cmbThreadCount.SelectedItem = _settings.multithreadingNum;

                cmbThreadCount.IsEnabled = _settings.multithreadingEnabled;
            }
            else
            {
                multithreading.IsChecked = false;
                multithreading.IsEnabled = false;
                cmbThreadCount.Visibility = Visibility.Collapsed;
            }
        }

        private void ReadWallpaperEngineAdress()
        {
            _settings = ConfigManager.Load();
            if (_settings.WallpapersFile == "")
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
            else
            {
                WallpapersFile.Text = _settings.WallpapersFile;
            }
        }

        private void DefaultOutputPathSetting()
        {
            try
            {
                OutputPathBox.Text = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "GUI-for-Repkg_Output");
            }
            catch
            {
                OutputPathBox.Text = "";
            }
        }

        private void UpdateProgressState(string state)
        {
            switch (state)
            {
                case "Normal":
                    ConversionProgressBar1.Foreground = NativeGreen;
                    ConversionProgressBar2.Foreground = NativeGreen;
                    TaskBarProgress.ProgressState = TaskbarItemProgressState.Normal;
                    break;
                case "Paused":
                    ConversionProgressBar1.Foreground = NativeYellow;
                    ConversionProgressBar2.Foreground = NativeYellow;
                    TaskBarProgress.ProgressState = TaskbarItemProgressState.Paused;
                    break;
                case "Error":
                    ConversionProgressBar1.Foreground = NativeRed;
                    ConversionProgressBar2.Foreground = NativeRed;
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
                bool shouldEnableShadow = this.ActualWidth < ShadowDisableThreshold;
                if (shouldEnableShadow != _isShadowEnabled)
                {
                    _isShadowEnabled = shouldEnableShadow;
                    if (_isShadowEnabled) EnableAllThumbnailShadows();
                    else DisableAllThumbnailShadows();
                }

                //调用新的自适应图片大小方法
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

            if (e.OriginalSource is DependencyObject originalSource)
            {
                var parent = originalSource;
                while (parent != null && parent != grid)
                {
                    if (parent is CheckBox) return;
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            var checkBox = grid.Children.OfType<CheckBox>().FirstOrDefault();
            if (checkBox == null) return;

            string folderPath = checkBox.Tag as string;
            if (string.IsNullOrEmpty(folderPath)) return;

            if (isMultiSelectMode)
            {
                checkBox.IsChecked = !checkBox.IsChecked;
                LoadDetail(folderPath);
                UpdateSelectedCountDisplay();
            }
            else
            {

                foreach (Grid otherGrid in ThumbnailPanel.Children.OfType<Grid>())
                {
                    var otherCb = otherGrid.Children.OfType<CheckBox>().FirstOrDefault();
                    if (otherCb != null && otherGrid != grid)
                    {
                        otherCb.IsChecked = false;
                        otherCb.Visibility = Visibility.Collapsed;

                        var otherBorder = otherGrid.Children.OfType<Border>().FirstOrDefault();
                        if (otherBorder != null)
                        {
                            otherBorder.BorderThickness = new Thickness(0);
                            otherBorder.BorderBrush = Brushes.Transparent;
                        }
                    }
                }

                //选中当前项
                checkBox.IsChecked = true;

                //加载详情
                LoadDetail(folderPath);
            }
            //更新当前项的视觉（边框等）
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

            await LoadThumbnailsAutomatically();
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

                //回到UI线程创建控件
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

            //根据选中状态设置边框高亮
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
            //切换模式后更新所有视觉状态（主要是复选框可见性）
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
                string jsonPath = System.IO.Path.Combine(folderPath, "project.json");
                if (!File.Exists(jsonPath)) return;

                JObject json = JObject.Parse(File.ReadAllText(jsonPath));

                string title = json["title"]?.ToString() ?? "无标题";
                string description = json["description"]?.ToString() ?? "无描述";
                string previewFile = json["preview"]?.ToString();

                DetailTitle.Text = title;
                DetailWorkshopID.Text = new DirectoryInfo(folderPath).Name;

                if (!string.IsNullOrEmpty(previewFile))
                {
                    string fullPreviewPath = System.IO.Path.Combine(folderPath, previewFile);
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

        private void StartProcess_Click(object sender, RoutedEventArgs e)
        {
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
            StartProcess(sender, e, outputPath);
        }

        private void ImportIntoEditor_Click(object sender, RoutedEventArgs e)
        {
            string EditoroutputPath = TruncateToLastChar(WallpapersFile.Text, "\\workshop\\content\\431960") + "\\common\\wallpaper_engine\\projects\\myprojects";

            bool a = false;
            bool b = false;
            if (UseProjectNameForFile.IsChecked == false)
            {
                UseProjectNameForFile.IsChecked = true;
                a = true;
            }
            if (CopyProjectJson.IsChecked == false)
            {
                CopyProjectJson.IsChecked = true;
                b = true;
            }

            if (!Directory.Exists(EditoroutputPath))
            {
                MessageBox.Show("未找到目录，请检查！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            StartProcess(sender, e, EditoroutputPath);

            this.ProcessFinished += (s, args) =>
            {
                if (a)
                {
                    UseProjectNameForFile.IsChecked = false;
                    a = false;
                }
                if (b)
                {
                    CopyProjectJson.IsChecked = false;
                    b = false;
                }
            };
        }

        private async void StartProcess(object sender, RoutedEventArgs e, string outputPath)
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

            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true); //true表示初始状态为“运行中”
            _isPaused = false;

            ToggleControlButtons(true);
            UnenabledStartButtonAndSettingCheckbox();

            int threadCount = multithreading.IsChecked == true ? (int)cmbThreadCount.SelectedItem : 1;

            SituationPresentation.Text = "准备启动处理...";

            var progressReport = new Progress<ProcessProgressReport>(report =>
            {
                //Progress<T>会自动在UI线程上调用，这里不需要再Dispatcher.Invoke
                SituationPresentation.Text = report.Message;
                ConversionProgressBar1.Value = report.Percentage;
                ConversionProgressBar2.Value = report.Percentage;
                ConversionProgressBar1.Foreground = NativeGreen;
                ConversionProgressBar2.Foreground = NativeGreen;
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
                    true,
                    _cts.Token,
                    _pauseEvent,
                    progressReport);

                MessageBox.Show("所有壁纸处理完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                SituationPresentation.Text = "提取已停止";
                UpdateProgressState("Error");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理过程中发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                SituationPresentation.Text = "处理失败";
                UpdateProgressState("Error");
            }
            finally
            {
                UnenabledStartButtonAndSettingCheckbox();
                ToggleControlButtons(false);
                ProcessFinished?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_pauseEvent != null && !_isPaused)
            {
                _pauseEvent.Reset();
                _isPaused = true;
                SituationPresentation.Text = "任务已暂停...";

                BtnPause1.IsEnabled = false;
                BtnPause2.IsEnabled = false;

                BtnResume1.IsEnabled = true;
                BtnResume2.IsEnabled = true;
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

                BtnPause1.IsEnabled = true;
                BtnPause2.IsEnabled = true;

                BtnResume1.IsEnabled = false;
                BtnResume2.IsEnabled = false;
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
            BtnStop1.IsEnabled = isRunning;
            BtnStop2.IsEnabled = isRunning;

            BtnPause1.IsEnabled = isRunning;
            BtnPause2.IsEnabled = isRunning;

            BtnResume1.IsEnabled = false;
            BtnResume2.IsEnabled = false;
        }

        private static string TruncateToLastChar(string inputStr, string suffixToRemove)
        {
            if (string.IsNullOrEmpty(inputStr) || string.IsNullOrEmpty(suffixToRemove))
                return inputStr;

            if (inputStr.EndsWith(suffixToRemove))
                return inputStr.Substring(0, inputStr.Length - suffixToRemove.Length);

            return inputStr;
        }

        private void SettleOutputPathBox(object sender, RoutedEventArgs e)
        {
            bool isPathValid = false;

            while (!isPathValid)
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "选择文件夹",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
                };

                if (dialog.ShowDialog() == true)
                {
                    if (dialog.FolderName.Contains(" "))
                    {
                        MessageBox.Show($"请勿选择带有空格路径", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        isPathValid = true;
                        OutputPathBox.Text = dialog.FolderName;
                    }
                }
                else break;
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
            //如果还没加载过壁纸，直接返回
            if (allThumbnails == null || allThumbnails.Count == 0) return;

            //获取搜索框文本
            string searchText = WallpaperSearchBox.Text.Trim().ToLower();

            //获取选中的(遍历Expander里的CheckBox)
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

            //获取选中的标签
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

            //INQ 查询过滤
            var filteredList = allThumbnails.Where(item =>
            {
                //搜索文本
                bool matchSearch = string.IsNullOrEmpty(searchText) ||
                                   item.Title.ToLower().Contains(searchText);

                //分级过滤 (强力兼容模式)
                string itemRating = string.IsNullOrEmpty(item.ContentRating) ? "everyone" : item.ContentRating.ToLower();
                //确保 checkedRatings 里的值也都是小写
                bool matchRating = checkedRatings.Any(r => r.Equals(itemRating, StringComparison.OrdinalIgnoreCase));

                //标签过滤
                bool matchTags = false;
                if (item.Tags == null || item.Tags.Count == 0)
                {
                    // 没标签的壁纸，只要勾选了“未指定”就显示
                    matchTags = checkedTags.Any(t => t.Equals("Unspecified", StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    //只要壁纸拥有的标签中，有一个是在已选列表里的，就返回 true
                    matchTags = item.Tags.Any(t => checkedTags.Contains(t));
                }

                return matchSearch && matchRating && matchTags;
            }).ToList();

            //更新 UI
            ThumbnailLoader.CreateThumbnailControls(
                filteredList,
                ThumbnailPanel,
                isMultiSelectMode,
                ThumbnailItem_PreviewMouseLeftButtonDown,
                ThumbnailCheckBox_Click);

            //更新底部状态栏文字
            SituationPresentation.Text = $"筛选显示 {filteredList.Count} / {allThumbnails.Count} 个壁纸";

            //处理阴影
            if (_isShadowEnabled) EnableAllThumbnailShadows();
            else DisableAllThumbnailShadows();
        }

        private void WallpaperFile_PreciewDragover(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);

                //检查第一个元素是否为文件
                if (paths != null && paths.Length > 0 && File.Exists(paths[0]))
                {
                    e.Effects = DragDropEffects.Copy; //显示“复制”图标
                }
                else
                {
                    e.Effects = DragDropEffects.None; //显示“禁止”图标
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            // 标记为已处理，防止 TextBox 原生逻辑干扰
            e.Handled = true;
        }

        private void WallpaperFile_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Contains(" "))
                {
                    MessageBox.Show(
                    $"拖入的路径包含空格：\n\n\"{files}\"\n\nRePKG 无法处理带空格的路径，请移动文件夹后再拖入。",
                    "路径非法",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                    return;
                }
                if (files.Length > 0)
                {
                    string droppedPath = files[0];
                    OneWallpaperFile.Text = droppedPath;
                    foreach (var t in files)
                    {
                        AddFileToUIList(t);
                    }
                }
            }
        }

        private void OneWallpaperFileBoxChanged(object sender, TextChangedEventArgs e)
        {
            if (System.IO.Path.IsPathRooted(OneWallpaperFile.Text))
            {
                OneOutputPath.Text = System.IO.Path.GetDirectoryName(OneWallpaperFile.Text) + "\\Output";
                TurnOffWallpaperFolder();
            }
            else
            {
                OneOutputPath.Text = "";
                TurnOnWallpaperFolder();
            }
        }

        private void ChooseOneWallpaper_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Wallpaper Engine 包文件 (*.pkg;*.mpkg)|*.pkg;*.mpkg"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!file.Contains(" ")) AddFileToUIList(file);
                    else MessageBox.Show($"跳过含空格路径: {file}");
                }
            }
        }

        private void AddFileToUIList(string filePath)
        {
            if (_pendingFiles.Contains(filePath)) return;
            _pendingFiles.Add(filePath);

            Border itemBorder = new Border
            {
                Margin = new Thickness(0, 2, 0, 2),
                Padding = new Thickness(10, 5, 5, 5),
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                Tag = filePath
            };

            DockPanel dock = new DockPanel { LastChildFill = true };

            Button btnDelete = new Button
            {
                Content = "❌",
                Width = 24,
                Height = 24,
                Margin = new Thickness(5, 0, 0, 0),
                Background = Brushes.Transparent,
                Foreground = Brushes.Gray,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = Cursors.Hand
            };
            DockPanel.SetDock(btnDelete, Dock.Right);

            btnDelete.Click += (s, e) =>
            {
                _pendingFiles.Remove(filePath);
                SelectedFilesPanel.Children.Remove(itemBorder);
            };

            //左侧：文件名显示
            TextBlock txtName = new TextBlock
            {
                Text = filePath,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
            };

            dock.Children.Add(btnDelete);
            dock.Children.Add(txtName);
            itemBorder.Child = dock;

            SelectedFilesPanel.Children.Add(itemBorder);
        }

        private void ChooseOneWallpaperOutputPath(object sender, RoutedEventArgs e)
        {
            bool isFileValid = false;

            while (!isFileValid)
            {
                OpenFolderDialog openFileDialog = new OpenFolderDialog();

                openFileDialog.Title = "请选择输出目录";

                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedFile = openFileDialog.FolderName;
                    if (selectedFile.Contains(" "))
                    {
                        MessageBox.Show(
                            $"请勿选择包含空格的文件夹路径：\n{selectedFile}\n",
                            "路径不合法",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        isFileValid = true;
                        OneOutputPath.Text = selectedFile;
                    }
                }
                else return;
            }
        }

        private void WallpapersFolder_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] folders = (string[])e.Data.GetData(DataFormats.FileDrop);
                string droppedPath = folders[0];

                if (droppedPath.Contains(" "))
                {
                    MessageBox.Show("根路径不能含空格");
                    return;
                }

                MultipleWallpaperFiles.Text = droppedPath;

                var files = Directory.GetFiles(droppedPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".pkg") || f.EndsWith(".mpkg"));

                foreach (var f in files)
                {
                    AddFileToUIList(f);
                }
            }
        }

        private void WallpaperFolder_PreciewDragover(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);

                //检查第一个元素是否为文件夹
                if (paths != null && paths.Length > 0 && !File.Exists(paths[0]))
                {
                    e.Effects = DragDropEffects.Copy;//显示“复制”图标
                }
                else
                {
                    e.Effects = DragDropEffects.None;//显示“禁止”图标
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void ChooseOneWallpaperFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                string root = dialog.FolderName;
                if (root.Contains(" ")) { MessageBox.Show("根路径不能含空格"); return; }

                var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".pkg") || f.EndsWith(".mpkg"));

                foreach (var f in files)
                {
                    AddFileToUIList(f);
                }
            }
        }

        private void ChooseOneWallpaperFolderOutputPath(object sender, RoutedEventArgs e)
        {
            bool isFileValid = false;

            while (!isFileValid)
            {
                OpenFolderDialog openFileDialog = new OpenFolderDialog();

                openFileDialog.Title = "请选择输出目录";

                if (openFileDialog.ShowDialog() == true)
                {
                    string selectedFile = openFileDialog.FolderName;
                    if (selectedFile.Contains(" "))
                    {
                        MessageBox.Show(
                            $"请勿选择包含空格的文件夹路径：\n{selectedFile}\n",
                            "路径不合法",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        isFileValid = true;
                        MultipleWallpaperFilesOutputPath.Text = selectedFile;
                    }
                }
                else return;
            }
        }

        private void MultipleWallpaperFilesBoxChanged(object sender, TextChangedEventArgs e)
        {
            if (System.IO.Path.IsPathRooted(MultipleWallpaperFiles.Text))
            {
                MultipleWallpaperFilesOutputPath.Text = MultipleWallpaperFiles.Text + "\\Output";
            }
            else
            {
                MultipleWallpaperFilesOutputPath.Text = "";
            }
        }

        private void FolderOutputChangedCheck(object sender, TextChangedEventArgs e)
        {
            if (MultipleWallpaperFilesOutputPath.Text != "")
            {
                TurnOffWallpaperFile();
            }
            else TurnOnWallpaperFile();
        }

        private void FileOutputChangedCheck(object sender, TextChangedEventArgs e)
        {
            if (OneOutputPath.Text != "")
            {
                TurnOffWallpaperFolder();
            }
            else TurnOnWallpaperFolder();
        }

        private async void StartConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingFiles.Count == 0)
            {
                MessageBox.Show($"队列中没有任务！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!File.Exists(repkgExePath))
            {
                MessageBox.Show($"找不到{repkgExePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string outputPath = "";
            if (OneOutputPath.Text == "" && MultipleWallpaperFilesOutputPath.Text == "")
            {
                MessageBox.Show($"没有输出目录！", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            else
            {
                outputPath = OneOutputPath.Text != "" ? OneOutputPath.Text : MultipleWallpaperFilesOutputPath.Text;
            }

            _cts = new CancellationTokenSource();
            _pauseEvent = new ManualResetEventSlim(true);
            _isPaused = false;

            UpdateProgressState("Normal");
            ToggleControlButtons(true);
            UnenabledStartButtonAndSettingCheckbox();

            int threadCount = multithreading.IsChecked == true ? (int)cmbThreadCount.SelectedItem : 1;

            var progressreport = new Progress<ProcessProgressReport>(report =>
            {
                SituationPresentation.Text = report.Message;
                ConversionProgressBar1.Value = report.Percentage;
                ConversionProgressBar2.Value = report.Percentage;
                ConversionProgressBar1.Foreground = NativeGreen;
                ConversionProgressBar2.Foreground = NativeGreen;
                TaskBarProgress.ProgressValue = report.Percentage / 100;
                SelectedCount.Text = $"已完成: {report.CompletedCount}/{report.TotalCount}";
            });

            try
            {
                await ProcessLauncher.LaunchAsync(
                    _pendingFiles.ToList(),
                    repkgExePath,
                    outputPath,
                    threadCount,
                    false,
                    _cts.Token,
                    _pauseEvent,
                    progressreport);

                MessageBox.Show("转换队列执行完毕！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                SituationPresentation.Text = "任务已被用户停止";
                UpdateProgressState("Error");
                UnenabledStartButtonAndSettingCheckbox();
                ToggleControlButtons(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"处理过程中出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateProgressState("Error");
                UnenabledStartButtonAndSettingCheckbox();
                ToggleControlButtons(false);
            }
            finally
            {
                UnenabledStartButtonAndSettingCheckbox();
                ToggleControlButtons(false);
                SelectedFilesPanel.Children.Clear();
                _pendingFiles.Clear();
            }
        }

        private void Cleanqueue_Click(object sender, RoutedEventArgs e)
        {
            SelectedFilesPanel.Children.Clear();
            _pendingFiles.Clear();
        }

        private void TurnOffWallpaperFile()
        {
            OneWallpaperFile.IsEnabled = false;
            OneOutputPath.IsEnabled = false;
            OneWallpaperChooseButton.IsEnabled = false;
            OneWallpaperOutPathButton.IsEnabled = false;
        }
        private void TurnOnWallpaperFile()
        {
            OneWallpaperFile.IsEnabled = true;
            OneOutputPath.IsEnabled = true;
            OneWallpaperChooseButton.IsEnabled = true;
            OneWallpaperOutPathButton.IsEnabled = true;
        }
        private void TurnOffWallpaperFolder()
        {
            MultipleWallpaperFiles.IsEnabled = false;
            MultipleWallpaperFilesOutputPath.IsEnabled = false;
            FolderChooseButton.IsEnabled = false;
            FolderOutputPathButton.IsEnabled = false;
        }
        private void TurnOnWallpaperFolder()
        {
            MultipleWallpaperFiles.IsEnabled = true;
            MultipleWallpaperFilesOutputPath.IsEnabled = true;
            FolderChooseButton.IsEnabled = true;
            FolderOutputPathButton.IsEnabled = true;
        }

        private void UnenabledStartButtonAndSettingCheckbox()
        {
            UseProjectNameForFile.IsEnabled = (!UseProjectNameForFile.IsEnabled);
            JustSaveImages.IsEnabled = (!JustSaveImages.IsEnabled);
            DontTransformTexFiles.IsEnabled = (!DontTransformTexFiles.IsEnabled);
            PutAllFilesInOneDirectory.IsEnabled = (!PutAllFilesInOneDirectory.IsEnabled);
            CoverAllFiles.IsEnabled = (!CoverAllFiles.IsEnabled);
            CopyProjectJson.IsEnabled = (!CopyProjectJson.IsEnabled);
            multithreading.IsEnabled = (!multithreading.IsEnabled);
            cmbThreadCount.IsEnabled = (!cmbThreadCount.IsEnabled);
            StartProcessButton.IsEnabled = (!StartProcessButton.IsEnabled);
            StartConvertButton.IsEnabled = (!StartConvertButton.IsEnabled);
            ImportIntoEditor.IsEnabled = (!ImportIntoEditor.IsEnabled);
        }

        private void WallpapersFilePathChanged(object sender, TextChangedEventArgs e)
        {
            SettingChanged_Save(sender, e);

            string currentText = ((TextBox)sender).Text;
            if (!string.IsNullOrWhiteSpace(currentText))
            {
                LoadThumbnailsAutomatically();
            }
        }

        private void RestoreDefaultWallpapersPath_Click(object sender, RoutedEventArgs e)
        {
            _settings.WallpapersFile = "";
            ConfigManager.Save(_settings);
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

        private void GUIGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/ReZe20/GUI-for-RePKG";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开链接出现错误", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void GUIBilibiliButton_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://space.bilibili.com/504365497";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开链接出现错误", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GUIGiteeButton_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://gitee.com/Re-Ze/gui-for-re-pkg";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开链接出现错误", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RePKGGitHubButton_Click(object sender, RoutedEventArgs e)
        {
            string url = "https://github.com/notscuffed/repkg";
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开链接出现错误", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
