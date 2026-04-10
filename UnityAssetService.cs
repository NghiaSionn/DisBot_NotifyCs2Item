using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CS2PriceBot
{
    public class UnityGiftInfo
    {
        public string Title { get; set; } = "Unknown Asset";
        public string Link { get; set; } = "https://assetstore.unity.com/publisher-sale";
        public string ImageUrl { get; set; } = "";
        public string CouponCode { get; set; } = "Unknown";
    }

    public class UnityAssetService
    {
        private readonly HttpClient _httpClient;
        private const string SaleUrl = "https://assetstore.unity.com/publisher-sale";

        public UnityAssetService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<UnityGiftInfo?> GetCurrentGiftAsync()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                
                var html = await _httpClient.GetStringAsync(SaleUrl);

                var gift = new UnityGiftInfo();

                // 1. Extract Asset Link and Title from the "Get your free gift" section
                // Pattern: matches <a ... href=".../packages/...">Get your free gift</a> or similar
                // Based on the markdown, it looks like: [Get your free gift](https://assetstore.unity.com/packages/...)
                // In HTML, it's likely <a class="..." href="/packages/vfx/particles/map-track-markers-vfx-131762">Get your free gift</a>
                var linkMatch = Regex.Match(html, @"href=""(/packages/[^""]+)""[^>]*>Get your free gift", RegexOptions.IgnoreCase);
                if (!linkMatch.Success)
                {
                    // Fallback for different button text
                    linkMatch = Regex.Match(html, @"href=""(/packages/[^""]+)""[^>]*>GET YOUR FREE GIFT", RegexOptions.IgnoreCase);
                }

                if (linkMatch.Success)
                {
                    gift.Link = "https://assetstore.unity.com" + linkMatch.Groups[1].Value;
                    
                    // Extract title from the slug (e.g. map-track-markers-vfx-131762)
                    string slug = linkMatch.Groups[1].Value.Split('/').Last();
                    // Remove the numeric ID at the end if present
                    var titleMatch = Regex.Match(slug, @"(.+)-\d+$");
                    string cleanTitle = titleMatch.Success ? titleMatch.Groups[1].Value : slug;
                    gift.Title = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(cleanTitle.Replace("-", " "));
                }

                // 2. Extract Coupon Code
                // Pattern from user screenshot: "enter the coupon code HOVL2026"
                var couponMatch = Regex.Match(html, @"coupon code\s+([A-Z0-9]+)", RegexOptions.IgnoreCase);
                if (couponMatch.Success)
                {
                    gift.CouponCode = couponMatch.Groups[1].Value;
                }

                // 3. Extract Image URL
                // Often in a banner or as og:image if the whole page is about the sale
                var imageMatch = Regex.Match(html, @"<meta property=""og:image"" content=""([^""]+)""", RegexOptions.IgnoreCase);
                if (imageMatch.Success)
                {
                    gift.ImageUrl = imageMatch.Groups[1].Value;
                }
                else
                {
                    // Fallback: search for background images or product images
                    var imgTagMatch = Regex.Match(html, @"<img [^>]*src=""([^""]+assets.u3d.com/[^""]+)""", RegexOptions.IgnoreCase);
                    if (imgTagMatch.Success)
                    {
                        gift.ImageUrl = imgTagMatch.Groups[1].Value;
                    }
                }

                return gift;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UnityAssetService] Error: {ex.Message}");
                return null;
            }
        }
    }
}
