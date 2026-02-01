using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace GUI_for_Repkg.Models
{
    internal class ProcessLauncher
    {
        public struct ProcessProgressReport
        {
            public int CompletedCount { get; set; }
            public int TotalCount { get; set; }
            public string Message { get; set; }
            public double Percentage => TotalCount > 0 ? (double)CompletedCount / TotalCount * 100 : 0;
        }

        private static string GetSafeFolderName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                rawName = "未命名";

            //替换Windows文件夹名非法字符
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                rawName = rawName.Replace(c, '_');
            }

            //去除首尾空格和点
            rawName = rawName.Trim().TrimEnd('.');

            if (string.IsNullOrEmpty(rawName))
                rawName = "未命名";

            return $"{rawName}";
        }

        public static async Task LaunchAsync(
            IEnumerable<string> folders,
            string exePath,
            string outputRootPath,
            int maxDegreeOfParallelism,
            bool wallpaperengineOutputMode,
            CancellationToken token,
            ManualResetEventSlim pauseEvent,
            IProgress<ProcessProgressReport> progressReport = null)
        {
            var folderList = new List<string>(folders);
            if (folderList.Count == 0) return;
            int completed = 0;
            int total = folderList.Count;

            progressReport?.Report(new ProcessProgressReport
            {
                CompletedCount = 0,
                TotalCount = total,
                Message = $"开始处理 {total} 个壁纸，使用 {maxDegreeOfParallelism} 线程..."
            });

            var mainWindow = Application.Current.MainWindow as MainWindow;

            //判断是否用壁纸名做文件夹名
            bool useProjectName = mainWindow?.UseProjectNameForFile.IsChecked == true;
            //只转换Tex
            string justSaveImages = mainWindow?.JustSaveImages.IsChecked == true ? "-e Tex" : "";
            //不转换Tex文件
            string dontTransfornTexFiles = mainWindow?.DontTransformTexFiles.IsChecked == true ? " --no-tex-convert" : "";
            //将所有文件放在一个文件夹中
            string putAllFilesInOneFolder = mainWindow?.PutAllFilesInOneDirectory.IsChecked == true ? " -s" : "";
            //覆盖所有文件
            string coverAllFiles = mainWindow?.CoverAllFiles.IsChecked == true ? " --overwrite" : "";
            //将project.json文件复制到输出文件夹
            string copyProjectJson = mainWindow?.CopyProjectJson.IsChecked == true ? " -c" : "";
            //判断是否从“已有壁纸”导入
            string outputMode = wallpaperengineOutputMode ? "\\scene.pkg" : "";

            string SettingOptions = justSaveImages + dontTransfornTexFiles + putAllFilesInOneFolder + coverAllFiles + copyProjectJson;

            await Task.Run(() =>
            {
                _ = Parallel.ForEach(folderList, new ParallelOptions
                {
                    MaxDegreeOfParallelism = maxDegreeOfParallelism,
                    CancellationToken = token,
                }, folder =>
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        pauseEvent.Wait(token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }

                    try
                    {
                        string workshopId = wallpaperengineOutputMode == false ? Path.GetFileNameWithoutExtension(folder) : Path.GetFileName(folder);
                        string rawTitle = "未命名";
                        if (wallpaperengineOutputMode)
                        {
                            //安全读取project.json的title（带异常保护）
                            string jsonPath = Path.Combine(folder, "project.json");
                            if (File.Exists(jsonPath))
                            {
                                try
                                {
                                    var json = JObject.Parse(File.ReadAllText(jsonPath));
                                    rawTitle = json["title"]?.ToString()?.Trim() ?? "未命名";
                                }
                                catch
                                {
                                    //JSON损坏或解析失败时回退
                                    rawTitle = "未命名";
                                }
                            }
                        }

                        bool isMPKG = false;
                        string oldFileName = null;
                        if (!wallpaperengineOutputMode)
                        {
                            if (folder.EndsWith(".mpkg", StringComparison.OrdinalIgnoreCase))
                            {
                                var newFolder = Path.ChangeExtension(folder, ".pkg");
                                oldFileName = folder;

                                try
                                {
                                    File.Move(folder, newFolder);
                                    folder = newFolder;
                                }
                                catch (IOException ex)
                                {
                                    MessageBox.Show($"转换.mpkg文件失败，程序在对其重命名时发生错误：{ex.Message}");
                                }
                                isMPKG = true;
                            }
                        }

                        string tempFolderName = workshopId;
                        string tempOutputPath = Path.Combine(outputRootPath, tempFolderName);

                        Directory.CreateDirectory(tempOutputPath);

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            Arguments = $"extract -o \"{tempOutputPath}\" {SettingOptions} \"{folder}{outputMode}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var process = Process.Start(startInfo))
                        {
                            if (process != null)
                            {
                                try
                                {
                                    JobObjectManager.AddProcess(process.Handle);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Job绑定失败:{ex.Message}");
                                }
                                process.WaitForExit();
                            }
                        }

                        if (isMPKG)
                        {
                            File.Move(folder, oldFileName);
                        }

                        string finalFolderName = tempFolderName;
                        if (useProjectName)
                        {
                            string safeTitle = GetSafeFolderName(rawTitle);

                            //处理重名冲突：自动加(1),(2)...
                            string candidateName = safeTitle;
                            int suffix = 1;
                            string candidatePath = Path.Combine(outputRootPath, candidateName);

                            while (Directory.Exists(candidatePath) && !string.Equals(candidatePath, tempOutputPath, StringComparison.OrdinalIgnoreCase))
                            {
                                candidateName = $"{safeTitle} ({suffix++})";
                                candidatePath = Path.Combine(outputRootPath, candidateName);
                            }

                            if (!string.Equals(candidatePath, tempOutputPath, StringComparison.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    Directory.Move(tempOutputPath, candidatePath);
                                    finalFolderName = candidateName;
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"重命名失败 ({rawTitle} → {candidateName}): {ex.Message}");
                                    //重命名失败时保持原ID文件夹，不影响后续处理
                                    finalFolderName = workshopId;
                                }
                            }
                            else
                            {
                                finalFolderName = candidateName;
                            }
                        }

                        int current = Interlocked.Increment(ref completed);

                        progressReport?.Report(new ProcessProgressReport
                        {
                            CompletedCount = completed,
                            TotalCount = total,
                            Message = $"正在处理：{Path.GetFileName(folder)}",
                        });
                    }
                    catch (AggregateException ae)
                    {
                        //检查里面是否包含“取消异常”
                        //Flatten()会把嵌套的异常摊平
                        ae.Handle(ex =>
                        {
                            return ex is OperationCanceledException; //如果是取消异常，视为“已处理”
                        });

                        throw new OperationCanceledException();
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                });
            });
            if (!token.IsCancellationRequested)
            {
                progressReport?.Report(new ProcessProgressReport
                {
                    CompletedCount = total,
                    TotalCount = total,
                    Message = $"全部完成！共处理 {total} 个壁纸。"
                });
            }
        }
    }
}