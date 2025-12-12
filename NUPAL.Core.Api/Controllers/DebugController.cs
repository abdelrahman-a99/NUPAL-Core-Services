using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NUPAL.Core.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public DebugController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("wuzzuf-json")]
        public async Task<IActionResult> GetWuzzufJson([FromQuery] string? keyword = "software")
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var url = $"https://wuzzuf.net/search/jobs/?q={keyword}&a=hpb";
                var html = await client.GetStringAsync(url);

                // Extract JSON using regex
                var match = Regex.Match(html, @"Wuzzuf\.initialStoreState\s*=\s*(\{.*?\});", RegexOptions.Singleline);

                if (!match.Success)
                {
                    return Ok(new { 
                        success = false, 
                        message = "Could not find Wuzzuf.initialStoreState in page",
                        htmlLength = html.Length
                    });
                }

                var jsonText = match.Groups[1].Value;

                return Content(jsonText, "application/json");
            }
            catch (Exception ex)
            {
                return Ok(new { 
                    success = false, 
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }
}
