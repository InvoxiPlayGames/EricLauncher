using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EricLauncher
{
    public class LegendaryManifestEntry
    {
        public string? app_name { get; set; }
        public string[]? base_urls { get; set; }
        public bool can_run_offline { get; set; }
        public string? egl_guid { get; set; }
        public string? executable { get; set; }
        public string? install_path { get; set; }
        public long install_size { get; set; }
        public string[]? install_tags { get; set; }
        public bool is_dlc { get; set; }
        public string? launch_parameters { get; set; }
        public string? manifest_path { get; set; }
        public bool needs_verification { get; set; }
        public string? platform { get; set; }
        public object? prereq_info { get; set; }
        public bool requires_ot { get; set; }
        public string? save_path { get; set; }
        public string? title { get; set; }
        public string? version { get; set; }
    }

}
