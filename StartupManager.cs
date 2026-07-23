namespace BootUptime;

/// <summary>
/// 开机自启与桌面快捷方式管理器
/// 通过 Startup 文件夹快捷方式实现开机自启（无需管理员权限），
/// 同时支持桌面快捷方式的增删与状态查询。
/// 使用纯 C# 动态调用 WScript.Shell，不引入编译期 COM 依赖。
/// </summary>
public class StartupManager
{
    private const string ShortcutName = "电脑启动时间.lnk";

    private readonly string _startupFolder;
    private readonly string _desktopFolder;
    private readonly string _exePath;

    /// <summary>
    /// 初始化管理器，自动获取 Startup 文件夹、桌面路径及当前 EXE 路径
    /// </summary>
    public StartupManager()
    {
        _startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        _desktopFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        _exePath = Application.ExecutablePath;
    }

    // ==================== 开机自启 ====================

    /// <summary>
    /// 查询开机自启是否已启用（同时校验快捷方式目标 EXE 是否存在）
    /// </summary>
    public bool IsStartupEnabled()
    {
        return IsShortcutValid(Path.Combine(_startupFolder, ShortcutName));
    }

    /// <summary>
    /// 创建开机自启快捷方式，失败时静默处理
    /// </summary>
    public void CreateStartupShortcut()
    {
        CreateShortcutSilent(Path.Combine(_startupFolder, ShortcutName));
    }

    /// <summary>
    /// 移除开机自启快捷方式，失败时静默处理
    /// </summary>
    public void RemoveStartupShortcut()
    {
        RemoveShortcutSilent(Path.Combine(_startupFolder, ShortcutName));
    }

    // ==================== 桌面快捷方式 ====================

    /// <summary>
    /// 查询桌面快捷方式是否存在且有效
    /// </summary>
    public bool IsDesktopShortcutEnabled()
    {
        return IsShortcutValid(Path.Combine(_desktopFolder, ShortcutName));
    }

    /// <summary>
    /// 创建桌面快捷方式，失败时静默处理
    /// </summary>
    public void CreateDesktopShortcut()
    {
        CreateShortcutSilent(Path.Combine(_desktopFolder, ShortcutName));
    }

    /// <summary>
    /// 移除桌面快捷方式，失败时静默处理
    /// </summary>
    public void RemoveDesktopShortcut()
    {
        RemoveShortcutSilent(Path.Combine(_desktopFolder, ShortcutName));
    }

    // ==================== 内部方法 ====================

    /// <summary>
    /// 校验快捷方式文件是否存在且目标 EXE 路径有效
    /// </summary>
    /// <param name="linkPath">快捷方式 .lnk 文件路径</param>
    /// <returns>快捷方式有效返回 true，否则返回 false</returns>
    private bool IsShortcutValid(string linkPath)
    {
        if (!File.Exists(linkPath))
            return false;

        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return false;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);
            string targetPath = shortcut.TargetPath;
            return File.Exists(targetPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 使用 WScript.Shell COM 创建 .lnk 快捷方式，失败时静默处理
    /// </summary>
    /// <param name="linkPath">目标 .lnk 文件完整路径</param>
    private void CreateShortcutSilent(string linkPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
                return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(linkPath);
            shortcut.TargetPath = _exePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(_exePath);
            shortcut.Description = "电脑启动时间";
            shortcut.Save();
        }
        catch
        {
            // 静默失败，不弹窗打扰用户
        }
    }

    /// <summary>
    /// 删除指定路径的快捷方式文件，失败时静默处理
    /// </summary>
    /// <param name="linkPath">待删除的 .lnk 文件路径</param>
    private void RemoveShortcutSilent(string linkPath)
    {
        try
        {
            if (File.Exists(linkPath))
                File.Delete(linkPath);
        }
        catch
        {
            // 静默失败
        }
    }
}