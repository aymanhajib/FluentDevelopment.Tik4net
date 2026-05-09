using FluentDevelopment.Tik4net.Models;
using System;
using System.Threading;
using System.Threading.Tasks;
using tik4net;

namespace FluentDevelopment.Tik4net;


/// <summary>
/// Provides methods for managing MikroTik API connections and operations, including short-lived, long-lived, and background tasks.
/// </summary>
public interface ITikService
{
    /// <summary>
    /// Executes a short-lived asynchronous operation using a MikroTik connection from the pool.
    /// </summary>
    /// <param name="operation">The operation to execute using the connection.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IOperationResult"/> representing the result of the operation.</returns>
    Task<IOperationResult> QuickAsync(Func<ITikConnection, Task> operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a short-lived synchronous operation using a MikroTik connection from the pool.
    /// </summary>
    /// <param name="operation">The operation to execute using the connection.</param>
    /// <returns>An <see cref="IOperationResult"/> representing the result of the operation.</returns>
    IOperationResult Quick(Action<ITikConnection> operation);

    /// <summary>
    /// Creates and returns a long-lived connection for manual operations.
    /// </summary>
    /// <param name="onConnected">An optional callback to execute when the connection is established.</param>
    /// <param name="connectionName">An optional name for the connection.</param>
    /// <param name="onStatusChanged">An optional callback for connection status changes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IOperationResult{ILongConnection}"/> containing the long-lived connection.</returns>
    Task<IOperationResult<ILongConnection>> GetLongConnectionAsync(
        Func<ITikConnection, Task>? onConnected = null,
        string? connectionName = null,
        Action<LongConnectionStatus>? onStatusChanged = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a background asynchronous operation using a MikroTik connection from the pool.
    /// </summary>
    /// <param name="operation">The operation to execute using the connection.</param>
    /// <param name="onCompleted">An optional callback to execute when the operation completes.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IOperationResult"/> representing the result of the operation.</returns>
    Task<IOperationResult> BackgroundAsync(
        Func<ITikConnection, Task> operation,
        Action<IOperationResult>? onCompleted = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates and logs in to the MikroTik API.
    /// </summary>
    /// <param name="host">The host address of the MikroTik device.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="port">The API port (default is 8728).</param>
    /// <returns>A <see cref="LoginResult"/> indicating the outcome of the login attempt.</returns>
    Task<LoginResult> LoginAsync(string host, string username, string password, int port = 8728);

    /// <summary>
    /// Logs out from the MikroTik API and closes all active connections.
    /// </summary>
    /// <returns>A task representing the asynchronous logout operation.</returns>
    Task LogoutAsync();

    /// <summary>
    /// Gets a value indicating whether the service is currently authenticated and logged in.
    /// </summary>
    bool IsLoggedIn { get; }

    /// <summary>
    /// Gets the number of available connections in the pool.
    /// </summary>
    int AvailableConnections { get; }

    /// <summary>
    /// Gets the number of active connections currently in use.
    /// </summary>
    int ActiveConnections { get; }
}
