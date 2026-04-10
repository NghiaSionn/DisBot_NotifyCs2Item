using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace CS2PriceBot
{
    public class PriceCheckerWorker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly DataStorage _storage;
        private readonly SteamMarketService _steamService;

        public PriceCheckerWorker(DiscordSocketClient client, DataStorage storage, SteamMarketService steamService)
        {
            _client = client;
            _storage = storage;
            _steamService = steamService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken); 

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_client.ConnectionState != Discord.ConnectionState.Connected)
                {
                    await Task.Delay(10000, stoppingToken);
                    continue;
                }

                if (!_storage.Config.EnableCS2Notifications)
                {
                    await Task.Delay(10000, stoppingToken);
                    continue;
                }

                var items = _storage.Config.TrackedItems.ToList();
                foreach (var item in items)
                {
                    try
                    {
                        var details = await _steamService.GetItemDetailsAsync(item);
                        double? currentPrice = details.Price;

                        if (currentPrice.HasValue)
                        {
                            bool hasOldPrice = _storage.Config.LastKnownPrices.TryGetValue(item, out double oldPrice);

                            if (!hasOldPrice || Math.Abs(oldPrice - currentPrice.Value) > 0.01)
                            {
                                bool isIncrease = currentPrice.Value > oldPrice;
                                bool shouldNotify = true;

                                if (hasOldPrice && _storage.Config.NotifyOnlyOnIncrease && !isIncrease)
                                {
                                    shouldNotify = false;
                                }

                                _storage.Config.LastKnownPrices[item] = currentPrice.Value;
                                _storage.Save();

                                if (shouldNotify)
                                {
                                    var guild = _client.Guilds.FirstOrDefault();
                                    if (guild != null)
                                    {
                                        SocketTextChannel channel = null;
                                        if (_storage.Config.NotificationChannelId.HasValue)
                                        {
                                            channel = guild.GetTextChannel(_storage.Config.NotificationChannelId.Value);
                                        }
                                        
                                        if (channel == null)
                                        {
                                            channel = guild.TextChannels.FirstOrDefault(c => c.Name.Equals("chung", StringComparison.OrdinalIgnoreCase) || c.Name.Equals("general", StringComparison.OrdinalIgnoreCase)) 
                                                      ?? guild.TextChannels.FirstOrDefault();
                                        }
                                        
                                        if (channel != null)
                                        {
                                            string direction = hasOldPrice ? (isIncrease ? "TĂNG LÊN 📈" : "GIẢM XUỐNG 📉") : "BẮT ĐẦU THEO DÕI 🔖";
                                            string oldPriceText = hasOldPrice ? $" (Giá cũ: {oldPrice:N0} VNĐ)" : "";
                                            
                                            var embed = new Discord.EmbedBuilder()
                                                .WithTitle($"🔖 {item}")
                                                .WithUrl($"https://steamcommunity.com/market/listings/730/{Uri.EscapeDataString(item)}")
                                                .WithThumbnailUrl(details.IconUrl)
                                                .WithColor(isIncrease ? Discord.Color.Green : (hasOldPrice ? Discord.Color.Red : Discord.Color.Blue))
                                                .AddField("Giá hiện tại", $"**{currentPrice.Value:N0} VNĐ**", inline: true)
                                                .AddField("Loại", details.Type, inline: true)
                                                .AddField("Biến động", direction + oldPriceText, inline: false)
                                                .WithFooter(footer => footer.WithText("CS2 Price Bot • " + DateTime.Now.ToString("HH:mm:ss")))
                                                .WithCurrentTimestamp()
                                                .Build();

                                            await channel.SendMessageAsync(embed: embed);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Worker error on item {item}: {ex.Message}");
                    }
                    await Task.Delay(3000, stoppingToken); 
                }

                Console.WriteLine($"[{DateTime.Now}] Đã hoàn tất đợt quét giá. Đợt tiếp theo sau {_storage.Config.CheckIntervalMinutes} phút.");
                await Task.Delay(TimeSpan.FromMinutes(_storage.Config.CheckIntervalMinutes), stoppingToken);
            }
        }
    }
}
