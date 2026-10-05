namespace BootUptime;

/// <summary>
/// 应用程序入口
/// </summary>
internal static class Program
{
    /// <summary>单实例互斥体名称（Local\ 前缀 = 仅当前登录会话内有效）</summary>
    private const string SingleInstanceMutexName = @"Local\BootUptime.SingleInstance";

    /// <summary>命令行参数：只做初始化冒烟自检，验证完立即退出（不显示窗口）</summary>
    private const string SelfTestArg = "--selftest";

    /// <summary>
    /// 崩溃日志路径：%LOCALAPPDATA%\BootUptime\error.log
    /// </summary>
    /// <remarks>
    /// 存在的意义：本程序是 WinExe，没有控制台窗口。
    /// 任何在 Application.Run 之前抛出的异常都会让进程直接退出、屏幕上什么都不显示，
    /// 也就是用户看到的"双击打不开"。有日志才能定位问题。
    /// </remarks>
    private static readonly string CrashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BootUptime", "error.log");

    /// <summary>
    /// 程序主入口：初始化 WinForms 运行环境后启动主窗口。
    /// </summary>
    /// <param name="args">命令行参数。传入 <c>--selftest</c> 时只做初始化自检并退出。</param>
    /// <remarks>
    /// 需要 [STAThread] 特性，否则剪贴板、文件对话框等 COM 组件会异常。
    /// ApplicationConfiguration.Initialize() 由 csproj 中的
    /// ApplicationHighDpiMode / ApplicationDefaultFont 等属性源生成。
    /// </remarks>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // 冒烟自检分支：供构建后自动化验证初始化链路
        if (args.Any(a => string.Equals(a, SelfTestArg, StringComparison.OrdinalIgnoreCase)))
        {
            RunSelfTest();
            return;
        }

        // 单实例保护：反复双击图标时不再开出一堆挂件，而是提示已有实例
        using var singleInstance = new Mutex(true, SingleInstanceMutexName, out bool isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "程序已经在运行了，请在桌面上查看。",
                "电脑启动时间",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // 兜底：任何未捕获异常都落盘记录，杜绝"静默闪退、无从排查"的情况
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => WriteCrashLog(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => WriteCrashLog(e.ExceptionObject as Exception);

        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            // 构造窗口阶段抛出的异常不会走 ThreadException，必须在这里补一刀
            WriteCrashLog(ex);
            throw;
        }
    }

    /// <summary>
    /// 冒烟自检：完整构造主窗口并走一遍真实显示路径，验证初始化链路无异常。
    /// </summary>
    /// <remarks>
    /// 只把结果写到标准输出，窗口仅出现数十毫秒，可由构建脚本安全调用。
    /// 退出码 0 表示通过，1 表示失败（异常会一并打印）。
    /// </remarks>
    private static void RunSelfTest()
    {
        try
        {
            using var form = new MainForm();

            form.Show();              // 触发 OnHandleCreated → OnLoad → 首次绘制
            Application.DoEvents();
            form.Refresh();
            Application.DoEvents();

            string report =
                $"  DeviceDpi   = {form.DeviceDpi}{Environment.NewLine}" +
                $"  ClientSize  = {form.ClientSize.Width} x {form.ClientSize.Height}{Environment.NewLine}" +
                $"  Location    = {form.Location.X}, {form.Location.Y}{Environment.NewLine}" +
                $"  Opacity     = {form.Opacity:0.00}{Environment.NewLine}" +
                $"  TopMost     = {form.TopMost}{Environment.NewLine}" +
                "  Controls    = " + string.Join(" | ", form.Controls.Cast<Control>()
                    .Select(c => $"{c.Text} {c.Width}x{c.Height} @{c.Left},{c.Top}"));

            form.Close();   // 触发 OnFormClosed，顺带验证配置持久化链路

            Console.WriteLine("SELFTEST OK");
            Console.WriteLine(report);
            Environment.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("SELFTEST FAILED");
            Console.WriteLine(ex);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// 将异常信息追加写入崩溃日志文件。
    /// </summary>
    /// <param name="ex">异常对象；为 null 时忽略</param>
    private static void WriteCrashLog(Exception? ex)
    {
        if (ex == null) return;

        try
        {
            string? dir = Path.GetDirectoryName(CrashLogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string text =
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(CrashLogPath, text);
        }
        catch
        {
            // 日志本身写不进去就只能放弃，绝不能让写日志再引发一次崩溃
        }
    }
}
