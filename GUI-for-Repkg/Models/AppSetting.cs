using System;
using System.IO;
using Newtonsoft.Json;

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
        public int multithreadingNum { get; set; } = 1;
        public bool LoadImageWhenStart { get; set; } = true;
        public string WallpapersFile { get; set; } = null;
    }

    public static class ConfigManager
    {
        private static readonly string FilePath = Path.Combine("config.json");
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
