
using System.Threading.Tasks;

namespace FluentDevelopment.Tik4net
{
    public static class ConnectivityService
    {
        // 1. حقل خاص (Private Field) للاحتفاظ بالحالة الحالية.
        private static NetworkAccess _currentAccessStatus = NetworkAccess.Unknown;

        // 2. الخاصية العامة (Public Property) للقراءة فقط.
        // هذه هي واجهة المستخدم لقراءة حالة الاتصال.
        // 
        /// <summary>
        /// الحصول على حالة الوصول الحالية للشبكة (Unknown, None, Local, ConstrainedInternet, Internet).
        /// </summary>
        public static NetworkAccess CurrentAccessStatus
        {
            get {
                Task.Run(async () => await UpdateStatusAsync()).Wait();
                return _currentAccessStatus; }
        }

        /// <summary>
        /// دالة غير متزامنة (Async) لتحديث الحالة عن طريق إجراء فحص فعلي.
        /// يجب استدعاء هذه الدالة أولاً للحصول على أحدث حالة.
        /// </summary>
        public static async Task UpdateStatusAsync()
        {
            // استخدام المنطق المعقد من الفئة المساعدة
            NetworkAccess newStatus = await ConnectivityChecker.GetCurrentAccessStatusAsync();

            // تحديث الحقل الخاص بالحالة
            _currentAccessStatus = newStatus;
        }

        /// <summary>
        /// طريقة مساعدة لمعرفة ما إذا كان الجهاز متصلاً بالإنترنت بالكامل.
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
