

using FluentDevelopment.Tik4net.Models;
using System.Collections.Concurrent;
using tik4net;

namespace FluentDevelopment.Tik4net.Managers;

public class TikConnectionPool : IDisposable
{
    private readonly ConcurrentQueue<ITikConnection> _availableConnections;
    private readonly ConcurrentDictionary<ITikConnection, bool> _activeConnections;
    private readonly SemaphoreSlim _poolSemaphore;
    private readonly int _maxPoolSize;
    private TikCredentials? _credentials;
    private bool _disposed = false;
    private Timer _healthCheckTimer;

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

    public bool IsLoggedIn => _credentials?.IsValid == true;
    public int AvailableConnections => _availableConnections.Count;
    public int ActiveConnections => _activeConnections.Count;
    public int TotalConnections => AvailableConnections + ActiveConnections;
    public bool Internet => ConnectivityService.IsFullyConnected;

    public async Task<LoginResult> LoginAsync(string host, string username, string password, int port = 8728)
    {
        try
        {
            if (!Internet)
                return LoginResult.Failure("يجب الاتصال بشبكة الانترنت");
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
                return LoginResult.Failure("فشل في الاتصال بالخادم. تحقق من البيانات وإمكانية الوصول");
            }
        }
        catch (TikConnectionException ex)
        {
            return LoginResult.Failure($"خطأ في الاتصال: {ex.Message}");
        }
        catch (TimeoutException)
        {
            return LoginResult.Failure("انتهت مهلة الاتصال. تحقق من إمكانية الوصول للخادم");
        }
        catch (UnauthorizedAccessException)
        {
            return LoginResult.Failure("بيانات الدخول غير صحيحة. تحقق من اسم المستخدم وكلمة المرور");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"خطأ غير متوقع في تسجيل الدخول: {ex}");
            return LoginResult.Failure($"حدث خطأ غير متوقع: {ex.Message}");
        }
    }
    public async Task LogoutAsync()
    {
        await DisposeAllConnectionsAsync();
        _credentials = null;
    }

    public async Task<ITikConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLoggedIn)
            throw new InvalidOperationException("يجب تسجيل الدخول أولاً");

        if (_disposed)
            throw new ObjectDisposedException(nameof(TikConnectionPool));

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
        finally
        {
            _poolSemaphore.Release();
        }
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
        return await Task.Run( async () =>
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

    private async Task<bool> IsConnectionHealthyAsync(ITikConnection connection)
    {
        if (connection == null || !connection.IsOpened)
            return false;

        try
        {
            var testCommand = connection.CreateCommand("/system/identity/print");
            await Task.Run(() => testCommand.ExecuteScalar());
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
                        connectionsToRemove.Add(connection); //标记 الاتصال المعطوب
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

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _healthCheckTimer?.Dispose();
        _poolSemaphore?.Dispose();
        DisposeAllConnectionsAsync().GetAwaiter().GetResult();
    }
}