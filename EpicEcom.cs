using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace EricLauncher
{
    class EpicEcomToken
    {
        public string? token { get; set; }
    }

    class EpicEcom
    {
        public const string API_BASE = "https://ecommerceintegration-public-service-ecomprod02.ol.epicgames.com";

        private const string OWT_API_TEMPLATE = "/ecommerceintegration/api/public/platforms/EPIC/identities/{0}/ownershipToken";

        private EpicAccount Account;
        private HttpClient HTTPClient;

        public EpicEcom(EpicAccount account)
        {
            Account = account;
            
            HTTPClient = new();
            HTTPClient.BaseAddress = new Uri(API_BASE);
            HTTPClient.DefaultRequestHeaders.Accept.Clear();
            HTTPClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            HTTPClient.DefaultRequestHeaders.Add("Authorization", "bearer " + account.AccessToken!);
        }

        public async Task<string?> GetOwnershipToken(string catalog_namespace, string catalog_item_id)
        {
            string formatted_url = string.Format(OWT_API_TEMPLATE, Account.AccountId!);
            HttpResponseMessage resp = await HTTPClient.PostAsync(formatted_url, new StringContent($"nsCatalogItemId={catalog_namespace}:{catalog_item_id}", Encoding.UTF8, "application/x-www-form-urlencoded"));
            if (!resp.IsSuccessStatusCode)
            {
                EpicError? error_response = await resp.Content.ReadFromJsonAsync<EpicError>();
                Console.WriteLine($"Failed to fetch ownership token: '{error_response!.errorMessage}' ({error_response!.errorCode})");
                return null;
            }
            EpicEcomToken? ecom_response = await resp.Content.ReadFromJsonAsync<EpicEcomToken>();
            return ecom_response!.token;
        }
    }
}
