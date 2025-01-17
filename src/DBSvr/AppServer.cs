﻿using DBSvr.Conf;
using DBSvr.Services;
using DBSvr.Storage;
using DBSvr.Storage.Impl;
using DBSvr.Storage.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Spectre.Console;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SystemModule.Hosts;
using SystemModule.Logger;

namespace DBSvr
{
    public class AppServer : ServiceHost
    {
        private static PeriodicTimer _timer;
        private static DBSvrConf _config;

        public AppServer()
        {

            PrintUsage();
            Console.CancelKeyPress += delegate
            {
                DBShare.ShowLog = true;
                if (_timer != null)
                {
                    _timer.Dispose();
                }
                AnsiConsole.Reset();
            };
            Builder.ConfigureLogging(ConfigureLogging);
            Builder.ConfigureServices(ConfigureServices);
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var configManager = new ConfigManager();
            configManager.LoadConfig();
            _config = configManager.GetConfig;
            Logger.Info("数据库配置文件读取完成...");
            if (!Enum.TryParse<StoragePolicy>(_config.StoreageType, true, out var storagePolicy))
            {
                throw new Exception("数据存储配置文件错误或者不支持该存储类型");
            }
            switch (storagePolicy)
            {
                case StoragePolicy.MySQL:
                    LoadAssembly(services, "MySQL");
                    Logger.Info("使用[MySQL]数据存储.");
                    break;
                case StoragePolicy.MongoDB:
                    LoadAssembly(services, "MongoDB");
                    Logger.Info("使用[MongoDB]数据存储.");
                    break;
                case StoragePolicy.Sqlite:
                    LoadAssembly(services, "Sqlite");
                    Logger.Info("使用[Sqlite]数据存储.");
                    break;
                case StoragePolicy.Local:
                    LoadAssembly(services, "Local");
                    Logger.Info("使用[Local]数据存储.");
                    break;
            }
            services.AddSingleton(_config);
            services.AddSingleton<MirLogger>();
            services.AddSingleton<LoginSessionServer>();
            services.AddSingleton<GateUserService>();
            services.AddSingleton<PlayerDataService>();
            services.AddSingleton<ICacheStorage, CacheStorageService>();
            services.AddHostedService<TimedService>();
            services.AddHostedService<AppService>();
        }

        private void ConfigureLogging(ILoggingBuilder logging)
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddNLog(Configuration);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            Host = await Builder.StartAsync(cancellationToken);
            await ProcessLoopAsync();
            Stop();
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private void LoadAssembly(IServiceCollection services, string storageName)
        {
            var storageFileName = $"DBSvr.Storage.{storageName}.dll";
            var storagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, storageFileName);
            if (!File.Exists(storagePath))
            {
                throw new Exception($"请确认{storageFileName}文件是否存在.");
            }
            var context = new AssemblyLoadContext(storagePath);
            context.Resolving += Context_Resolving;
            var assembly = context.LoadFromAssemblyPath(storagePath);
            if (assembly == null)
            {
                throw new Exception($"获取{storageName}数据存储实例失败，请确认文件是否正确.");
            }
            var storageOption = new StorageOption()
            {
                ConnectionString = _config.ConnctionString
            };
            var playDataStorage = (IPlayDataStorage)Activator.CreateInstance(assembly.GetType($"DBSvr.Storage.{storageName}.PlayDataStorage", true, true), storageOption);
            var playRecordStorage = (IPlayRecordStorage)Activator.CreateInstance(assembly.GetType($"DBSvr.Storage.{storageName}.PlayRecordStorage", true, true), storageOption);
            if (playDataStorage != null)
            {
                services.AddSingleton(playDataStorage);
            }
            if (playRecordStorage != null)
            {
                services.AddSingleton(playRecordStorage);
            }
        }

        /// <summary>
        /// 加载依赖项
        /// </summary>
        /// <returns></returns>
        private Assembly Context_Resolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var expectedPath = Path.Combine(AppContext.BaseDirectory, assemblyName.Name + ".dll");
            if (File.Exists(expectedPath))
            {
                try
                {
                    using var stream = File.OpenRead(expectedPath);
                    return context.LoadFromStream(stream);
                }
                catch (Exception ex)
                {
                    Logger.Error($"加载依赖项{expectedPath}发生异常：{ex.Message},{ex.StackTrace}");
                }
            }
            else
            {
                Logger.Error($"依赖项不存在：{expectedPath}");
            }
            return null;
        }

        private static void Stop()
        {
            AnsiConsole.Status().Start("Disconnecting...", ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
            });
        }

        private async Task ProcessLoopAsync()
        {
            string input = null;
            do
            {
                input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                if (input.StartsWith("/exit") && AnsiConsole.Confirm("Do you really want to exit?"))
                {
                    return;
                }

                var firstTwoCharacters = input[..2];

                if (firstTwoCharacters switch
                {
                    "/s" => ShowServerStatus(),
                    "/c" => ClearConsole(),
                    "/q" => Exit(),
                    _ => null
                } is Task task)
                {
                    await task;
                    continue;
                }

            } while (input is not "/exit");
        }

        private static Task Exit()
        {
            Environment.Exit(Environment.ExitCode);
            return Task.CompletedTask;
        }

        private static Task ClearConsole()
        {
            Console.Clear();
            AnsiConsole.Clear();
            return Task.CompletedTask;
        }

        private async Task ShowServerStatus()
        {
            DBShare.ShowLog = false;
            var userService = Host.Services.GetService<GateUserService>();
            if (userService == null)
            {
                return;
            }
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            var serverList = userService.GetGates.ToArray();
            var table = new Table().Expand().BorderColor(Color.Grey);
            table.AddColumn("[yellow]ServerName[/]");
            table.AddColumn("[yellow]EndPoint[/]");
            table.AddColumn("[yellow]Status[/]");
            table.AddColumn("[yellow]Sessions[/]");
            table.AddColumn("[yellow]Send[/]");
            table.AddColumn("[yellow]Revice[/]");
            table.AddColumn("[yellow]Queue[/]");

            await AnsiConsole.Live(table)
                 .AutoClear(true)
                 .Overflow(VerticalOverflow.Crop)
                 .Cropping(VerticalOverflowCropping.Bottom)
                 .StartAsync(async ctx =>
                 {
                     foreach (var _ in Enumerable.Range(0, 10))
                     {
                         table.AddRow(new[] { new Markup("-"), new Markup("-"), new Markup("-"), new Markup("-"), new Markup("-"), new Markup("-") });
                     }

                     while (await _timer.WaitForNextTickAsync())
                     {
                         for (int i = 0; i < serverList.Length; i++)
                         {
                             var (serverIp, status, sessionCount, reviceTotal, sendTotal, queueCount) = serverList[i].GetStatus();

                             table.UpdateCell(i, 0, "[bold][blue]SelGate[/][/]");
                             table.UpdateCell(i, 1, ($"[bold]{serverIp}[/]"));
                             table.UpdateCell(i, 2, ($"[bold]{status}[/]"));
                             table.UpdateCell(i, 3, ($"[bold]{sessionCount}[/]"));
                             table.UpdateCell(i, 4, ($"[bold]{sendTotal}[/]"));
                             table.UpdateCell(i, 5, ($"[bold]{reviceTotal}[/]"));
                             table.UpdateCell(i, 6, ($"[bold]{queueCount}[/]"));
                         }
                         ctx.Refresh();
                     }
                 });
        }

        private static void PrintUsage()
        {
            AnsiConsole.WriteLine();
            using var logoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DBSvr.logo.png");
            var logo = new CanvasImage(logoStream!)
            {
                MaxWidth = 25
            };

            var table = new Table()
            {
                Border = TableBorder.None,
                Expand = true,
            }.HideHeaders();
            table.AddColumn(new TableColumn("One"));

            var header = new FigletText("OpenMir2")
            {
                Color = Color.Fuchsia
            };
            var header2 = new FigletText("DB Server")
            {
                Color = Color.Aqua
            };

            var sb = new StringBuilder();
            sb.Append("[bold fuchsia]/s[/] [aqua]查看[/] 网关状况\n");
            sb.Append("[bold fuchsia]/r[/] [aqua]重读[/] 配置文件\n");
            sb.Append("[bold fuchsia]/c[/] [aqua]清空[/] 清除屏幕\n");
            sb.Append("[bold fuchsia]/q[/] [aqua]退出[/] 退出程序\n");
            var markup = new Markup(sb.ToString());

            table.AddColumn(new TableColumn("Two"));

            var rightTable = new Table()
                .HideHeaders()
                .Border(TableBorder.None)
                .AddColumn(new TableColumn("Content"));

            rightTable.AddRow(header)
                .AddRow(header2)
                .AddEmptyRow()
                .AddEmptyRow()
                .AddRow(markup);
            table.AddRow(logo, rightTable);

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        public override void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
