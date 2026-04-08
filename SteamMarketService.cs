using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CS2PriceBot
{
    public class ItemDetails
    {
        public string Name { get; set; } = "";
        public double? Price { get; set; }
        public string Type { get; set; } = "Unknown";
        public string IconUrl { get; set; } = "";
    }

    public class SteamMarketService
    {
        private readonly HttpClient _httpClient;

        public SteamMarketService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ItemDetails> GetItemDetailsAsync(string marketHashName)
        {
            var details = new ItemDetails { Name = marketHashName };
            try
            {
                string renderUrl = $"https://steamcommunity.com/market/listings/730/{Uri.EscapeDataString(marketHashName)}/render?start=0&count=1&currency=15&language=english";
                
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                
                var response = await _httpClient.GetStringAsync(renderUrl);
                var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Object)
                {
                    var appAssets = assets.GetProperty("730");
                    var contextAssets = appAssets.GetProperty("2");
                    
                    var firstAsset = contextAssets.EnumerateObject().FirstOrDefault().Value;
                    if (firstAsset.ValueKind == JsonValueKind.Object)
                    {
                        if (firstAsset.TryGetProperty("type", out var typeProp))
                            details.Type = typeProp.GetString() ?? "Unknown";
                        
                        if (firstAsset.TryGetProperty("icon_url", out var iconProp))
                            details.IconUrl = $"https://community.cloudflare.steamstatic.com/economy/image/{iconProp.GetString()}/360fx360f";
                    }
                }

                details.Price = await GetLowestPriceAsync(marketHashName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching details for {marketHashName}: {ex.Message}");
            }
            return details;
        }

        public async Task<double?> GetLowestPriceAsync(string marketHashName)
        {
            try
            {
                string url = $"https://steamcommunity.com/market/priceoverview/?appid=730&currency=15&market_hash_name={Uri.EscapeDataString(marketHashName)}";
                
                var response = await _httpClient.GetStringAsync(url);
                var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("success", out var success) && success.GetBoolean())
                {
                    if (root.TryGetProperty("lowest_price", out var priceElement))
                    {
                        string priceStr = priceElement.GetString();
                        if (!string.IsNullOrEmpty(priceStr))
                        {
                            string cleaned = priceStr.Replace("₫", "").Replace("đ", "").Replace("?", "").Trim();
                            cleaned = cleaned.Replace(".", ""); 
                            cleaned = cleaned.Replace(",", "."); 
                            if (double.TryParse(cleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double price))
                            {
                                return price;
                            }
                        }
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
