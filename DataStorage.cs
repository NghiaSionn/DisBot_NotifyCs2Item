using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System;

namespace CS2PriceBot
{
    public class BotConfig
    {
        public string Token { get; set; } = "YOUR_DISCORD_BOT_TOKEN_HERE";
        public ulong? NotificationChannelId { get; set; } = null;
        public int CheckIntervalMinutes { get; set; } = 30;
        public bool EnableCS2Notifications { get; set; } = true;
        public bool EnableUnityNotifications { get; set; } = false;
        public bool NotifyOnlyOnIncrease { get; set; } = false;
        public string? LastUnityGiftTitle { get; set; } = null;
        public List<string> TrackedItems { get; set; } = new List<string>();
        public Dictionary<string, double> LastKnownPrices { get; set; } = new Dictionary<string, double>();
    }

    public class DataStorage
    {
        private readonly string _filePath = "bot_data.json";
        public BotConfig Config { get; private set; }

        public DataStorage()
        {
            Load();
        }

        public void Load()
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                Config = JsonSerializer.Deserialize<BotConfig>(json) ?? new BotConfig();
            }
            else
            {
                Config = new BotConfig();
                Save();
            }

            var envToken = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
            if (!string.IsNullOrWhiteSpace(envToken))
            {
                Config.Token = envToken;
            }
        }

        public void Save()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(Config, options));
        }
    }
}
