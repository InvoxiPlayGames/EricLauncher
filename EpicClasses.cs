using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EricLauncher
{
    public class EpicLoginResponse
    {
        public string? access_token { get; set; }
        public int expires_in { get; set; }
        public DateTime expires_at { get; set; }
        public string? token_type { get; set; }
        public string? refresh_token { get; set; }
        public int refresh_expires { get; set; }
        public DateTime refresh_expires_at { get; set; }
        public string? account_id { get; set; }
        public string? client_id { get; set; }
        public bool internal_client { get; set; }
        public string? client_service { get; set; }
        public string[]? scope { get; set; }
        public string? displayName { get; set; }
        public string? app { get; set; }
        public string? in_app_id { get; set; }
        public string? product_id { get; set; }
        public string? application_id { get; set; }
    }

    public class EpicExchangeResponse
    {
        public int expiresInSeconds { get; set; }
        public string? code { get; set; }
        public string? creatingClientId { get; set; }
    }

    public class EpicError
    {
        public string? errorCode { get; set; }
        public string? errorMessage { get; set; }
        public string[]? messageVars { get; set; }
        public int numericErrorCode { get; set; }
        public string? originatingService { get; set; }
        public string? intent { get; set; }
        public string? error_description { get; set; }
        public string? error { get; set; }
    }

    public class EpicVerifyResponse
    {
        public string? token { get; set; }
        public string? session_id { get; set; }
        public string? token_type { get; set; }
        public string? client_id { get; set; }
        public bool internal_client { get; set; }
        public string? client_service { get; set; }
        public string? account_id { get; set; }
        public int expires_in { get; set; }
        public DateTime expires_at { get; set; }
        public string? auth_method { get; set; }
        public string? display_name { get; set; }
        public string? app { get; set; }
        public string? in_app_id { get; set; }
        public string? device_id { get; set; }
    }
}
