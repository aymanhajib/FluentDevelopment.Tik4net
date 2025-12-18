using System.Net.NetworkInformation;
using System.Net;

namespace FluentDevelopment.Tik4net
{
    public static class ConnectivityChecker
    {
        // موقع اختبار موثوق به لا يتطلب إعادة توجيه (Captive Portal Test)
        private const string InternetTestUrl = "http://www.google.com/generate_204";

        // **بدلاً من الخاصية غير المتزامنة، نستخدم دالة Get غير متزامنة**
        /// <summary>
        /// دالة غير متزامنة تحدد حالة الوصول للشبكة في الوقت الحالي.
        /// يتم إجراء جميع عمليات الفحص المعقدة (Ping و HTTP) عند استدعاء هذه الدالة.
        /// </summary>
        public static async Task<NetworkAccess> GetCurrentAccessStatusAsync()
        {
            // 1. التحقق من التوفر المحلي (الخطوة الأسرع والأولى)
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return NetworkAccess.None;
            }

            // 2. التحقق من الوصول للإنترنت باستخدام Ping أولاً
            if (!await IsPingSuccessful("8.8.8.8"))
            {
                // لا يوجد وصول للخارج، لكن الشبكة المحلية متوفرة
                return NetworkAccess.Local;
            }

            // 3. التحقق من قيود الإنترنت (Constrained Internet)
            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    var response = await client.GetAsync(InternetTestUrl);

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
            using (Ping pinger = new Ping())
            {
                try
                {
                    // استخدام SendPingAsync لأننا داخل دالة غير متزامنة
                    PingReply reply = await pinger.SendPingAsync(host, 3000);
                    return reply.Status == IPStatus.Success;
                }
                catch (PingException)
                {
                    return false;
                }
            }
        }
    }

    public enum NetworkAccess
    {
        // حالة الاتصال غير معروفة بعد (الافتراضية)
        Unknown,

        // لا يوجد اتصال على الإطلاق (كابل غير موصول، أو Wi-Fi مغلق)
        None,

        // متصل بشبكة محلية (راوتر) ولكن لا يوجد طريق للخارج
        Local,

        // متصل بالإنترنت، ولكن الوصول مقيد (مثل بوابات تسجيل الدخول في المطارات/الفنادق)
        ConstrainedInternet,

        // وصول كامل وغير مقيد للإنترنت
        Internet
    }
}
