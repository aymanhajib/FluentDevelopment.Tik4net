
using FluentDevelopment.Tik4net.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using tik4net;

namespace FluentDevelopment.Tik4net.Demo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Start view TikService ===");
            Console.WriteLine();

            // 1. إنشاء المسجل
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            var logger = loggerFactory.CreateLogger<TikService>();
            // 2. إنشاء الخدمة
            using var tikService = new TikService(
                maxPoolSize: 10,
                logger: logger);

            try
            {
                await RunCompleteDemo(tikService);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"خطأ غير متوقع: {ex.Message}");
                Console.WriteLine($"تفاصيل: {ex}");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("=== End View ===");
            Console.ReadKey();
        }

        static async Task RunCompleteDemo(TikService tikService)
        {
            // ===== 1. تسجيل الدخول =====
            Console.WriteLine("\n1. Login to MikroTik...");
            var loginResult = await tikService.LoginAsync(
                host: "192.168.88.1",
                username: "ayman",
                password: "123Qwertyuiop!@#",
                port: 8728);

            if (!loginResult.IsSuccess)
            {
                Console.WriteLine($"Error to login: {loginResult.ErrorMessage}");
                return;
            }
            Console.WriteLine("Logined ✓");

            // ===== 2. استخدام QuickAsync =====
            Console.WriteLine("\n2. Use QuickAsync to process...");

            // مثال 1: جلب معلومات النظام
            var systemInfo = await tikService.QuickAsync(async connection =>
            {
                var cmd = connection.CreateCommand("/system/resource/print");
                var result = await Task.Run(() => cmd.ExecuteList());
                return result;
            });

            if (systemInfo.IsSuccess && systemInfo.Data != null)
            {
                Console.WriteLine($" System Information: {systemInfo.Data.Count()} regester");
                foreach (var item in systemInfo.Data)
                {
                    Console.WriteLine($"  - CPU: {item.GetResponseField("cpu-load")}% | Uptime: {item.GetResponseField("uptime")}");
                }
            }

            // مثال 2: جلب قائمة المستخدمين
            var usersCount = await tikService.QuickAsync(async connection =>
            {
                var cmd = connection.CreateCommand("/user/print");
                var users = await Task.Run(() => cmd.ExecuteList());
                return users.Count();
            });

            if (usersCount.IsSuccess)
            {
                Console.WriteLine($"Number of users: {usersCount.Data}");
            }
            /*
            // ===== 3. استخدام RetryAsync =====
            Console.WriteLine("\n3. استخدام RetryAsync مع إعادة المحاولة...");

            var interfacesResult = await tikService.RetryAsync(async connection =>
            {
                var cmd = connection.CreateCommand("/interface/print");
                var interfaces = await Task.Run(() => cmd.ExecuteList());
                return interfaces;
            }, maxRetries: 3, delayBetweenRetries: TimeSpan.FromSeconds(1));

            if (interfacesResult.IsSuccess)
            {
                Console.WriteLine($"عدد الواجهات: {interfacesResult.Data?.Count ?? 0}");
            }

            // ===== 4. استخدام TimeoutAsync =====
            Console.WriteLine("\n4. استخدام TimeoutAsync مع مهلة زمنية...");

            var timeoutResult = await tikService.TimeoutAsync(async connection =>
            {
                await Task.Delay(2000); // محاكاة عملية طويلة
                var cmd = connection.CreateCommand("/system/clock/print");
                return await Task.Run(() => cmd.ExecuteList());
            }, timeout: TimeSpan.FromSeconds(3));

            if (timeoutResult.IsSuccess)
            {
                Console.WriteLine("تم جلب وقت النظام بنجاح ✓");
            }
            else
            {
                Console.WriteLine($"فشل مع المهلة: {timeoutResult.ErrorMessage}");
            }
            */
            // ===== 5. استخدام GetLongConnectionAsync =====
            Console.WriteLine("\n5. Use GetLongConnectionAsync to long conection...");

            var longConnectionResult = await tikService.GetLongConnectionAsync(
                operation: async connection =>
                {
                    Console.WriteLine("  - Is long connected");
                    Console.WriteLine("  - Loading setting tasks...");

                    // يمكن هنا إعداد اشتراكات للأحداث
                    await Task.Delay(500);
                },
                connectionName: "InterfaceMonitor",
                onStatusChanged: status =>
                {
                    Console.WriteLine($"  - Status conection: {status.Status}");
                });

            if (longConnectionResult.IsSuccess)
            {
                var longConnection = longConnectionResult.Data;
                Console.WriteLine($"created long connection with counter: {longConnection.Id}");

                // تنفيذ عمليات متعددة على نفس الاتصال
                for (int i = 1; i <= 3; i++)
                {
                    var result = await longConnection.ExecuteAsync(async connection =>
                    {
                        var cmd = connection.CreateCommand("/ip/address/print");
                        var addresses = await Task.Run(() => cmd.ExecuteList());
                        return addresses.Count();
                    }, $"GetAddresses_{i}");

                    if (result.IsSuccess)
                    {
                        Console.WriteLine($"  Process {i}: number of address IP = {result.Data}");
                    }
                }

                // استخدام كمحسن إذا كان متاحاً
                if (longConnection is ILongConnection enhancedConn)
                {
                    // الحصول على إحصاءات
                    var stats2 = enhancedConn.GetStats();
                    Console.WriteLine($"  Totel Connection : {stats2.SuccessRate:F1}% Succeful");

                    // إغلاق متحكم به
                    await Task.Delay(1000);
                    Console.WriteLine("  إغلاق الاتصال الطويل...");
                    await enhancedConn.CloseAsync("انتهاء المهمة");
                }
                else
                {
                    longConnection.Dispose();
                }
            }

            // ===== 6. استخدام BackgroundAsync =====
            Console.WriteLine("\n6. use BackgroundAsync to process in background...");

            var backgroundTasks = new List<Task<IOperationResult>>();

            for (int i = 1; i <= 3; i++)
            {
                var taskNumber = i;
                var task = tikService.BackgroundAsync(
                    async connection =>
                    {
                        Console.WriteLine($"  [Background {taskNumber}] start process...");
                        await Task.Delay(taskNumber * 1000);

                        var cmd = connection.CreateCommand("/log/print");
                        var logs = await Task.Run(() => cmd.ExecuteList());
                        Console.WriteLine($"  [Background {taskNumber}] number regester: {logs.Count()}");

                        Console.WriteLine($"  [Background {taskNumber}] end process");
                    },
                    onCompleted: result =>
                    {
                        Console.WriteLine($"  [Callback {taskNumber}] completed: {result.IsSuccess}");
                    });

                backgroundTasks.Add(task);
            }

            await Task.WhenAll(backgroundTasks);
            Console.WriteLine("Completed all tasks ✓");

            // ===== 11. تسجيل الخروج =====
            Console.WriteLine("\n11. Logout...");

            // ===== 12. عرض الإحصاءات النهائية =====
            Console.WriteLine("\n12. Totel end...");

            var finalStats = tikService.GetStatistics();
            Console.WriteLine($"  totel process: {finalStats.TotalLongConnections}");
            Console.WriteLine($"  معدل النجاح النهائي: {finalStats.ActivePoolConnections:F2}%");
            Console.WriteLine($"  وقت التشغيل الإجمالي: {finalStats.AvailablePoolConnections}");
        }
    }

    // فئة مساعدة للعرض
    public class DemoHelper
    {
        public static async Task SimulateLongOperation(ITikConnection connection, string operationName)
        {
            Console.WriteLine($"  [{operationName}] Begin long process...");
            await Task.Delay(2000);

            var cmd = connection.CreateCommand("/system/identity/print");
            var identity = await Task.Run(() => cmd.ExecuteScalar());

            Console.WriteLine($"  [{operationName}] system : {identity}");
            Console.WriteLine($"  [{operationName}] ent task");
        }
    }
}