using Newtonsoft.Json;
using System.IO;

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
        private static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "config.json");
        public static void Save(AppSettings settings)
        {
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(FilePath, json);
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
