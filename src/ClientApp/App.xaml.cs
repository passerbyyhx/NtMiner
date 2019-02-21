﻿using NTMiner.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace NTMiner {
    public partial class App : Application {
        public App() {
            Logging.LogDir.SetDir(Path.Combine(ClientId.GlobalDirFullName, "Logs"));
            AppHelper.Init(this);
            InitializeComponent();
        }

        private bool createdNew;
        private Mutex appMutex;
        private static string _appPipName = "ntminerclient";
        ExtendedNotifyIcon notifyIcon;

        protected override void OnExit(ExitEventArgs e) {
            notifyIcon?.Dispose();
            NTMinerRoot.Current.Exit();
            base.OnExit(e);
        }

        protected override void OnStartup(StartupEventArgs e) {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            if (!string.IsNullOrEmpty(CommandLineArgs.Upgrade)) {
                Vms.AppStatic.Upgrade(CommandLineArgs.Upgrade, () => {
                    Environment.Exit(0);
                });
            }
            else {
                try {
                    appMutex = new Mutex(true, _appPipName, out createdNew);
                }
                catch (Exception) {
                    createdNew = false;
                }
                if (createdNew) {
                    Vms.AppStatic.IsMinerClient = true;
                    SplashWindow splashWindow = new SplashWindow();
                    splashWindow.Show();
                    NTMinerRoot.Current.Init(OnNTMinerRootInited);
                    VirtualRoot.Accept<CloseNTMinerCommand>(
                        Guid.Parse("47966213-0279-4189-B5DE-5E6A21938EF0"),
                        "处理关闭NTMiner客户端命令",
                        LogEnum.Console,
                        action: message => {
                            Write.DevLine("关闭客NTMiner客户端");
                            UIThread.Execute(() => {
                                if (MainWindow != null) {
                                    MainWindow.Close();
                                }
                                else {
                                    Shutdown();
                                    Environment.Exit(0);
                                }
                            });
                        });
                }
                else {
                    try {
                        AppHelper.ShowMainWindow(this, _appPipName);
                    }
                    catch (Exception) {
                        DialogWindow.ShowDialog(message: "另一个NTMiner正在运行，请手动结束正在运行的NTMiner进程后再次尝试。", title: "alert", icon: "Icon_Error");
                        Process currentProcess = Process.GetCurrentProcess();
                        Process[] processes = Process.GetProcessesByName(currentProcess.ProcessName);
                        foreach (var process in processes) {
                            if (process.Id != currentProcess.Id) {
                                NTMiner.Windows.TaskKill.Kill(process.Id);
                            }
                        }
                    }
                }
            }
            base.OnStartup(e);
        }

        private void OnNTMinerRootInited() {
            OhGodAnETHlargementPill.OhGodAnETHlargementPillUtil.Access();
            NTMinerRoot.KernelDownloader = new KernelDownloader();
            UIThread.Execute(() => {
                Window splashWindow = MainWindow;
                MainWindow window = new MainWindow();
                IMainWindow mainWindow = window;
                this.MainWindow = window;
                this.MainWindow.Show();
                this.MainWindow.Activate();
                notifyIcon = new ExtendedNotifyIcon("pack://application:,,,/NTMiner;component/logo.ico");
                notifyIcon.Init();
                #region 处理显示主界面命令
                VirtualRoot.Accept<ShowMainWindowCommand>(
                    Guid.Parse("01f3c467-f494-42b8-bcb5-848050df59f3"),
                    "处理显示主界面命令",
                    LogEnum.None,
                    action: message => {
                        UIThread.Execute(() => {
                            Dispatcher.Invoke((ThreadStart)mainWindow.ShowThisWindow);
                        });
                    });
                #endregion
                try {
                    NTMinerRoot.Current.Start();
                }
                catch (Exception ex) {
                    Logger.ErrorDebugLine(ex.Message, ex);
                }
                splashWindow?.Close();
                if (NTMinerRoot.Current.MinerProfile.IsAutoStart || CommandLineArgs.IsAutoStart) {
                    Logger.InfoDebugLine("自动开始挖矿倒计时");
                    Views.Ucs.AutoStartCountdown.ShowDialog();
                }
            });
        }
    }
}
