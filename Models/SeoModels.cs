using System;
using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public sealed class ArchiveLink
    {
        public string Path { get; set; }
        public string Label { get; set; }
        public string Region { get; set; }
        public string ProvinceCode { get; set; }
        public string ProvinceName { get; set; }
        public string GameCode { get; set; }
        public string DrawNo { get; set; }
        public DateTime? Date { get; set; }
        public DateTime? Modified { get; set; }
    }
    public sealed class ArchivePage
    {
        public string Title { get; set; }
        public string ParentPath { get; set; }
        public string ParentLabel { get; set; }
        public XoSoRegionViewModel Regional { get; set; }
        public VietlottPageViewModel Vietlott { get; set; }
        public List<ArchiveLink> Links { get; set; } = new List<ArchiveLink>();
        public ArchiveLink Previous { get; set; }
        public ArchiveLink Next { get; set; }
    }
}
