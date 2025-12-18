using FluentDevelopment.Tik4net.Managers;
using FluentDevelopment.Tik4net.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using tik4net;

namespace FluentDevelopment.Tik4net;

public class TikService : ITikService, IDisposable
{
    private readonly TikConnectionPool _pool;
    private readonly ConcurrentDictionary<Guid, ILongConnection> _activeLongConnections = new();
    private readonly ILogger<TikService>? _logger;
    private bool _disposed = false;

    public TikService(int maxPoolSize = 10 , ILogger<TikService>? logger = null)
    {
        _pool = new TikConnectionPool(maxPoolSize);
        _logger = logger;
    }

    // الخصائص الحالية
    public bool IsLoggedIn => _pool.IsLoggedIn;
    public int AvailableConnections => _pool.AvailableConnections;
    public int ActiveConnections => _pool.ActiveConnections;

    public async Task<LoginResult> LoginAsync(string host, string username, string password, int port = 8728)
    {
        return await _pool.LoginAsync(host, username, password, port);
    }

    // 1. QuickAsync المحسنة
    public async Task<IOperationResult<T>> QuickAsync<T>(
        Func<ITikConnection, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger?.LogDebug("Starting QuickAsync operation");

            // التحقق من حالة الخدمة
            if (!IsLoggedIn)
                return OperationResult<T>.Failure(
                    "Service is not logged in",
                    null,
                    DateTime.UtcNow - startTime);

            // الحصول على اتصال
            var connection = await _pool.GetConnectionAsync(cancellationToken);

            try
            {
                // تنفيذ العملية
                var result = await operation(connection);

                _logger?.LogInformation("QuickAsync operation completed successfully");
                return OperationResult<T>.Success(
                    result,
                    DateTime.UtcNow - startTime);
            }
            finally
            {
                // إرجاع الاتصال للـ Pool حتى في حالة الخطأ
                await _pool.ReturnConnectionAsync(connection);
            }
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogWarning(ex, "QuickAsync operation was cancelled");
            return OperationResult<T>.Failure(
                "Operation was cancelled",
                ex,
                DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "QuickAsync operation failed");
            return OperationResult<T>.Failure(
                $"Operation failed: {ex.Message}",
                ex,
                DateTime.UtcNow - startTime);
        }
    }

    // 2. GetLongConnectionAsync المحسنة
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
            _logger?.LogDebug("Creating long connection {ConnectionId}", connectionId);

            if (!IsLoggedIn)
                return OperationResult<ILongConnection>.Failure(
                    "Service is not logged in",
                    null,
                    DateTime.UtcNow - startTime);

            var connection = await _pool.GetConnectionAsync(cancellationToken);

            // إنشاء اتصال طويل
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

            // إضافة للقائمة النشطة
            _activeLongConnections.TryAdd(connectionId, longConnection);

            // تنفيذ callback إذا وُجد
            if (operation != null)
            {
                try
                {
                    await operation(connection);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex,
                        "Error in onConnected callback for connection {ConnectionId}",
                        connectionId);
                    // لا نعيد فشلاً هنا لأن الاتصال تم إنشاؤه بنجاح
                }
            }

            _logger?.LogInformation(
                "Long connection {ConnectionId} created successfully",
                connectionId);

            return OperationResult<ILongConnection>.Success(
                longConnection,
                DateTime.UtcNow - startTime);
        }
        catch (OperationCanceledException ex)
        {
            _logger?.LogWarning(ex, "GetLongConnectionAsync was cancelled");
            return OperationResult<ILongConnection>.Failure(
                "Operation was cancelled",
                ex,
                DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create long connection");
            return OperationResult<ILongConnection>.Failure(
                $"Failed to create long connection: {ex.Message}",
                ex,
                DateTime.UtcNow - startTime);
        }
    }

    // 3. BackgroundAsync المحسنة
    public async Task<IOperationResult> BackgroundAsync(
        Func<ITikConnection, Task> operation,
        Action<IOperationResult>? onCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var operationId = Guid.NewGuid();

        try
        {
            _logger?.LogDebug(
                "Starting background operation {OperationId}",
                operationId);

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

            // تشغيل العملية في الخلفية
            _ = Task.Run(async () =>
            {
                IOperationResult backgroundResult;

                try
                {
                    await operation(connection);
                    backgroundResult = OperationResult.Success(
                        DateTime.UtcNow - startTime);

                    _logger?.LogInformation(
                        "Background operation {OperationId} completed successfully",
                        operationId);
                }
                catch (Exception ex)
                {
                    backgroundResult = OperationResult.Failure(
                        $"Background operation failed: {ex.Message}",
                        ex,
                        DateTime.UtcNow - startTime);

                    _logger?.LogError(ex,
                        "Background operation {OperationId} failed",
                        operationId);
                }
                finally
                {
                    await _pool.ReturnConnectionAsync(connection);
                }

                // استدعاء callback عند الانتهاء
                onCompleted?.Invoke(backgroundResult);
            }, cancellationToken);

            return OperationResult.Success(DateTime.UtcNow - startTime);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Failed to start background operation {OperationId}",
                operationId);

            var result = OperationResult.Failure(
                $"Failed to start background operation: {ex.Message}",
                ex,
                DateTime.UtcNow - startTime);

            onCompleted?.Invoke(result);
            return result;
        }
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

            _logger?.LogInformation("Enhanced long connection {Name} ({Id}) created", name, id);

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

                _logger?.LogDebug("Long connection {Id} operation {Name} completed in {Elapsed}ms",
                    _id, operationName, stopwatch.ElapsedMilliseconds);

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

                _logger?.LogError(ex, "Long connection {Id} operation {Name} failed after {Elapsed}ms",
                    _id, operationName, stopwatch.ElapsedMilliseconds);

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

                _logger?.LogInformation("Attempting to reconnect long connection {Id}", _id);

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

                    _logger?.LogInformation("Long connection {Id} reconnected successfully", _id);

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

                _logger?.LogError(ex, "Failed to reconnect long connection {Id}", _id);

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

                _logger?.LogInformation("Closing long connection {Id}. Reason: {Reason}",
                    _id, reason ?? "Manual closure");

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

                _logger?.LogInformation("Long connection {Id} closed successfully after {Uptime}",
                    _id, Uptime);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error closing long connection {Id}", _id);
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

                _logger?.LogInformation("Enhanced long connection {Name} ({Id}) disposed after {Uptime} with {Ops} operations",
                    _name, _id, Uptime, _operationCount);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing long connection {Id}", _id);
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

    // 5. LogoutAsync المحسنة
    public async Task LogoutAsync()
    {
        _logger?.LogInformation("Logging out, closing {Count} long connections",
            _activeLongConnections.Count);

        // إغلاق جميع الاتصالات الطويلة
        foreach (var (id, connection) in _activeLongConnections.ToList())
        {
            try
            {
                connection.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Error disposing long connection {ConnectionId}",
                    id);
            }
        }

        _activeLongConnections.Clear();
        await _pool.LogoutAsync();
    }

    // 6. Dispose المحسنة
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            LogoutAsync().GetAwaiter().GetResult();
            _pool?.Dispose();
            _logger?.LogInformation("TikService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during TikService disposal");
        }
    }

    // 7. طريقة مساعدة للحصول على إحصاءات
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