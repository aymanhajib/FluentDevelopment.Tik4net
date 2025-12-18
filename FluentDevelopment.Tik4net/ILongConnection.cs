using FluentDevelopment.Tik4net.Models;
using tik4net;

namespace FluentDevelopment.Tik4net;

/// <summary>
/// واجهة تمثل اتصالاً طويل المدى مع جهاز MikroTik
/// </summary>
public interface ILongConnection : IDisposable
{
    /// <summary>
    /// معرف الاتصال الفريد
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// اتصال Tik4Net الأساسي
    /// </summary>
    ITikConnection Connection { get; }

    /// <summary>
    /// اسم الاتصال (اختياري)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// يشير إلى ما إذا كان الاتصال نشطاً وقابلاً للاستخدام
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// وقت إنشاء الاتصال
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// مدة تشغيل الاتصال
    /// </summary>
    TimeSpan Uptime { get; }

    /// <summary>
    /// عدد العمليات التي تم تنفيذها على هذا الاتصال
    /// </summary>
    int OperationCount { get; }

    /// <summary>
    /// تنفيذ عملية على الاتصال مع إدارة الأخطاء
    /// </summary>
    /// <typeparam name="T">نوع نتيجة العملية</typeparam>
    /// <param name="operation">العملية المطلوب تنفيذها</param>
    /// <param name="operationName">اسم العملية (اختياري)</param>
    /// <returns>نتيجة العملية</returns>
    Task<IOperationResult<T>> ExecuteAsync<T>(
        Func<ITikConnection, Task<T>> operation,
        string? operationName = null);

    /// <summary>
    /// حدث يتغير عند تغيير حالة الاتصال
    /// </summary>
    event EventHandler<LongConnectionStatus> StatusChanged;

    /// <summary>
    /// إغلاق الاتصال بشكل آمن
    /// </summary>
    /// <param name="reason">سبب الإغلاق</param>
    Task CloseAsync(string? reason = null);

    /// <summary>
    /// إعادة تعيين الاتصال (في حالة فقدان الاتصال)
    /// </summary>
    Task<IOperationResult> ReconnectAsync();

    /// <summary>
    /// الحصول على إحصاءات الاتصال
    /// </summary>
    LongConnectionStats GetStats();
}

/// <summary>
/// إحصائيات الاتصال الطويل
/// </summary>
public class LongConnectionStats
{
    /// <summary>
    /// إجمالي العمليات المنفذة
    /// </summary>
    public int TotalOperations { get; set; }

    /// <summary>
    /// العمليات الناجحة
    /// </summary>
    public int SuccessfulOperations { get; set; }

    /// <summary>
    /// العمليات الفاشلة
    /// </summary>
    public int FailedOperations { get; set; }

    /// <summary>
    /// معدل النجاح
    /// </summary>
    public double SuccessRate => TotalOperations > 0 ?
        ((double)SuccessfulOperations / TotalOperations) * 100 : 100;

    /// <summary>
    /// متوسط وقت التنفيذ
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// حجم البيانات المستلمة (بايت)
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// حجم البيانات المرسلة (بايت)
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// آخر عملية تم تنفيذها
    /// </summary>
    public DateTime? LastOperationTime { get; set; }

    /// <summary>
    /// عدد إعادة الاتصال
    /// </summary>
    public int ReconnectCount { get; set; }
}

/// <summary>
/// حالة الاتصال الطويل
/// </summary>
public class LongConnectionStatus
{
    /// <summary>
    /// معرف الاتصال
    /// </summary>
    public Guid ConnectionId { get; set; }

    /// <summary>
    /// حالة الاتصال
    /// </summary>
    public ConnectionStatus Status { get; set; }

    /// <summary>
    /// اسم العملية (إذا كان مرتبطاً بعملية)
    /// </summary>
    public string? OperationName { get; set; }

    /// <summary>
    /// مدة العملية (إذا كان مرتبطاً بعملية)
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// مدة تشغيل الاتصال
    /// </summary>
    public TimeSpan? Uptime { get; set; }

    /// <summary>
    /// عدد العمليات
    /// </summary>
    public int? OperationCount { get; set; }

    /// <summary>
    /// رسالة الخطأ (إذا وجد)
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// سبب الإغلاق (إذا كان الاتصال مغلقاً)
    /// </summary>
    public string? CloseReason { get; set; }

    /// <summary>
    /// وقت التغيير
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// بيانات إضافية
    /// </summary>
    public object? Data { get; set; }
}

/// <summary>
/// حالات الاتصال الممكنة
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// جاري الإنشاء
    /// </summary>
    Creating,

    /// <summary>
    /// متصل
    /// </summary>
    Connected,

    /// <summary>
    /// مفصول
    /// </summary>
    Disconnected,

    /// <summary>
    /// جاري إعادة الاتصال
    /// </summary>
    Reconnecting,

    /// <summary>
    /// بدأت العملية
    /// </summary>
    OperationStarted,

    /// <summary>
    /// اكتملت العملية
    /// </summary>
    OperationCompleted,

    /// <summary>
    /// فشلت العملية
    /// </summary>
    OperationFailed,

    /// <summary>
    /// جاري الإغلاق
    /// </summary>
    Closing,

    /// <summary>
    /// مغلق
    /// </summary>
    Closed,

    /// <summary>
    /// تم التخلص منه
    /// </summary>
    Disposed,

    /// <summary>
    /// خطأ في الاتصال
    /// </summary>
    Error
}