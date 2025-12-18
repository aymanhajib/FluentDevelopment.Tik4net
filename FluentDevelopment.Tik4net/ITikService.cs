using FluentDevelopment.Tik4net.Models;
using tik4net;

namespace FluentDevelopment.Tik4net;


public interface ITikService
{
    // الاتصالات القصيرة (تلقائية)
    Task<IOperationResult<T>> QuickAsync<T>(Func<ITikConnection, Task<T>> operation, CancellationToken cancellationToken = default);

    // الاتصالات الطويلة (يدوية بسيطة)
    Task<IOperationResult<ILongConnection>> GetLongConnectionAsync(Func<ITikConnection, Task>? onConnected = null,
            string? connectionName = null,
            Action<LongConnectionStatus>? onStatusChanged = null,
            CancellationToken cancellationToken = default);

    // عمليات الخلفية (تلقائية)
    Task<IOperationResult> BackgroundAsync(Func<ITikConnection, Task> operation,Action<IOperationResult>? onCompleted = null,
        CancellationToken cancellationToken = default);

    // تسجيل الدخول
    Task<LoginResult> LoginAsync(string host, string username, string password, int port = 8728);
    Task LogoutAsync();
    bool IsLoggedIn { get; }

    // معلومات الـ Pool
    int AvailableConnections { get; }
    int ActiveConnections { get; }
}



