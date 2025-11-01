using BepInEx;
using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

namespace CloverLife
{
    internal class JsonConfig
    {
        public int JackpotSpeed { get; set; }
        public int WhenGambling { get; set; }
        public int Normal { get; set; }
        public int DiscardCharm { get; set; }
        public int WhenInCutscene { get; set; }

        public bool LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(Paths.ConfigPath, "CloverLife.json");

                if (!File.Exists(configPath))
                {
                    Debug.LogWarning($"Config file not found at {configPath}");
                    return false;
                }

                string json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<JsonConfig>(json);

                JackpotSpeed = config.JackpotSpeed;
                WhenGambling = config.WhenGambling;
                Normal = config.Normal;
                DiscardCharm = config.DiscardCharm;
                WhenInCutscene = config.WhenInCutscene;

                Debug.LogWarning("Config loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load config: {ex.Message}");
                return false;
            }
        }
    }
}
