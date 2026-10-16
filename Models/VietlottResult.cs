using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace XoSoWeb.Models
{
 public class VietlottResult
    {
        public long Id { get; set; }
        public int TotalPeriods { get; set; }
        public string GameCode { get; set; }

        public string DrawNo { get; set; }

        public DateTime DrawDate { get; set; }

        public long? EstimatedPrize { get; set; } // Jackpot 1

        public long? EstimatedPrize2 { get; set; } // Jackpot 2

        public List<string> Numbers { get; set; }

        public List<string> SpecialNumbers { get; set; }

        public int? Total { get; set; }

        public string Size { get; set; }

        public string OddEven { get; set; }

        public List<VietlottPrize> Prizes { get; set; }
    }
}