using FluentDevelopment.Tik4net.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using tik4net;

namespace FluentDevelopment.Tik4net;


public interface ITikService
{
    // الاتصالات القصيرة (تلقائية)
    Task<IOperationResult<T>> QuickAsync<T>(Func<ITikConnection, Task<T>> operation, CancellationToken cancellationToken = default);
    IOperationResult<T> Quick<T>(Func<ITikConnection, T> operation);
    IOperationResult Quick(Action<ITikConnection> operation);

    // الاتصالات الطويلة (يدوية بسيطة)
    Task<IOperationResult<ILongConnection>> GetLongConnectionAsync(Func<ITikConnection, Task>? onConnected = null,
            string? connectionName = null,
            Action<LongConnectionStatus>? onStatusChanged = null,
            CancellationToken cancellationToken = default);
    IOperationResult<ILongConnection> GetLongConnection(
            Action<ITikConnection>? operation = null,
            string? connectionName = null,
            Action<LongConnectionStatus>? onStatusChanged = null);

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



