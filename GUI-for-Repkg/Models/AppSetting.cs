using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;

namespace GUI_for_RePKG.Models
{
    public class AppSettings
    {
        public bool UseProjectNameForFile { get; set; } = false;
        public bool JustSaveImages { get; set; } = false;
        public bool DontTransformTexFiles { get; set; } = false;
        public bool PutAllFilesInOneDirectory { get; set; } = false;
        public bool CoverAllFiles { get; set; } = false;
        public bool CopyProjectJson { get; set; } = false;
        public bool multithreading { get; set; } = true;
        public bool multithreadingEnabled { get; set; } = true;
        public int multithreadingNum { get; set; } = 0;
        public string WallpapersFile { get; set; } = "";
    }

    public static class ConfigManager
    {
        private static bool suppressElevationPromptFlag = false;

        private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "config.json");
        public static void Save(AppSettings settings)
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            try
            {
                File.WriteAllText(FilePath, json);
            }
            catch (UnauthorizedAccessException)
            {
                if (suppressElevationPromptFlag == true)
                {
                    return;
                }
                HandlePrivilegeElevation();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}");
            }
        }

        private static void HandlePrivilegeElevation()
        {
            if (IsRunningAsAdmin())
            {
                MessageBox.Show("已经是管理员权限，但仍无法写入文件。请检查文件是否被占用或设为只读。");
                return;
            }

            var result = MessageBox.Show(
                "保存设置需要管理员权限。是否以管理员身份重启应用？",
                "权限受限",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                RestartAsAdmin();
            }
            if (result == MessageBoxResult.No)
            {
                MessageBox.Show("操作已取消，设置将不保存");
                suppressElevationPromptFlag = true;
            }
        }

        private static bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static void RestartAsAdmin()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Process.GetCurrentProcess().MainModule.FileName,
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start(startInfo);
                Application.Current.Shutdown();
            }
            catch
            {
                MessageBox.Show("操作已取消，设置将不保存");
            }
        }

        public static AppSettings Load()
        {
            if (!File.Exists(FilePath)) return new AppSettings();

            try
            {
                string json = File.ReadAllText(FilePath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}
