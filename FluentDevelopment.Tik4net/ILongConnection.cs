using FluentDevelopment.Tik4net.Models;
using System;
using System.Threading.Tasks;
using tik4net;

namespace FluentDevelopment.Tik4net;

/// <summary>
/// Interface representing a long-term persistent connection with a MikroTik device.
/// </summary>
public interface ILongConnection : IDisposable
{
    /// <summary>
    /// Unique identifier for the connection.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// The underlying Tik4Net connection instance.
    /// </summary>
    ITikConnection Connection { get; }

    /// <summary>
    /// Optional name or alias for the connection.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indicates whether the connection is active and ready for use.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// The exact timestamp when the connection was established.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// The total duration the connection has been active.
    /// </summary>
    TimeSpan Uptime { get; }

    /// <summary>
    /// Total number of operations executed during this connection's lifecycle.
    /// </summary>
    int OperationCount { get; }

    /// <summary>
    /// Executes an operation on the connection with built-in error management.
    /// </summary>
    /// <typeparam name="T">The type of the operation result.</typeparam>
    /// <param name="operation">The functional logic to execute.</param>
    /// <param name="operationName">Optional name for the operation for tracking purposes.</param>
    /// <returns>A task representing the operation result.</returns>
    Task<IOperationResult<T>> ExecuteAsync<T>(
        Func<ITikConnection, Task<T>> operation,
        string? operationName = null);

    /// <summary>
    /// Event triggered whenever the connection status changes.
    /// </summary>
    event EventHandler<LongConnectionStatus> StatusChanged;

    /// <summary>
    /// Gracefully closes the connection.
    /// </summary>
    /// <param name="reason">Optional reason for closing the connection.</param>
    Task CloseAsync(string? reason = null);

    /// <summary>
    /// Attempts to re-establish the connection in case of failure or loss.
    /// </summary>
    /// <returns>The result of the reconnection attempt.</returns>
    Task<IOperationResult> ReconnectAsync();

    /// <summary>
    /// Retrieves the current performance and data statistics for this connection.
    /// </summary>
    LongConnectionStats GetStats();
}

/// <summary>
/// Represents statistical data for a long-lived connection.
/// </summary>
public class LongConnectionStats
{
    /// <summary>
    /// Total number of operations attempted.
    /// </summary>
    public int TotalOperations { get; set; }

    /// <summary>
    /// Number of operations that completed successfully.
    /// </summary>
    public int SuccessfulOperations { get; set; }

    /// <summary>
    /// Number of operations that failed due to errors.
    /// </summary>
    public int FailedOperations { get; set; }

    /// <summary>
    /// Calculated percentage of successful operations.
    /// </summary>
    public double SuccessRate => TotalOperations > 0 ?
        ((double)SuccessfulOperations / TotalOperations) * 100 : 100;

    /// <summary>
    /// Average time taken to execute operations.
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Total volume of data received in bytes.
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// Total volume of data sent in bytes.
    /// </summary>
    public long BytesSent { get; set; }

    /// <summary>
    /// The timestamp of the last executed operation.
    /// </summary>
    public DateTime? LastOperationTime { get; set; }

    /// <summary>
    /// Number of times the connection has been re-established.
    /// </summary>
    public int ReconnectCount { get; set; }
}

/// <summary>
/// Represents the current state and contextual information of a long connection.
/// </summary>
public class LongConnectionStatus
{
    /// <summary>
    /// The ID of the connection the status belongs to.
    /// </summary>
    public Guid ConnectionId { get; set; }

    /// <summary>
    /// Current status of the connection.
    /// </summary>
    public ConnectionStatus Status { get; set; }

    /// <summary>
    /// The name of the operation (if the status update is operation-related).
    /// </summary>
    public string? OperationName { get; set; }

    /// <summary>
    /// Duration of the operation (if applicable).
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Current uptime of the connection.
    /// </summary>
    public TimeSpan? Uptime { get; set; }

    /// <summary>
    /// Current count of operations.
    /// </summary>
    public int? OperationCount { get; set; }

    /// <summary>
    /// Error message detailing the failure (if applicable).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Reason provided for closing the connection.
    /// </summary>
    public string? CloseReason { get; set; }

    /// <summary>
    /// The timestamp when the status change occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Additional metadata or data associated with the status update.
    /// </summary>
    public object? Data { get; set; }
}

/// <summary>
/// Enumerates the possible states of a MikroTik API connection.
/// </summary>
public enum ConnectionStatus
{
    /// <summary> Initial state during connection creation. </summary>
    Creating,

    /// <summary> Connection is successfully established and authenticated. </summary>
    Connected,

    /// <summary> Connection has been lost or manually disconnected. </summary>
    Disconnected,

    /// <summary> Re-authentication or socket recovery is in progress. </summary>
    Reconnecting,

    /// <summary> An operation has started executing. </summary>
    OperationStarted,

    /// <summary> An operation has completed successfully. </summary>
    OperationCompleted,

    /// <summary> An operation has failed to execute. </summary>
    OperationFailed,

    /// <summary> Graceful teardown is in progress. </summary>
    Closing,

    /// <summary> Connection is closed and inactive. </summary>
    Closed,

    /// <summary> Object resources have been released. </summary>
    Disposed,

    /// <summary> A critical communication error has occurred. </summary>
    Error
}