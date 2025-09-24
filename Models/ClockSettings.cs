using System.Text.Json;
using System.IO;

namespace ThreeMClock.Models
{
    public class ClockSettings
    {
        // General
        public string TopMostMode { get; set; } = "Always on Top"; // "Normal", "Always on Top", "Above Taskbar"
        public bool Use24Hour { get; set; } = false;
        public double Opacity { get; set; } = 1.0;

        // Position & Size
        public double Left { get; set; } = -1; // -1 indicates not set
        public double Top { get; set; } = -1;
        public double FontSize { get; set; } = 60;

        // Appearance
        public string FontColor { get; set; } = "#FFFFFF";
        public string FontFamily { get; set; } = "Segoe UI Variable Display";
        public string TextEffect { get; set; } = "Shadow";

        // Background
        public bool HasBackground { get; set; } = false;
        public string BackgroundColor { get; set; } = "#000000";
        public double BackgroundPaddingX { get; set; } = 10;
        public double BackgroundPaddingY { get; set; } = 5;

        public static string ConfigDir => Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "3MClock");
        private static string ConfigPath => Path.Combine(ConfigDir, "settings.json");

        public static ClockSettings Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var s = JsonSerializer.Deserialize<ClockSettings>(json);
                    if (s != null) return s;
                }
            }
            catch { }
            return new ClockSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}

