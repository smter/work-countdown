using System;
using System.Threading;
using System.Windows.Forms;

namespace WorkCountdown
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "WorkCountdownWidget.SingleInstance", out createdNew))
            {
                if (!createdNew) return;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // --portable：配置写到 exe 旁边的 data 目录，U 盘 / 免安装用
                if (args != null && Array.IndexOf(args, "--portable") >= 0)
                {
                    try
                    {
                        string marker = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.txt");
                        if (!System.IO.File.Exists(marker))
                            System.IO.File.WriteAllText(marker, "便携模式：配置保存在同目录的 data 文件夹里\r\n", new System.Text.UTF8Encoding(false));
                    }
                    catch { }
                }

                var store = new SettingsStore();
                bool firstRun = !store.Exists;
                var settings = store.Load();

                var form = new WidgetForm(store, settings);
                form.EnableTray();

                if (firstRun)
                {
                    form.Shown += delegate
                    {
                        form.BeginInvoke((MethodInvoker)delegate { form.RunFirstRun(); });
                    };
                }
                else if (args != null && args.Length > 0 && args[0] == "--settings")
                {
                    form.Shown += delegate { form.BeginInvoke((MethodInvoker)delegate { form.OpenSettingsPublic(); }); };
                }

                Application.Run(form);
            }
        }
    }
}
