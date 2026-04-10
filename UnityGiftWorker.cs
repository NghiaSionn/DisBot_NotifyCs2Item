using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace CS2PriceBot
{
    public class UnityGiftWorker : BackgroundService
    {
        private readonly DiscordSocketClient _client;
        private readonly DataStorage _storage;
        private readonly UnityAssetService _unityService;

        public UnityGiftWorker(DiscordSocketClient client, DataStorage storage, UnityAssetService unityService)
        {
            _client = client;
            _storage = storage;
            _unityService = unityService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait for bot to connect
            await Task.Delay(10000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_client.ConnectionState != ConnectionState.Connected)
                {
                    await Task.Delay(10000, stoppingToken);
                    continue;
                }

                if (_storage.Config.EnableUnityNotifications)
                {
                    try
                    {
                        var gift = await _unityService.GetCurrentGiftAsync();
                        if (gift != null && gift.Title != _storage.Config.LastUnityGiftTitle)
                        {
                            await NotifyNewGiftAsync(gift);
                            _storage.Config.LastUnityGiftTitle = gift.Title;
                            _storage.Save();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[UnityGiftWorker] Error: {ex.Message}");
                    }
                }

                // Check every 12 hours
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
        }

        private async Task NotifyNewGiftAsync(UnityGiftInfo gift)
        {
            var guild = _client.Guilds.FirstOrDefault();
            if (guild == null) return;

            SocketTextChannel? channel = null;
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
                var embed = new EmbedBuilder()
                    .WithTitle($"🎁 Món quà miễn phí hàng tuần từ Unity!")
                    .WithDescription($"**{gift.Title}** đang được tặng miễn phí trong tuần này tại Publisher Sale.")
                    .WithUrl(gift.Link)
                    .WithColor(Color.Gold)
                    .WithImageUrl(gift.ImageUrl)
                    .AddField("Mã Coupon", $"`{gift.CouponCode}`", inline: true)
                    .AddField("Hướng dẫn", "Thêm vào giỏ hàng và nhập mã coupon khi thanh toán.", inline: false)
                    .WithFooter(footer => footer.WithText("Unity Publisher Sale • " + DateTime.Now.ToString("dd/MM/yyyy")))
                    .WithCurrentTimestamp()
                    .Build();

                await channel.SendMessageAsync(embed: embed);
                Console.WriteLine($"[UnityGiftWorker] Đã thông báo quà tặng mới: {gift.Title}");
            }
        }
    }
}
