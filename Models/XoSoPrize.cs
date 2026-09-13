using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public class XoSoPrize
    {
        public string PrizeName { get; set; }

        public List<string> Numbers { get; set; }
            = new List<string>();
    }
}