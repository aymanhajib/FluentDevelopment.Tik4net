using System.Net.NetworkInformation;
using System.Net;
using System.Threading.Tasks;
using System.Net.Http;
using System;

namespace FluentDevelopment.Tik4net
{
    /// <summary>
    /// Provides methods to check the current network connectivity status.
    /// </summary>
    public static class ConnectivityChecker
    {
        // موقع اختبار موثوق به لا يتطلب إعادة توجيه (Captive Portal Test)
        private const string InternetTestUrl = "http://www.google.com/generate_204";

        /// <summary>
        /// Asynchronously determines the current network access status.
        /// Performs all complex checks (Ping and HTTP) when called.
        /// </summary>
        /// <returns>The current <see cref="NetworkAccess"/> status.</returns>
        public static async Task<NetworkAccess> GetCurrentAccessStatusAsync()
        {
            // 1. التحقق من التوفر المحلي (الخطوة الأسرع والأولى)
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return NetworkAccess.None;
            }

            // 2. التحقق من الوصول للإنترنت باستخدام Ping أولاً
            if (!await IsPingSuccessful("8.8.8.8").ConfigureAwait(false))
            {
                // لا يوجد وصول للخارج، لكن الشبكة المحلية متوفرة
                return NetworkAccess.Local;
            }

            // 3. التحقق من قيود الإنترنت (Constrained Internet)
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync(InternetTestUrl).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    // نجاح كامل: وصل إلى الخادم بدون إعادة توجيه
                    return NetworkAccess.Internet;
                }

                // إذا كان هناك إعادة توجيه (302) أو حالة أخرى تشير إلى Captive Portal
                if (response.StatusCode == HttpStatusCode.Found || response.StatusCode == HttpStatusCode.SeeOther)
                {
                    return NetworkAccess.ConstrainedInternet;
                }
            }
            catch (HttpRequestException)
            {
                // فشل الاتصال HTTP على الرغم من نجاح Ping، مما يشير إلى قيود.
                return NetworkAccess.ConstrainedInternet;
            }
            catch (TaskCanceledException)
            {
                // تجاوز المهلة، قد يشير إلى قيود أو بطء شديد.
                return NetworkAccess.ConstrainedInternet;
            }
            catch (Exception)
            {
                // أي خطأ آخر
                return NetworkAccess.Unknown;
            }

            // في حال نجح Ping وفشلت عمليات HTTP بطرق غريبة أخرى، نعتبره مقيداً.
            return NetworkAccess.ConstrainedInternet;
        }

        private static async Task<bool> IsPingSuccessful(string host)
        {
            using var pinger = new Ping();
            try
            {
                // استخدام SendPingAsync لأننا داخل دالة غير متزامنة
                PingReply reply = await pinger.SendPingAsync(host, 3000).ConfigureAwait(false);
                return reply.Status == IPStatus.Success;
            }
            catch (PingException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Represents the possible network access states.
    /// </summary>
    public enum NetworkAccess
    {
        /// <summary>
        /// The network access state is unknown (default).
        /// </summary>
        Unknown,

        /// <summary>
        /// No network connection at all (cable unplugged, or Wi-Fi off).
        /// </summary>
        None,

        /// <summary>
        /// Connected to a local network (router) but no route to the outside.
        /// </summary>
        Local,

        /// <summary>
        /// Connected to the internet, but access is constrained (e.g., captive portals in airports/hotels).
        /// </summary>
        ConstrainedInternet,

        /// <summary>
        /// Full and unrestricted internet access.
        /// </summary>
        Internet
    }
}
