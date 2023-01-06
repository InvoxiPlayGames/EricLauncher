using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace EricLauncher
{
    class EASLogin
    {
        public const string LAUNCHER_CLIENT = "34a02cf8f4414e29b15921876da36f9a";
        public const string LAUNCHER_SECRET = "daafbccc737745039dffe53d94fc76cf";

        public const string FORTNITE_CLIENT = "ec684b8c687f479fadea3cb2ad83f5c6";
        public const string FORTNITE_SECRET = "e1f31c211f28413186262d37a13fc84d";

        private const string API_URL = "https://account-public-service-prod.ol.epicgames.com/account/api/oauth/token";

        private string ClientId;
        private string ClientSecret;
        private HttpClient HTTPClient;

        public EASLogin(string client_id = LAUNCHER_CLIENT, string client_secret = LAUNCHER_SECRET)
        {
            ClientId = client_id;
            ClientSecret = client_secret;
            HTTPClient = new();
            HTTPClient.DefaultRequestHeaders.Accept.Clear();
            HTTPClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HTTPClient.DefaultRequestHeaders.Add("Authorization", "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
        }

        public async Task<string?> GetClientCredentials()
        {
            HttpResponseMessage resp = await HTTPClient.PostAsync(API_URL, new StringContent("grant_type=client_credentials&token_type=eg1", Encoding.UTF8, "application/x-www-form-urlencoded"));
            if (!resp.IsSuccessStatusCode)
            {
                EASError? error_response = await resp.Content.ReadFromJsonAsync<EASError>();
                throw new Exception($"Client credential fetch failed: '{error_response!.errorMessage}' ({error_response!.errorCode})");
            }
            EASLoginResponse? login_response = await resp.Content.ReadFromJsonAsync<EASLoginResponse>();
            return login_response!.access_token;
        }

        public async Task<EASAccount?> LoginWithRefreshToken(string refresh_token)
        {
            HttpResponseMessage resp = await HTTPClient.PostAsync(API_URL, new StringContent($"grant_type=refresh_token&refresh_token={refresh_token}&token_type=eg1", Encoding.UTF8, "application/x-www-form-urlencoded"));
            if (!resp.IsSuccessStatusCode)
            {
                EASError? error_response = await resp.Content.ReadFromJsonAsync<EASError>();
                throw new Exception($"Refresh token login failed: '{error_response!.errorMessage}' ({error_response!.errorCode})");
            }
            EASLoginResponse? login_response = await resp.Content.ReadFromJsonAsync<EASLoginResponse>();
            if (login_response == null) // maybe we should throw an exception here
                return null;
            return new EASAccount(login_response);
        }

        public async Task<EASAccount?> LoginWithAuthorizationCode(string authorization_code)
        {
            HttpResponseMessage resp = await HTTPClient.PostAsync(API_URL, new StringContent($"grant_type=authorization_code&code={authorization_code}&token_type=eg1", Encoding.UTF8, "application/x-www-form-urlencoded"));
            if (!resp.IsSuccessStatusCode)
            {
                EASError? error_response = await resp.Content.ReadFromJsonAsync<EASError>();
                throw new Exception($"Authorization code login failed: '{error_response!.errorMessage}' ({error_response!.errorCode})");
            }
            EASLoginResponse? login_response = await resp.Content.ReadFromJsonAsync<EASLoginResponse>();
            if (login_response == null) // maybe we should throw an exception here
                return null;
            return new EASAccount(login_response);
        }
    }
}
