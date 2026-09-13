using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace XoSoWeb.Models
{
    public class VietlottPrize
    {
        public string PrizeName { get; set; }

        public List<string> Numbers { get; set; }

        public string Value { get; set; }
    }
}