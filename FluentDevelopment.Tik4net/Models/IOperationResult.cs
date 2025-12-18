
using System;
using System.Threading;
using System.Threading.Tasks;
using tik4net;

namespace FluentDevelopment.Tik4net.Models
{
    public interface IOperationResult<T>
    {
        bool IsSuccess { get; }
        T? Data { get; }
        string? ErrorMessage { get; }
        Exception? Exception { get; }
    }

    public interface IOperationResult
    {
        bool IsSuccess { get; }
        string? ErrorMessage { get; }
        Exception? Exception { get; }
    }

    public interface ITikService
    {
        // الخصائص الحالية
        bool IsLoggedIn { get; }
        int AvailableConnections { get; }
        int ActiveConnections { get; }

        // الأساليب المحسنة
        Task<IOperationResult<T>> QuickAsync<T>(
            Func<ITikConnection, Task<T>> operation,
            CancellationToken cancellationToken = default);

        Task<IOperationResult<ILongConnection>> GetLongConnectionAsync(
            Func<ITikConnection, Task>? onConnected = null,
            CancellationToken cancellationToken = default);

        Task<IOperationResult> BackgroundAsync(
            Func<ITikConnection, Task> operation,
            Action<IOperationResult>? onCompleted = null,
            CancellationToken cancellationToken = default);

        // الأساليب الحالية
        Task<LoginResult> LoginAsync(string host, string username,
            string password, int port = 8728);
        Task LogoutAsync();
    }
}
