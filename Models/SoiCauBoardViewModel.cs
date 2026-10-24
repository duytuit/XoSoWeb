using System.Collections.Generic;

namespace XoSoWeb.Models
{
    public sealed class SoiCauBoardViewModel
    {
        public SoiCauDraw Draw { get; set; }
        public HashSet<int> Positions { get; set; } = new();
        public bool Interactive { get; set; }
    }
}
