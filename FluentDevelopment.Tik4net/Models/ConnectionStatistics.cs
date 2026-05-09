

using System;
using System.Collections.Generic;

namespace FluentDevelopment.Tik4net.Models
{
    /// <summary>
    /// Represents statistics about the current connection pool and long-lived connections.
    /// </summary>
    public class ConnectionStatistics
    {
        /// <summary>
        /// Gets or sets the total number of long-lived connections.
        /// </summary>
        public int TotalLongConnections { get; set; }

        /// <summary>
        /// Gets or sets the number of available connections in the pool.
        /// </summary>
        public int AvailablePoolConnections { get; set; }

        /// <summary>
        /// Gets or sets the number of active connections in the pool.
        /// </summary>
        public int ActivePoolConnections { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the user is currently logged in.
        /// </summary>
        public bool IsLoggedIn { get; set; }

        /// <summary>
        /// Gets or sets the list of identifiers for long-lived connections.
        /// </summary>
        public List<Guid> LongConnectionIds { get; set; } = new List<Guid>();
    }
}
