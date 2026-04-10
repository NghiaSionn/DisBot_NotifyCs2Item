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
        private readonly UnityAssetService _unityService;

        public BotService(DiscordSocketClient client, DataStorage storage, SteamMarketService steamService, UnityAssetService unityService)
        {
            _client = client;
            _storage = storage;
            _steamService = steamService;
            _unityService = unityService;

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
                .WithDescription("Xem danh sách các vật phẩm hoặc quà tặng đang theo dõi")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("filter")
                    .WithDescription("Chọn loại danh sách hiển thị")
                    .WithType(ApplicationCommandOptionType.String)
                    .AddChoice("Tất cả", "all")
                    .AddChoice("CS2 (Skins)", "cs")
                    .AddChoice("Unity (Free Gift)", "unity"));
                
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

            var selectCommand = new SlashCommandBuilder()
                .WithName("select")
                .WithDescription("Bật/tắt thông báo cho các ứng dụng")
                .AddOption(new SlashCommandOptionBuilder()
                    .WithName("app")
                    .WithDescription("Chọn ứng dụng")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithRequired(true)
                    .AddChoice("CS2 (Giá vật phẩm)", "cs2")
                    .AddChoice("Unity (Quà tặng hàng tuần)", "unity"))
                .AddOption("status", ApplicationCommandOptionType.Boolean, "Chọn True (Bật) hoặc False (Tắt)", isRequired: true);

            try 
            {
                var commandList = new ApplicationCommandProperties[] 
                {
                    trackCommand.Build(),
                    untrackCommand.Build(),
                    listCommand.Build(),
                    setchannelCommand.Build(),
                    setintervalCommand.Build(),
                    onlyincreaseCommand.Build(),
                    selectCommand.Build()
                };

                await _client.BulkOverwriteGlobalApplicationCommandsAsync(commandList);
                Console.WriteLine($"Đã đăng ký {commandList.Length} lệnh Global Slash Commands.");
            }
            catch(Exception ex) 
            {
                Console.WriteLine($"Lỗi khi đăng ký Global Slash Commands: {ex.Message}");
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
                await command.DeferAsync();
                
                string filter = (string)(command.Data.Options.FirstOrDefault(o => o.Name == "filter")?.Value ?? "all");
                bool showCs = filter == "all" || filter == "cs";
                bool showUnity = filter == "all" || filter == "unity";

                var embed = new EmbedBuilder()
                    .WithTitle("📋 Danh sách theo dõi")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp();

                if (showCs)
                {
                    if (_storage.Config.TrackedItems.Count > 0)
                    {
                        string itemsStr = "";
                        foreach (var item in _storage.Config.TrackedItems)
                        {
                            if (_storage.Config.LastKnownPrices.TryGetValue(item, out double price))
                                itemsStr += $"• **{item}**: {price:N0} VNĐ\n";
                            else
                                itemsStr += $"• **{item}**: (Chưa có giá)\n";
                        }
                        embed.AddField("🎮 CS2 Skins", itemsStr);
                        
                        // Set thumbnail to first item
                        var firstItem = _storage.Config.TrackedItems.First();
                        var details = await _steamService.GetItemDetailsAsync(firstItem);
                        if (!string.IsNullOrEmpty(details.IconUrl)) embed.WithThumbnailUrl(details.IconUrl);
                    }
                    else if (filter == "cs")
                    {
                        await command.FollowupAsync("Danh sách theo dõi CS2 đang trống.");
                        return;
                    }
                }

                if (showUnity)
                {
                    var gift = await _unityService.GetCurrentGiftAsync();
                    if (gift != null)
                    {
                        embed.AddField("🎁 Unity Free Gift", $"**{gift.Title}**\nCode: `{gift.CouponCode}`\n[(Link Store)]({gift.Link})");
                        if (!showCs || string.IsNullOrEmpty(embed.ThumbnailUrl))
                        {
                            embed.WithThumbnailUrl(gift.ImageUrl);
                        }
                    }
                    else if (filter == "unity")
                    {
                        await command.FollowupAsync("Không thể lấy dữ liệu quà tặng Unity hiện tại.");
                        return;
                    }
                }

                if (embed.Fields.Count == 0)
                {
                    await command.FollowupAsync("Không có thông tin nào để hiển thị.");
                }
                else
                {
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
            else if (command.CommandName == "select")
            {
                string app = (string)command.Data.Options.First(o => o.Name == "app").Value;
                bool status = (bool)command.Data.Options.First(o => o.Name == "status").Value;

                if (app == "cs2")
                {
                    _storage.Config.EnableCS2Notifications = status;
                    _storage.Save();
                    await command.RespondAsync($"{(status ? "✅" : "❌")} Đã **{(status ? "BẬT" : "TẮT")}** thông báo giá vật phẩm CS2.");
                }
                else if (app == "unity")
                {
                    _storage.Config.EnableUnityNotifications = status;
                    _storage.Save();
                    await command.RespondAsync($"{(status ? "✅" : "❌")} Đã **{(status ? "BẬT" : "TẮT")}** thông báo quà tặng Unity Publisher Sale hàng tuần.");
                }
            }
        }
    }
}
