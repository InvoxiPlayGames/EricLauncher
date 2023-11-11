using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Json;
using System.Web;

namespace EricLauncher
{
    public class CalderaRequest
    {
        public string? account_id { get; set; }
        public string? exchange_code { get; set; }
        public bool test_mode { get; set; }
        public string? epic_app { get; set; }
        public bool nvidia { get; set; }
        public bool luna { get; set; }
        public bool salmon { get; set; }
    }

    public class CalderaResponse
    {
        public string? provider { get; set; }
        public string? jwt { get; set; }
    }

    class EpicCaldera
    {
        public const string CALDERA_HOST = "https://caldera-service-prod.ecosec.on.epicgames.com";
        public const string CALDERA_RACP_PATH = "/caldera/api/v1/launcher/racp";

        public static async Task<CalderaResponse?> GetCalderaResponse(string accountid, string exchangecode, string app)
        {
            HttpClient client = new();
            client.BaseAddress = new Uri(CALDERA_HOST);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", "Caldera/UNKNOWN-UNKNOWN-UNKNOWN");
            CalderaRequest req = new CalderaRequest
            {
                account_id = accountid,
                exchange_code = exchangecode,
                epic_app = app
            };
            HttpResponseMessage resp = await client.PostAsJsonAsync(CALDERA_RACP_PATH, req);
            return await resp.Content.ReadFromJsonAsync<CalderaResponse>();
        }
    }
}
