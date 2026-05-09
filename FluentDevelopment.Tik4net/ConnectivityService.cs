
using System.Threading.Tasks;

namespace FluentDevelopment.Tik4net
{
    /// <summary>
    /// Provides connectivity status and related utilities for network access detection.
    /// </summary>
    public static class ConnectivityService
    {
        private static NetworkAccess _currentAccessStatus = NetworkAccess.Unknown;

        /// <summary>
        /// Gets the current network access status. This property updates the status before returning the value.
        /// </summary>
        public static NetworkAccess CurrentAccessStatus
        {
            get
            {
                Task.Run(async () => await UpdateStatusAsync()).Wait();
                return _currentAccessStatus;
            }
        }

        /// <summary>
        /// Asynchronously updates the current network access status by checking connectivity.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task UpdateStatusAsync()
        {
            NetworkAccess newStatus = await ConnectivityChecker.GetCurrentAccessStatusAsync();
            _currentAccessStatus = newStatus;
        }

        /// <summary>
        /// Gets a value indicating whether the device is fully connected to the Internet.
        /// </summary>
        public static bool IsFullyConnected
        {
            get
            {
                return CurrentAccessStatus == NetworkAccess.Internet;
            }
        }
    }
}
