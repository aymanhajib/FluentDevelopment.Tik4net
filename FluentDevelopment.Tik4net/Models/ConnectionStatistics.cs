

using System;
using System.Collections.Generic;

namespace FluentDevelopment.Tik4net.Models
{
    public class ConnectionStatistics
    {
        public int TotalLongConnections { get; set; }
        public int AvailablePoolConnections { get; set; }
        public int ActivePoolConnections { get; set; }
        public bool IsLoggedIn { get; set; }
        public List<Guid> LongConnectionIds { get; set; } = new();
    }
}
