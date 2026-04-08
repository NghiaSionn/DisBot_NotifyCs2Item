using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace CS2PriceBot
{
    public class BotService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly DataStorage _storage;
        private readonly SteamMarketService _steamService;

        public BotService(DiscordSocketClient client, DataStorage storage, SteamMarketService steamService)
        {
            _client = client;
            _storage = storage;
            _steamService = steamService;

            _client.Log += LogAsync;
            _client.Ready += ReadyAsync;
            _client.SlashCommandExecuted += SlashCommandHandler;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            string token = _storage.Config.Token;
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client.StopAsync();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            Console.WriteLine($"\n--- Bot đã kết nối thành công: {_client.CurrentUser.Username} ---\n");
            
            foreach (var guild in _client.Guilds)
            {
                var trackCommand = new SlashCommandBuilder()
                    .WithName("track")
                    .WithDescription("Khởi tạo theo dõi giá cho một vật phẩm CS2")
                    .AddOption("item", ApplicationCommandOptionType.String, "Tên vật phẩm (ví dụ: MAC-10 | Amber Fade (Factory New))", isRequired: true);
                    
                var untrackCommand = new SlashCommandBuilder()
                    .WithName("untrack")
                    .WithDescription("Ngừng theo dõi giá của một vật phẩm CS2")
                    .AddOption("item", ApplicationCommandOptionType.String, "Tên vật phẩm cần xóa", isRequired: true);
                    
                var listCommand = new SlashCommandBuilder()
                    .WithName("list")
                    .WithDescription("Xem danh sách các vật phẩm đang được theo dõi");
                    
                var setchannelCommand = new SlashCommandBuilder()
                    .WithName("setchannel")
                    .WithDescription("Đặt kênh hiện tại làm nơi nhận thông báo giá từ bot");
                    
                var setintervalCommand = new SlashCommandBuilder()
                    .WithName("setinterval")
                    .WithDescription("Thay đổi thời gian kiểm tra giá (mặc định: 30 phút)")
                    .AddOption("minutes", ApplicationCommandOptionType.Integer, "Số phút giữa mỗi lần quét", isRequired: true);

                var onlyincreaseCommand = new SlashCommandBuilder()
                    .WithName("onlyincrease")
                    .WithDescription("Bật/tắt chế độ CHỈ thông báo khi giá TĂNG")
                    .AddOption("enable", ApplicationCommandOptionType.Boolean, "Chọn True (Bật) hoặc False (Tắt thông báo khi giảm)", isRequired: true);

                try 
                {
                    await guild.CreateApplicationCommandAsync(trackCommand.Build());
                    await guild.CreateApplicationCommandAsync(untrackCommand.Build());
                    await guild.CreateApplicationCommandAsync(listCommand.Build());
                    await guild.CreateApplicationCommandAsync(setchannelCommand.Build());
                    await guild.CreateApplicationCommandAsync(setintervalCommand.Build());
                    await guild.CreateApplicationCommandAsync(onlyincreaseCommand.Build());
                    Console.WriteLine($"Đã tạo lệnh Gạch chéo (Slash Commands) cho server: {guild.Name}");
                }
                catch(Exception ex) 
                {
                    Console.WriteLine($"Lỗi khi tạo Slash Command ở server {guild.Name}: {ex.Message}");
                }
            }
        }

        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            if (command.CommandName == "track")
            {
                string item = (string)command.Data.Options.First().Value;
                if (!_storage.Config.TrackedItems.Contains(item))
                {
                    _storage.Config.TrackedItems.Add(item);
                    _storage.Save();
                    await command.RespondAsync($"✅ Đã thêm `{item}` vào danh sách theo dõi!");
                }
                else
                {
                    await command.RespondAsync($"⚠️ `{item}` đã có trong danh sách theo dõi từ trước.");
                }
            }
            else if (command.CommandName == "untrack")
            {
                string item = (string)command.Data.Options.First().Value;
                if (_storage.Config.TrackedItems.Contains(item))
                {
                    _storage.Config.TrackedItems.Remove(item);
                    _storage.Config.LastKnownPrices.Remove(item);
                    _storage.Save();
                    await command.RespondAsync($"❌ Đã xóa `{item}` khỏi danh sách theo dõi.");
                }
                else
                {
                    await command.RespondAsync($"⚠️ `{item}` không có trong danh sách theo dõi.");
                }
            }
            else if (command.CommandName == "list")
            {
                // Defer response because fetching image might take more than 3 seconds
                await command.DeferAsync();

                if (_storage.Config.TrackedItems.Count == 0)
                {
                    await command.FollowupAsync("Danh sách theo dõi đang trống.");
                }
                else
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("📋 Danh sách vật phẩm đang theo dõi")
                        .WithColor(Color.Blue)
                        .WithFooter(footer => footer.WithText($"Tổng cộng: {_storage.Config.TrackedItems.Count} vật phẩm"))
                        .WithCurrentTimestamp();

                    // Nếu chỉ có 1 vài vật phẩm, ta lấy ảnh của vật phẩm đầu tiên làm thumbnail
                    if (_storage.Config.TrackedItems.Count > 0)
                    {
                        var firstItem = _storage.Config.TrackedItems.First();
                        var details = await _steamService.GetItemDetailsAsync(firstItem);
                        if (!string.IsNullOrEmpty(details.IconUrl))
                        {
                            embed.WithThumbnailUrl(details.IconUrl);
                        }
                    }

                    foreach (var item in _storage.Config.TrackedItems)
                    {
                        if (_storage.Config.LastKnownPrices.TryGetValue(item, out double price))
                        {
                            embed.AddField(item, $"**{price:N0} VNĐ** [(Link Steam)](https://steamcommunity.com/market/listings/730/{Uri.EscapeDataString(item)})", inline: false);
                        }
                        else
                        {
                            embed.AddField(item, $"(Chưa có dữ liệu giá) [(Link Steam)](https://steamcommunity.com/market/listings/730/{Uri.EscapeDataString(item)})", inline: false);
                        }
                    }

                    await command.FollowupAsync(embed: embed.Build());
                }
            }
            else if (command.CommandName == "setchannel")
            {
                _storage.Config.NotificationChannelId = command.Channel.Id;
                _storage.Save();
                await command.RespondAsync($"✅ Đã cài đặt kênh này làm nơi nhận thông báo giá vật phẩm!");
            }
            else if (command.CommandName == "setinterval")
            {
                long minutesLong = (long)command.Data.Options.First().Value;
                int minutes = (int)minutesLong;
                if (minutes < 1) minutes = 1;
                if (minutes > 1440) minutes = 1440;
                _storage.Config.CheckIntervalMinutes = minutes;
                _storage.Save();
                await command.RespondAsync($"⏳ Đã đổi thời gian kiểm tra giá thành **{minutes} phút/lần**.");
            }
            else if (command.CommandName == "onlyincrease")
            {
                bool enable = (bool)command.Data.Options.First().Value;
                _storage.Config.NotifyOnlyOnIncrease = enable;
                _storage.Save();
                if (enable)
                    await command.RespondAsync($"📈 Đã BẬT chế độ: **Chỉ thông báo khi giá vật phẩm TĂNG**.");
                else
                    await command.RespondAsync($"🔄 Đã TẮT chế độ báo tăng: Bot sẽ thông báo cả khi giá TĂNG và GIẢM.");
            }
        }
    }
}
