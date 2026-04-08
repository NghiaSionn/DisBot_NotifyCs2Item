using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Discord;
using Discord.WebSocket;

namespace CS2PriceBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var storage = new DataStorage();
            if (storage.Config.Token == "YOUR_DISCORD_BOT_TOKEN_HERE" || string.IsNullOrWhiteSpace(storage.Config.Token))
            {
                Console.WriteLine("\n[LỖI] CHƯA CÓ TOKEN DISCORD. Vui lòng thiết lập biến môi trường DISCORD_TOKEN hoặc điền vào bot_data.json.\n");
                return;
            }

            var builder = WebApplication.CreateBuilder(args);
            
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.UseUrls($"http://*:{port}");

            builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                AlwaysDownloadUsers = true,
            }));
            builder.Services.AddSingleton(storage);
            builder.Services.AddHttpClient<SteamMarketService>();
            builder.Services.AddHostedService<BotService>();
            builder.Services.AddHostedService<PriceCheckerWorker>();

            var app = builder.Build();
            
            app.MapGet("/", () => "CS2 Price Bot is online 24/7!");

            await app.RunAsync();
        }
    }
}
