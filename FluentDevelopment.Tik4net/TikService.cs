using FluentDevelopment.Tik4net.Managers;
using FluentDevelopment.Tik4net.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace FluentDevelopment.Tik4net;

/// <summary>
/// Provides high-level management and pooling of MikroTik API connections, including
/// quick operations, long-lived connections, background tasks, and connection statistics.
/// </summary>
public class TikService : ITikService, IDisposable
{
    private readonly TikConnectionPool _pool;
    private readonly ConcurrentDictionary<Guid, ILongConnection> _activeLongConnections = new();
    private readonly ILogger<TikService>? _logger;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="TikService"/> class.
    /// </summary>
    /// <param name="maxPoolSize">The maximum number of connections in the pool.</param>
    /// <param name="logger">The logger instance.</param>
    public TikService(int maxPoolSize = 10, ILogger<TikService>? logger = null)
    {
        _pool = new TikConnectionPool(maxPoolSize);
        _logger = logger;
    }

    /// <summary>
    /// Gets a value indicating whether the service is logged in.
    /// </summary>
    public bool IsLoggedIn => _pool.IsLoggedIn;

    /// <summary>
    /// Gets the number of available connections in the pool.
    /// </summary>
    public int AvailableConnections => _pool.AvailableConnections;

    /// <summary>
    /// Gets the number of active connections in the pool.
    /// </summary>
    public int ActiveConnections => _pool.ActiveConnections;

    /// <summary>
    /// Logs in to the MikroTik API using the specified credentials.
    /// </summary>
    /// <param name="host">The host address.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <param name="port">The port number (default is 8728).</param>
    /// <returns>A <see cref="LoginResult"/> indicating the result of the login operation.</returns>
    public async Task<LoginResult> LoginAsync(string host, string username, string password, int port = 8728)
    {
        return await _pool.LoginAsync(host, username, password, port);
    }

    /// <summary>
    /// Executes a quick asynchronous operation using a pooled connection.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="IOperationResult"/> representing the result.</returns>
    public async Task<IOperationResult> QuickAsync(
        Func<ITikConnection, Task> operation,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        ITikConnection? connection = null;
        try
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug("Starting QuickAsync operation");

            if (!IsLoggedIn)
                return OperationResult.Failure(
                    "Service is not logged in",
                    null,
                    DateTime.UtcNow - startTime);

            connection = await _pool.GetConnectionAsync(cancellationToken);
            await operation(connection);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
                _logger.LogInformation("QuickAsync operation completed successfully");

            return OperationResult.Success(
                DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(
                GetMessageException(ex, "QuickAsync"),
                ex,
                DateTime.UtcNow - startTime);
        }
        finally
        {
            if (connection != null)
            {
                await _pool.ReturnConnectionAsync(connection);
            }
        }
    }

    /// <summary>
    /// Executes a quick synchronous operation using a pooled connection.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>An <see cref="IOperationResult"/> representing the result.</returns>
    public IOperationResult Quick(Action<ITikConnection> operation)
    {
        var startTime = DateTime.UtcNow;
        ITikConnection? connection = null;
        try
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug("Starting Quick operation");

            if (!IsLoggedIn)
                return OperationResult.Failure(
                    "Service is not logged in",
                    null,
                    DateTime.UtcNow - startTime);

            connection = _pool.GetConnection();
            operation(connection);

            if (_logger?.IsEnabled(LogLevel.Information) == true)
                _logger.LogInformation("Quick operation completed successfully");

            return OperationResult.Success(
                DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(
                GetMessageException(ex, "Quick"),
                ex,
                DateTime.UtcNow - startTime);
        }
        finally
        {
            if (connection != null)
            {
                _pool.ReturnConnection(connection);
            }
        }
    }

    /// <summary>
    /// Gets a long-lived connection for advanced operations.
    /// </summary>
    /// <param name="operation">An optional operation to execute on connection.</param>
    /// <param name="connectionName">The name of the connection.</param>
    /// <param name="onStatusChanged">Callback for status changes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="IOperationResult{ILongConnection}"/> representing the result.</returns>
    public async Task<IOperationResult<ILongConnection>> GetLongConnectionAsync(
            Func<ITikConnection, Task>? operation = null,
            string? connectionName = null,
            Action<LongConnectionStatus>? onStatusChanged = null,
            CancellationToken cancellationToken = default)
    {
        connectionName ??= "LongConnection";
        var startTime = DateTime.UtcNow;
        var connectionId = Guid.NewGuid();

        try
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug("Creating long connection {ConnectionId}", connectionId);

            if (!IsLoggedIn)
                return OperationResult<ILongConnection>.Failure(
                    "Service is not logged in",
                    null,
                    DateTime.UtcNow - startTime);

            var connection = await _pool.GetConnectionAsync(cancellationToken);

            var longConnection = new LongConnection(
                connectionId,
                connection,
                connectionName,
                async (conn) =>
                {
                    _activeLongConnections.TryRemove(connectionId, out _);
                    await _pool.ReturnConnectionAsync(conn);

                    onStatusChanged?.Invoke(new LongConnectionStatus
                    {
                        ConnectionId = connectionId,
                        Status = ConnectionStatus.Disposed,
                        Timestamp = DateTime.UtcNow
                    });
                },
                onStatusChanged,
                _pool.ReconnectAsync,
                _logger);

            _activeLongConnections.TryAdd(connectionId, longConnection);

            if (operation != null)
            {
                try
                {
                    await operation(connection);
                }
                catch (Exception ex)
                {
                    if (_logger?.IsEnabled(LogLevel.Error) == true)
                        _logger.LogError(ex, "Error in onConnected callback for connection {ConnectionId}", connectionId);
                }
            }

            if (_logger?.IsEnabled(LogLevel.Information) == true)
                _logger.LogInformation("Long connection {ConnectionId} created successfully", connectionId);

            return OperationResult<ILongConnection>.Success(
                longConnection,
                DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            return OperationResult<ILongConnection>.Failure(
                GetMessageException(ex, "GetLongConnectionAsync"),
                ex,
                DateTime.UtcNow - startTime);
        }
    }

    /// <summary>
    /// Runs an operation in the background using a pooled connection.
    /// </summary>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="onCompleted">Callback when the operation completes.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An <see cref="IOperationResult"/> representing the result.</returns>
    public async Task<IOperationResult> BackgroundAsync(
        Func<ITikConnection, Task> operation,
        Action<IOperationResult>? onCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var operationId = Guid.NewGuid();

        try
        {
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
                _logger.LogDebug("Starting background operation {OperationId}", operationId);

            if (!IsLoggedIn)
            {
                var result = OperationResult.Failure(
                    "Service is not logged in",
                    null,
                    DateTime.UtcNow - startTime);
                onCompleted?.Invoke(result);
                return result;
            }

            var connection = await _pool.GetConnectionAsync(cancellationToken);

            _ = Task.Run(async () =>
            {
                IOperationResult backgroundResult;

                try
                {
                    await operation(connection);
                    backgroundResult = OperationResult.Success(
                        DateTime.UtcNow - startTime);

                    if (_logger?.IsEnabled(LogLevel.Information) == true)
                        _logger.LogInformation("Background operation {OperationId} completed successfully", operationId);
                }
                catch (Exception ex)
                {
                    backgroundResult = OperationResult.Failure(
                        GetMessageException(ex, "Background"),
                        ex,
                        DateTime.UtcNow - startTime);

                    if (_logger?.IsEnabled(LogLevel.Error) == true)
                        _logger.LogError(ex, "Background operation {OperationId} failed", operationId);
                }
                finally
                {
                    await _pool.ReturnConnectionAsync(connection);
                }

                onCompleted?.Invoke(backgroundResult);
            }, cancellationToken);

            return OperationResult.Success(DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
                _logger.LogError(ex, "Failed to start background operation {OperationId}", operationId);

            var result = OperationResult.Failure(
                GetMessageException(ex, "Background"),
                ex,
                DateTime.UtcNow - startTime);

            onCompleted?.Invoke(result);
            return result;
        }
    }

    /// <summary>
    /// Returns a user-friendly error message for a given exception and action name.
    /// </summary>
    /// <param name="ex">The exception that occurred.</param>
    /// <param name="actionName">The name of the action being performed when the exception occurred.</param>
    /// <returns>A string containing a user-friendly error message.</returns>
    protected virtual string GetMessageException(Exception ex, string actionName)
    {
        // سنستخدم المتغير message لتخزين ما سيظهر للمستخدم أو الواجهة
        string message;
        if (ex is NotImplementedException)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
                _logger.LogError(ex, "{ActionName} operation not supported", actionName);
            message = "هذه العملية غير مدعومة في إصدار المايكروتك الحالي.";
        }
        else if (ex is InvalidOperationException || ex is OperationCanceledException)
        {
            if (_logger?.IsEnabled(LogLevel.Warning) == true)
                _logger.LogWarning(ex, "{ActionName} operation was cancelled", actionName);
            message = "تم إلغاء العملية أو أنها غير صالحة حالياً.";
        }
        // الخطأ الأكثر شيوعاً: السيرفر رفض الأمر لسبب منطقي (مثل كلمة سر ضعيفة أو مستخدم موجود)
        else if (ex is TikCommandTrapException trapEx)
        {
            if (_logger?.IsEnabled(LogLevel.Warning) == true)
                _logger.LogWarning(trapEx, "{ActionName}: Trap error from RouterOS", actionName);
            // جلب الرسالة القادمة من المايكروتك نفسه (مثل: "user already exists")
            message = $"خطأ من السيرفر: {trapEx.Code} - {trapEx.Message}";
        }
        else if (ex is TikCommandFatalException fatalEx)
        {
            if (_logger?.IsEnabled(LogLevel.Critical) == true)
                _logger.LogCritical(fatalEx, "{ActionName}: Fatal connection error", actionName);
            message = "انقطع الاتصال بالسيرفر بشكل مفاجئ. يرجى التحقق من الشبكة.";
        }
        else if (ex is TikNoSuchCommandException)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
                _logger.LogError(ex, "{ActionName}: Invalid API path", actionName);
            message = "المسار البرمجي غير صحيح. يرجى التأكد من كتابة الأمر بدقة.";
        }
        else if (ex is TikNoSuchItemException)
        {
            if (_logger?.IsEnabled(LogLevel.Warning) == true)
                _logger.LogWarning(ex, "{ActionName}: Item not found", actionName);
            message = "العنصر المطلوب (مستخدم/سجل) غير موجود في السيرفر.";
        }
        else if (ex is TikAlreadyHaveSuchItemException)
        {
            if (_logger?.IsEnabled(LogLevel.Warning) == true)
                _logger.LogWarning(ex, "{ActionName}: Duplicate item", actionName);
            message = "هذا العنصر (الاسم أو المعرف) موجود مسبقاً في السيرفر.";
        }
        else if (ex is TikCommandUnexpectedResponseException unexEx)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
                _logger.LogError(unexEx, "{ActionName}: Unexpected response", actionName);
            message = "وصل رد غير متوقع من السيرفر. قد يكون هناك اختلاف في الإصدارات.";
        }
        else if (ex is TikCommandAmbiguousResultException)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
                _logger.LogError(ex, "{ActionName}: Ambiguous result", actionName);
            message = "النتيجة غامضة؛ الأمر أعاد أكثر من قيمة بينما المتوقع قيمة واحدة.";
        }
        else
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
                _logger.LogError(ex, "{ActionName}: operation failed with unknown error", actionName);
            message = "حدث خطأ غير معروف أثناء تنفيذ العملية.";
        }

        return message;
    }

    // 4. LongConnection المحسنة
    private class LongConnection : ILongConnection
    {
        private readonly Guid _id;
        private readonly ITikConnection _connection;
        private readonly string _name;
        private readonly Func<ITikConnection, Task> _onDispose;
        private readonly Action<LongConnectionStatus>? _onStatusChanged;
        private readonly ILogger? _logger;
        private bool _disposed = false;
        private readonly Stopwatch _connectionLifetime = Stopwatch.StartNew();
        private int _operationCount = 0;
        private int _successfulOperations = 0;
        private int _failedOperations = 0;
        private long _totalExecutionTimeTicks = 0;
        private long _bytesReceived = 0;
        private long _bytesSent = 0;
        private int _reconnectCount = 0;
        private DateTime? _lastOperationTime = null;
        private readonly Func<ITikConnection, Task<bool>> _reconnectAsync;

        public event EventHandler<LongConnectionStatus>? StatusChanged;

        public Guid Id => _id;
        public string Name => _name;
        public ITikConnection Connection => _connection;
        public bool IsActive => !_disposed && _connection?.IsOpened == true;
        public DateTime CreatedAt { get; }
        public TimeSpan Uptime => _connectionLifetime.Elapsed;
        public int OperationCount => _operationCount;

        public LongConnection(Guid id, ITikConnection connection, string name,
            Func<ITikConnection, Task> onDispose,
            Action<LongConnectionStatus>? onStatusChanged,
            Func<ITikConnection, Task<bool>> reconnectAsync,
            ILogger? logger = null)
        {
            _id = id;
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _name = name;
            _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            _reconnectAsync = reconnectAsync;
            _onStatusChanged = onStatusChanged;
            _logger = logger;
            CreatedAt = DateTime.UtcNow;

            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("Enhanced long connection {Name} ({Id}) created", name, id);
            }

            RaiseStatusChanged(new LongConnectionStatus
            {
                ConnectionId = _id,
                Status = ConnectionStatus.Connected,
                Uptime = TimeSpan.Zero,
                Timestamp = DateTime.UtcNow
            });
            _reconnectAsync = reconnectAsync;
        }

        public async Task<IOperationResult<T>> ExecuteAsync<T>(
            Func<ITikConnection, Task<T>> operation,
            string? operationName = null)
        {
            if (_disposed)
                return OperationResult<T>.Failure("Connection is disposed");

            operationName ??= $"Operation_{Interlocked.Increment(ref _operationCount)}";
            var operationId = Guid.NewGuid();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                Interlocked.Increment(ref _operationCount);
                _lastOperationTime = DateTime.UtcNow;

                RaiseStatusChanged(new LongConnectionStatus
                {
                    ConnectionId = _id,
                    Status = ConnectionStatus.OperationStarted,
                    OperationName = operationName,
                    Timestamp = DateTime.UtcNow,
                    Data = new { OperationId = operationId }
                });

                // تنفيذ العملية
                var result = await operation(_connection);
                stopwatch.Stop();

                // تحديث الإحصاءات
                Interlocked.Increment(ref _successfulOperations);
                Interlocked.Add(ref _totalExecutionTimeTicks, stopwatch.Elapsed.Ticks);

                // محاكاة حجم البيانات (في تطبيق حقيقي، ستحتاج لتتبع البيانات الفعلية)
                Interlocked.Add(ref _bytesReceived, 1024); // مثال: 1KB مستلم
                Interlocked.Add(ref _bytesSent, 512); // مثال: 512B مرسل

                if (_logger?.IsEnabled(LogLevel.Debug) == true)
                {
                    _logger.LogDebug("Long connection {Id} operation {Name} completed in {Elapsed}ms",
                        _id, operationName, stopwatch.ElapsedMilliseconds);
                }

                RaiseStatusChanged(new LongConnectionStatus
                {
                    ConnectionId = _id,
                    Status = ConnectionStatus.OperationCompleted,
                    OperationName = operationName,
                    Duration = stopwatch.Elapsed,
                    Uptime = Uptime,
                    Timestamp = DateTime.UtcNow,
                    Data = new { OperationId = operationId, Duration = stopwatch.Elapsed }
                });

                return OperationResult<T>.Success(result, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Interlocked.Increment(ref _failedOperations);

                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Long connection {Id} operation {Name} failed after {Elapsed}ms",
                        _id, operationName, stopwatch.ElapsedMilliseconds);
                }

                RaiseStatusChanged(new LongConnectionStatus
                {
                    ConnectionId = _id,
                    Status = ConnectionStatus.OperationFailed,
                    OperationName = operationName,
                    Error = ex.Message,
                    Duration = stopwatch.Elapsed,
                    Uptime = Uptime,
                    Timestamp = DateTime.UtcNow,
                    Data = new { OperationId = operationId, Exception = ex }
                });

                // التحقق مما إذا كان الاتصال لا يزال نشطاً
                if (!_connection.IsOpened)
                {
                    RaiseStatusChanged(new LongConnectionStatus
                    {
                        ConnectionId = _id,
                        Status = ConnectionStatus.Disconnected,
                        Error = "Connection lost during operation",
                        Timestamp = DateTime.UtcNow
                    });
                }

                return OperationResult<T>.Failure($"Operation failed: {ex.Message}", ex, stopwatch.Elapsed);
            }
        }

        public async Task<IOperationResult> ReconnectAsync()
        {
            if (_disposed)
                return OperationResult.Failure("Connection is disposed");

            try
            {
                RaiseStatusChanged(new LongConnectionStatus
                {
                    ConnectionId = _id,
                    Status = ConnectionStatus.Reconnecting,
                    Timestamp = DateTime.UtcNow
                });

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Attempting to reconnect long connection {Id}", _id);
                }

                // إغلاق الاتصال الحالي إذا كان مفتوحاً
                if (_connection.IsOpened)
                {
                    _connection.Close();
                }

                var res = await _reconnectAsync(_connection);

                if (res)
                {
                    Interlocked.Increment(ref _reconnectCount);

                    RaiseStatusChanged(new LongConnectionStatus
                    {
                        ConnectionId = _id,
                        Status = ConnectionStatus.Connected,
                        Uptime = Uptime,
                        Timestamp = DateTime.UtcNow,
                        Data = new { ReconnectCount = _reconnectCount }
                    });

                    if (_logger?.IsEnabled(LogLevel.Information) == true)
                    {
                        _logger.LogInformation("Long connection {Id} reconnected successfully", _id);
                    }

                    return OperationResult.Success(Uptime);
                }
                else
                {
                    RaiseStatusChanged(new LongConnectionStatus
                    {
                        ConnectionId = _id,
                        Status = ConnectionStatus.Error,
                        Error = $"Reconnection failed",
                        Timestamp = DateTime.UtcNow
                    });
                    return OperationResult.Failure($"Reconnection failed");
                }
            }
            catch (Exception ex)
            {
                RaiseStatusChanged(new LongConnectionStatus
                {
                    ConnectionId = _id,
                    Status = ConnectionStatus.Error,
                    Error = $"Reconnection failed: {ex.Message}",
                    Timestamp = DateTime.UtcNow
                });

                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Failed to reconnect long connection {Id}", _id);
                }

                return OperationResult.Failure($"Reconnection failed: {ex.Message}", ex);
            }
        }

        public async Task CloseAsync(string? reason = null)
        {
            if (_disposed) return;

            try
            {
                RaiseStatusChanged(new LongConnectionStatus
                {
                    ConnectionId = _id,
                    Status = ConnectionStatus.Closing,
                    CloseReason = reason,
                    Timestamp = DateTime.UtcNow
                });

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Closing long connection {Id}. Reason: {Reason}",
                        _id, reason ?? "Manual closure");
                }

                // إغلاق الاتصال الأساسي
                if (_connection.IsOpened)
                {
                    _connection.Close();
                }

                // إشعار بالإغلاق
                await _onDispose(_connection);

                RaiseStatusChanged(new LongConnectionStatus
                {
                    ConnectionId = _id,
                    Status = ConnectionStatus.Closed,
                    CloseReason = reason,
                    Uptime = Uptime,
                    OperationCount = _operationCount,
                    Timestamp = DateTime.UtcNow
                });

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Long connection {Id} closed successfully after {Uptime}",
                        _id, Uptime);
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Error closing long connection {Id}", _id);
                }
                throw;
            }
        }

        public LongConnectionStats GetStats()
        {
            var avgTicks = _operationCount > 0 ? _totalExecutionTimeTicks / _operationCount : 0;

            return new LongConnectionStats
            {
                TotalOperations = _operationCount,
                SuccessfulOperations = _successfulOperations,
                FailedOperations = _failedOperations,
                AverageExecutionTime = TimeSpan.FromTicks(avgTicks),
                BytesReceived = _bytesReceived,
                BytesSent = _bytesSent,
                LastOperationTime = _lastOperationTime,
                ReconnectCount = _reconnectCount
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _connectionLifetime.Stop();

            try
            {
                // استخدام CloseAsync إذا أمكن
                CloseAsync("Dispose called").ConfigureAwait(false).GetAwaiter().GetResult();

                RaiseStatusChanged(new LongConnectionStatus
                {
                    ConnectionId = _id,
                    Status = ConnectionStatus.Disposed,
                    Uptime = Uptime,
                    OperationCount = _operationCount,
                    Timestamp = DateTime.UtcNow
                });

                if (_logger?.IsEnabled(LogLevel.Information) == true)
                {
                    _logger.LogInformation("Enhanced long connection {Name} ({Id}) disposed after {Uptime} with {Ops} operations",
                        _name, _id, Uptime, _operationCount);
                }
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                {
                    _logger.LogError(ex, "Error disposing long connection {Id}", _id);
                }
            }
        }

        private void RaiseStatusChanged(LongConnectionStatus status)
        {
            // استدعاء callback الخارجي
            _onStatusChanged?.Invoke(status);

            // رفع الحدث المحلي
            StatusChanged?.Invoke(this, status);
        }
    }

    /// <summary>
    /// Logs out and disposes all long connections and the connection pool.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LogoutAsync()
    {
        if (_logger?.IsEnabled(LogLevel.Information) == true)
            _logger.LogInformation("Logging out, closing {Count} long connections", _activeLongConnections.Count);

        foreach (var (id, connection) in _activeLongConnections.ToList())
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                if (_logger?.IsEnabled(LogLevel.Error) == true)
                    _logger.LogError(ex, "Error disposing long connection {ConnectionId}", id);
            }
        }

        _activeLongConnections.Clear();
        await _pool.LogoutAsync();
    }

    /// <summary>
    /// Disposes the TikService and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            LogoutAsync().GetAwaiter().GetResult();
            _pool?.Dispose();
            if (_logger?.IsEnabled(LogLevel.Information) == true)
                _logger.LogInformation("TikService disposed successfully");
        }
        catch (Exception ex)
        {
            if (_logger?.IsEnabled(LogLevel.Error) == true)
                _logger.LogError(ex, "Error during TikService disposal");
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Gets statistics about the current connections and pool.
    /// </summary>
    /// <returns>A <see cref="ConnectionStatistics"/> object.</returns>
    public ConnectionStatistics GetStatistics()
    {
        return new ConnectionStatistics
        {
            TotalLongConnections = _activeLongConnections.Count,
            AvailablePoolConnections = AvailableConnections,
            ActivePoolConnections = ActiveConnections,
            IsLoggedIn = IsLoggedIn,
            LongConnectionIds = _activeLongConnections.Keys.ToList()
        };
    }
}
