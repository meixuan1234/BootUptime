namespace BootUptime;

/// <summary>
/// 系统运行时间获取服务，数据源与 Windows 任务管理器一致
/// </summary>
public class UptimeService
{
    /// <summary>
    /// 获取格式化的系统运行时间字符串
    /// </summary>
    /// <param name="showSeconds">是否显示秒数</param>
    /// <returns>形如 "3天 12小时 28分钟" 或 "12小时 28分钟 15秒" 的格式化字符串</returns>
    /// <remarks>
    /// 使用 Environment.TickCount64 获取自系统启动以来的毫秒数，
    /// 与任务管理器"性能"选项卡中 CPU 板块的运行时间数据源完全一致。
    /// TickCount64 为 Int64 类型，运行约 2.92 亿年后才会溢出，无需处理。
    /// </remarks>
    public static string GetUptimeString(bool showSeconds = false)
    {
        long uptimeMs = Environment.TickCount64;
        TimeSpan ts = TimeSpan.FromMilliseconds(uptimeMs);

        if (showSeconds)
        {
            if (ts.Days > 0)
                return $"{ts.Days}天 {ts.Hours}小时 {ts.Minutes}分钟 {ts.Seconds}秒";
            if (ts.Hours > 0)
                return $"{ts.Hours}小时 {ts.Minutes}分钟 {ts.Seconds}秒";
            if (ts.Minutes > 0)
                return $"{ts.Minutes}分钟 {ts.Seconds}秒";
            return $"{ts.Seconds}秒";
        }
        else
        {
            if (ts.Days > 0)
                return $"{ts.Days}天 {ts.Hours}小时 {ts.Minutes}分钟";
            if (ts.Hours > 0)
                return $"{ts.Hours}小时 {ts.Minutes}分钟";
            return $"{ts.Minutes}分钟";
        }
    }
}