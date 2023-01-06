using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace EricLauncher
{
    class EASAccount
    {
        private const string EXCHANGE_API_URL = "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/exchange";

        public string? AccountId;
        public string? DisplayName;
        public string AccessToken;
        public DateTime AccessExpiry;
        public string RefreshToken;
        public DateTime RefreshExpiry;

        private HttpClient HTTPClient;

        public EASAccount(EASLoginResponse login)
        {
            AccessToken = login.access_token!;
            RefreshToken = login.refresh_token!;
            AccessExpiry = login.expires_at!;
            RefreshExpiry = login.refresh_expires_at!;
            AccountId = login.account_id!;
            DisplayName = login.displayName;

            HTTPClient = new();
            HTTPClient.DefaultRequestHeaders.Accept.Clear();
            HTTPClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HTTPClient.DefaultRequestHeaders.Add("Authorization", login.token_type + " " + login.access_token);
        }

        public async Task<string> GetExchangeCode()
        {
            EASExchangeResponse? resp = await HTTPClient.GetFromJsonAsync<EASExchangeResponse>(EXCHANGE_API_URL);
            return resp!.code!;
        }

        public StoredAccountInfo MakeStoredAccountInfo()
        {
            StoredAccountInfo info = new StoredAccountInfo();
            info.AccountId = AccountId;
            info.RefreshExpiry = RefreshExpiry;
            info.RefreshToken = RefreshToken;
            info.AccessExpiry = AccessExpiry;
            info.AccessToken = AccessToken;
            info.DisplayName = DisplayName;
            return info;
        }
    }
}
