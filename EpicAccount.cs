using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace EricLauncher
{
    class EpicAccount
    {
        private const string EXCHANGE_API_URL = "/account/api/oauth/exchange";
        private const string VERIFY_API_URL = "/account/api/oauth/verify";

        public string? AccountId;
        public string? DisplayName;
        public string AccessToken;
        public DateTime AccessExpiry;
        public string RefreshToken;
        public DateTime RefreshExpiry;

        private HttpClient HTTPClient;

        public EpicAccount(EpicLoginResponse login)
        {
            AccessToken = login.access_token!;
            RefreshToken = login.refresh_token!;
            AccessExpiry = login.expires_at!;
            RefreshExpiry = login.refresh_expires_at!;
            AccountId = login.account_id!;
            DisplayName = login.displayName;

            HTTPClient = new();
            HTTPClient.BaseAddress = new Uri(EpicLogin.ACCOUNTS_API_BASE);
            HTTPClient.DefaultRequestHeaders.Accept.Clear();
            HTTPClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HTTPClient.DefaultRequestHeaders.Add("Authorization", login.token_type + " " + login.access_token);
        }

        public EpicAccount(StoredAccountInfo info)
        {
            if (info.AccessToken == null || info.RefreshToken == null)
                throw new Exception("Stored account info doesn't have access or refresh token");

            if (info.AccountId != null)
                AccountId = info.AccountId;
            if (info.DisplayName != null)
                DisplayName = info.DisplayName;
            AccessToken = info.AccessToken;
            RefreshToken = info.RefreshToken;
            AccessExpiry = info.AccessExpiry;
            RefreshExpiry = info.RefreshExpiry;

            HTTPClient = new();
            HTTPClient.BaseAddress = new Uri(EpicLogin.ACCOUNTS_API_BASE);
            HTTPClient.DefaultRequestHeaders.Accept.Clear();
            HTTPClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HTTPClient.DefaultRequestHeaders.Add("Authorization", "bearer " + info.AccessToken);
        }

        public async Task<string> GetExchangeCode()
        {
            EpicExchangeResponse? resp = await HTTPClient.GetFromJsonAsync<EpicExchangeResponse>(EXCHANGE_API_URL);
            return resp!.code!;
        }

        public async Task<bool> VerifyToken()
        {
            HttpResponseMessage resp = await HTTPClient.GetAsync(VERIFY_API_URL);
            if (!resp.IsSuccessStatusCode)
                return false;
            EpicVerifyResponse? verify_response = await resp.Content.ReadFromJsonAsync<EpicVerifyResponse>();
            // if the token is expiring soon, why bother amirite
            return verify_response!.expires_in > 60;
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
