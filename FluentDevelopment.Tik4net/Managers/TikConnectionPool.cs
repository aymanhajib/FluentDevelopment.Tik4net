

using FluentDevelopment.Tik4net.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using tik4net;

namespace FluentDevelopment.Tik4net.Managers;

/// <summary>
/// Provides a pool of reusable connections to a MikroTik device using the tik4net API.
/// </summary>
public class TikConnectionPool : IDisposable
{
    private readonly ConcurrentQueue<ITikConnection> _availableConnections;
    private readonly ConcurrentDictionary<ITikConnection, bool> _activeConnections;
    private readonly SemaphoreSlim _poolSemaphore;
    private readonly int _maxPoolSize;
    private TikCredentials? _credentials;
    private bool _disposed = false;
    private readonly Timer _healthCheckTimer;

    /// <summary>
    /// Initializes a new instance of the <see cref="TikConnectionPool"/> class.
    /// </summary>
    /// <param name="maxPoolSize">The maximum number of connections in the pool.</param>
    public TikConnectionPool(int maxPoolSize = 10)
    {
        _maxPoolSize = maxPoolSize;
        _availableConnections = new ConcurrentQueue<ITikConnection>();
        _activeConnections = new ConcurrentDictionary<ITikConnection, bool>();
        _poolSemaphore = new SemaphoreSlim(maxPoolSize, maxPoolSize);

        // فحص صحة الاتصالات كل 30 ثانية
        _healthCheckTimer = new Timer(HealthCheckConnections, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Gets a value indicating whether the pool is logged in with valid credentials.
    /// </summary>
    public bool IsLoggedIn => _credentials?.IsValid == true;

    /// <summary>
    /// Gets the number of available connections in the pool.
    /// </summary>
    public int AvailableConnections => _availableConnections.Count;

    /// <summary>
    /// Gets the number of active (checked out) connections.
    /// </summary>
    public int ActiveConnections => _activeConnections.Count;

    /// <summary>
    /// Gets the total number of connections (available + active).
    /// </summary>
    public int TotalConnections => AvailableConnections + ActiveConnections;

    /// <summary>
    /// Logs in to the MikroTik device and initializes the connection pool.
    /// </summary>
    /// <param name="host">The host address of the MikroTik device.</param>
    /// <param name="username">The username for authentication.</param>
    /// <param name="password">The password for authentication.</param>
    /// <param name="port">The port to connect to (default is 8728).</param>
    /// <returns>A <see cref="LoginResult"/> indicating success or failure.</returns>
    public async Task<LoginResult> LoginAsync(string host, string username, string password, int port = 8728)
    {
        try
        {
            // التحقق من صحة المدخلات
            if (string.IsNullOrWhiteSpace(host))
                return LoginResult.Failure("يجب إدخال عنوان الخادم");

            if (string.IsNullOrWhiteSpace(username))
                return LoginResult.Failure("يجب إدخال اسم المستخدم");

            // اختبار الاتصال أولاً
            var testConnection = await CreateConnectionAsync(host, username, password, port);
            if (testConnection != null && testConnection.IsOpened)
            {
                testConnection.Close();
                testConnection.Dispose();

                // حفظ بيانات الاعتماد
                _credentials = new TikCredentials
                {
                    Host = host,
                    Username = username,
                    Password = password,
                    Port = port,
                    IsValid = true
                };

                // إنشاء اتصالات ابتدائية في الـ Pool
                await InitializePoolAsync(2);
                return LoginResult.Success();
            }
            else
            {
                return LoginResult.Failure("فشل في الاتصال بالخادم.\nتحقق من البيانات وإمكانية الوصول");
            }
        }
        catch (TikConnectionException ex)
        {
            return LoginResult.Failure($"خطأ في الاتصال: {ex.Message}");
        }
        catch (TimeoutException)
        {
            return LoginResult.Failure("انتهت مهلة الاتصال.\nتحقق من إمكانية الوصول للخادم");
        }
        catch (UnauthorizedAccessException)
        {
            return LoginResult.Failure("بيانات الدخول غير صحيحة.\nتحقق من اسم المستخدم وكلمة المرور");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"خطأ غير متوقع في تسجيل الدخول: {ex}");
            return LoginResult.Failure($"حدث خطأ غير متوقع: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs out and disposes all connections in the pool.
    /// </summary>
    public async Task LogoutAsync()
    {
        await DisposeAllConnectionsAsync();
        _credentials = null;
    }

    /// <summary>
    /// Gets a connection from the pool asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>An available <see cref="ITikConnection"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if not logged in.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the pool is disposed.</exception>
    public async Task<ITikConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLoggedIn)
            throw new InvalidOperationException("يجب تسجيل الدخول أولاً");

        ObjectDisposedException.ThrowIf(_disposed, nameof(TikConnectionPool));

        await _poolSemaphore.WaitAsync(cancellationToken);
        try
        {
            ITikConnection? connection = null;

            if (_availableConnections.TryDequeue(out connection))
            {
                if (!await IsConnectionHealthyAsync(connection))
                {
                    connection.Dispose();
                    connection = await CreateConnectionAsync();
                }
            }
            else
            {
                if (TotalConnections < _maxPoolSize)
                {
                    connection = await CreateConnectionAsync();
                }
            }

            if (connection != null)
            {
                _activeConnections[connection] = true;
                return connection;
            }

            throw new InvalidOperationException("لا يمكن إنشاء اتصال جديد");
        }
        catch
        {
            _poolSemaphore.Release();
            throw;
        }
    }

    /// <summary>
    /// Gets a connection from the pool synchronously.
    /// </summary>
    /// <returns>An available <see cref="ITikConnection"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if not logged in.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if the pool is disposed.</exception>
    public ITikConnection GetConnection()
    {
        if (!IsLoggedIn)
            throw new InvalidOperationException("يجب تسجيل الدخول أولاً");

        ObjectDisposedException.ThrowIf(_disposed, nameof(TikConnectionPool));

        try
        {
            ITikConnection? connection = null;

            if (_availableConnections.TryDequeue(out connection))
            {
                if (!IsConnectionHealthy(connection))
                {
                    connection.Dispose();
                    connection = CreateConnection();
                }
            }
            else
            {
                if (TotalConnections < _maxPoolSize)
                {
                    connection = CreateConnection();
                }
            }

            if (connection != null)
            {
                _activeConnections[connection] = true;
                return connection;
            }

            throw new InvalidOperationException("لا يمكن إنشاء اتصال جديد");
        }
        catch
        {
            throw;
        }
    }

    /// <summary>
    /// Attempts to reconnect the specified connection asynchronously.
    /// </summary>
    /// <param name="connection">The connection to reconnect.</param>
    /// <returns>True if reconnection was successful; otherwise, false.</returns>
    public async Task<bool> ReconnectAsync(ITikConnection connection)
    {
        if (_credentials == null) return false;

        return await Task.Run(async () =>
        {
            try
            {
                await connection.OpenAsync(_credentials.Host, _credentials.Port, _credentials.Username, _credentials.Password);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"فشل محاولة اعادة الاتصال: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Returns a connection to the pool asynchronously.
    /// </summary>
    /// <param name="connection">The connection to return.</param>
    public async Task ReturnConnectionAsync(ITikConnection connection)
    {
        if (_disposed || connection == null)
        {
            connection?.Dispose();
            return;
        }

        try
        {
            _activeConnections.TryRemove(connection, out _);

            // التحقق من صحة الاتصال قبل إعادته
            if (await IsConnectionHealthyAsync(connection) && _availableConnections.Count < _maxPoolSize)
            {
                _availableConnections.Enqueue(connection);
            }
            else
            {
                // استبدال الاتصال المعطوب
                connection.Dispose();
                if (TotalConnections < _maxPoolSize)
                {
                    var newConnection = await CreateConnectionAsync();
                    if (newConnection != null)
                    {
                        _availableConnections.Enqueue(newConnection);
                    }
                }
            }
        }
        catch { }
        finally
        {
            _poolSemaphore.Release();
        }
    }

    /// <summary>
    /// Returns a connection to the pool synchronously.
    /// </summary>
    /// <param name="connection">The connection to return.</param>
    public void ReturnConnection(ITikConnection connection)
    {
        if (_disposed || connection == null)
        {
            connection?.Dispose();
            return;
        }

        try
        {
            _activeConnections.TryRemove(connection, out _);

            // التحقق من صحة الاتصال قبل إعادته
            if (IsConnectionHealthy(connection) && _availableConnections.Count < _maxPoolSize)
            {
                _availableConnections.Enqueue(connection);
            }
            else
            {
                // استبدال الاتصال المعطوب
                connection.Dispose();
                if (TotalConnections < _maxPoolSize)
                {
                    var newConnection = CreateConnection();
                    if (newConnection != null)
                    {
                        _availableConnections.Enqueue(newConnection);
                    }
                }
            }
        }
        catch { }
    }

    private async Task InitializePoolAsync(int initialConnections)
    {
        for (int i = 0; i < initialConnections && i < _maxPoolSize; i++)
        {
            var connection = await CreateConnectionAsync();
            if (connection != null)
            {
                _availableConnections.Enqueue(connection);
            }
        }
    }

    private async Task<ITikConnection?> CreateConnectionAsync(string? host = null, string? username = null, string? password = null, int? port = null)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
                var targetHost = host ?? _credentials?.Host;
                var targetUsername = username ?? _credentials?.Username;
                var targetPassword = password ?? _credentials?.Password;
                var targetPort = port ?? _credentials?.Port ?? 8728;

                await connection.OpenAsync(targetHost, targetPort, targetUsername, targetPassword);
                return connection;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"فشل إنشاء اتصال: {ex.Message}");
                return null;
            }
        });
    }

    private ITikConnection? CreateConnection(string? host = null, string? username = null, string? password = null, int? port = null)
    {
        try
        {
            var connection = ConnectionFactory.CreateConnection(TikConnectionType.Api);
            var targetHost = host ?? _credentials?.Host;
            var targetUsername = username ?? _credentials?.Username;
            var targetPassword = password ?? _credentials?.Password;
            var targetPort = port ?? _credentials?.Port ?? 8728;

            connection.Open(targetHost, targetPort, targetUsername, targetPassword);
            return connection;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"فشل إنشاء اتصال: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Checks if the specified connection is healthy asynchronously.
    /// </summary>
    /// <param name="connection">The connection to check.</param>
    /// <returns>True if the connection is healthy; otherwise, false.</returns>
    private static async Task<bool> IsConnectionHealthyAsync(ITikConnection connection)
    {
        if (connection == null || !connection.IsOpened)
            return false;

        return await Task.Run(() =>
        {
            try
            {
                var testCommand = connection.CreateCommand("/system/identity/print");
                testCommand.ExecuteScalar();
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    /// <summary>
    /// Checks if the specified connection is healthy.
    /// </summary>
    /// <param name="connection">The connection to check.</param>
    /// <returns>True if the connection is healthy; otherwise, false.</returns>
    private static bool IsConnectionHealthy(ITikConnection connection)
    {
        if (connection == null || !connection.IsOpened)
            return false;
        try
        {
            var testCommand = connection.CreateCommand("/system/identity/print");
            testCommand.ExecuteScalar();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void HealthCheckConnections(object? state)
    {
        if (_disposed || !IsLoggedIn) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var connectionsToRemove = new List<ITikConnection>();
                var tempConnections = new List<ITikConnection>();

                // نسخ الاتصالات المتاحة إلى قائمة مؤقتة
                while (_availableConnections.TryDequeue(out var connection))
                {
                    tempConnections.Add(connection);
                }

                // فحص صحة الاتصالات
                foreach (var connection in tempConnections)
                {
                    if (await IsConnectionHealthyAsync(connection))
                    {
                        _availableConnections.Enqueue(connection); // إعادة الاتصال السليم
                    }
                    else
                    {
                        connectionsToRemove.Add(connection); //الاتصال المعطوب
                    }
                }

                // التخلص من الاتصالات المعطوبة
                foreach (var connection in connectionsToRemove)
                {
                    connection.Dispose();
                }

                // تعويض الاتصالات المفقودة
                if (_availableConnections.Count < 2 && TotalConnections < _maxPoolSize)
                {
                    var needed = Math.Min(2 - _availableConnections.Count, _maxPoolSize - TotalConnections);
                    for (int i = 0; i < needed; i++)
                    {
                        var newConnection = await CreateConnectionAsync();
                        if (newConnection != null)
                        {
                            _availableConnections.Enqueue(newConnection);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"خطأ في فحص صحة الاتصالات: {ex.Message}");
            }
        });
    }

    private async Task DisposeAllConnectionsAsync()
    {
        await Task.Run(() =>
        {
            // إغلاق الاتصالات النشطة
            foreach (var connection in _activeConnections.Keys)
            {
                connection.Close();
                connection.Dispose();
            }
            _activeConnections.Clear();

            // إغلاق الاتصالات المتاحة
            while (_availableConnections.TryDequeue(out var connection))
            {
                connection.Close();
                connection.Dispose();
            }

            // تحرير الـ semaphore
            for (int i = 0; i < _maxPoolSize; i++)
            {
                if (_poolSemaphore.CurrentCount < _maxPoolSize)
                {
                    _poolSemaphore.Release();
                }
            }
        });
    }

    /// <summary>
    /// Disposes the connection pool and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _healthCheckTimer?.Dispose();
        _poolSemaphore?.Dispose();
        DisposeAllConnectionsAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }
}
