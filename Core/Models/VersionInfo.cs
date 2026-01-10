using System;

namespace ArdysaModsTools.Core.Models
{
    public class VersionInfo
    {
        public string TagName { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public DateTime? PublishedAt { get; set; }
    }
}
