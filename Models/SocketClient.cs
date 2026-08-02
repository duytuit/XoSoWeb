using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace XoSoWeb.Models
{
    public class SocketClient
    {
        public Guid Id { get; set; }

        public WebSocket Socket { get; set; }
    }
}
