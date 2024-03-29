﻿using System;
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
    class FortniteUpdateResponse
    {
        public string? type { get; set; }
    }

    public class FortniteCloudContent
    {
        public string? AppName { get; set; }
        public string? BuildVersion { get; set; }
        public string? Platform { get; set; }
        public string? ManifestPath { get; set; }
        public string? ManifestHash { get; set; }
    }

    class FortniteUpdateCheck
    {

        public static async Task<bool> IsUpToDate(string fortnite_version, string platform)
        {
            EpicLogin epic = new EpicLogin(EpicLogin.FORTNITE_PC_CLIENT, EpicLogin.FORTNITE_PC_SECRET);
            string? client_credentials = await epic.GetClientCredentials();
            HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Add("Authorization", $"bearer {client_credentials}");
            string url = $"https://fngw-mcp-gc-livefn.ol.epicgames.com/fortnite/api/v2/versioncheck/{platform}?version={HttpUtility.UrlEncode(fortnite_version)}-{platform}";
            FortniteUpdateResponse? resp = await client.GetFromJsonAsync<FortniteUpdateResponse>(url);
            return (resp!.type == "NO_UPDATE");
        }
    }
}
