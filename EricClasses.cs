using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EricLauncher
{
    public class StoredAccountInfo
    {
        public string? AccountId { get; set; }
        public string? DisplayName { get; set; }
        public string? AccessToken { get; set; }
        public DateTime AccessExpiry { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime RefreshExpiry { get; set; }
    }
}
