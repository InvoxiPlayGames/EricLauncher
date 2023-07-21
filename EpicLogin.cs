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
    class EpicLogin
    {
        public const string LAUNCHER_CLIENT = "34a02cf8f4414e29b15921876da36f9a";
        public const string LAUNCHER_SECRET = "daafbccc737745039dffe53d94fc76cf";
        public const string ACCOUNTS_API_BASE = "https://account-public-service-prod.ol.epicgames.com";

        private const string TOKEN_API_URL = "/account/api/oauth/token";

        private string ClientId;
        private string ClientSecret;
        private HttpClient HTTPClient;

        public EpicLogin(string client_id = LAUNCHER_CLIENT, string client_secret = LAUNCHER_SECRET)
        {
            ClientId = client_id;
            ClientSecret = client_secret;
            HTTPClient = new();
            HTTPClient.BaseAddress = new Uri(ACCOUNTS_API_BASE);
            HTTPClient.DefaultRequestHeaders.Accept.Clear();
            HTTPClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HTTPClient.DefaultRequestHeaders.Add("Authorization", "basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));
        }

        private async Task<EpicLoginResponse?> DoOAuthLogin(string grant_type, string? grant_name = null, string? grant_code = null, string? extra_arguments = null, bool use_eg1 = true)
        {
            if (grant_name != null && grant_code == null)
                throw new Exception($"Grant name was given but with no grant value.");

            // build the data to send to the oauth token endpoint
            string form_encoded_data = $"grant_type={grant_type}";
            if (extra_arguments != null)
                form_encoded_data += $"&{extra_arguments}";
            if (grant_code != null) // if no grant name is provided use the name of the type
                form_encoded_data += $"&{(grant_name != null ? grant_name : grant_type)}={grant_code}";
            if (use_eg1)
                form_encoded_data += $"&token_type=eg1";

            HttpResponseMessage resp = await HTTPClient.PostAsync(TOKEN_API_URL, new StringContent(form_encoded_data, Encoding.UTF8, "application/x-www-form-urlencoded"));
            if (!resp.IsSuccessStatusCode)
            {
                EpicError? error_response = await resp.Content.ReadFromJsonAsync<EpicError>();
                throw new Exception($"OAuth login of grant type {grant_type} failed: '{error_response!.errorMessage}' ({error_response!.errorCode})");
            }
            EpicLoginResponse? login_response = await resp.Content.ReadFromJsonAsync<EpicLoginResponse>();
            return login_response;
        }

        public async Task<string?> GetClientCredentials()
        {
            EpicLoginResponse? login_response = await DoOAuthLogin("client_credentials");
            return login_response!.access_token;
        }

        public async Task<EpicAccount?> LoginWithRefreshToken(string refresh_token)
        {
            EpicLoginResponse? login_response = await DoOAuthLogin("refresh_token", null, refresh_token);
            if (login_response == null) // maybe we should throw an exception here
                return null;
            return new EpicAccount(login_response);
        }

        public async Task<EpicAccount?> LoginWithAuthorizationCode(string authorization_code)
        {
            EpicLoginResponse? login_response = await DoOAuthLogin("authorization_code", "code", authorization_code);
            if (login_response == null) // maybe we should throw an exception here
                return null;
            return new EpicAccount(login_response);
        }
    }
}
