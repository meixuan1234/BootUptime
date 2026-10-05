using System.ComponentModel;
using System.Text.Json;

namespace BootUptime;

/// <summary>
/// 主窗口：半透明无边框窗体，实时显示系统运行时间，
/// 支持全域拖拽、边缘缩放、齿轮菜单、开机自启/桌面快捷方式管理
/// </summary>
public class MainForm : Form
{
    // ==================== 窗口常量 ====================
    private const double WindowOpacity = 0.85;
    private const int MinWidth = 200;
    private const int MinHeight = 40;
    private const int ResizeBorderSize = 6;
    private static readonly Color BgColor = Color.FromArgb(26, 26, 26);       // #1A1A1A
    private static readonly Color TextColor = Color.FromArgb(200, 200, 200);  // #C8C8C8

    /// <summary>DPI 缩放系数（96 DPI = 1.0）</summary>
    private float DpiScale => DeviceDpi / 96f;

    /// <summary>
    /// 把逻辑像素（96 DPI 基准）换算为当前显示器下的物理像素。
    /// </summary>
    /// <param name="value">逻辑像素值</param>
    /// <returns>按当前 DPI 缩放并四舍五入后的像素值</returns>
    /// <remarks>
    /// 缩放热区、最小尺寸等常量都是按 96 DPI 写的逻辑像素，必须过一遍本方法换算，
    /// 否则在 125% / 150% 缩放下热区会偏小、拖拽边缘的手感变差。
    /// </remarks>
    private int S(int value) => (int)Math.Round(value * DpiScale);

    // ==================== Win32 消息常量 ====================
    private const int WM_NCHITTEST = 0x84;
    private const int HTCLIENT = 1;
    private const int HTCAPTION = 2;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    // ==================== 控件与服务 ====================
    private readonly Label _gearLabel;
    private readonly Label _uptimeLabel;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _menuStartup;
    private readonly ToolStripMenuItem _menuTopMost;
    private readonly ToolStripMenuItem _menuDesktopShortcut;
    private readonly ToolStripMenuItem _menuClose;
    private readonly StartupManager _startupManager;

    // ==================== 拖拽状态 ====================
    /// <summary>是否正在拖动窗口</summary>
    private bool _isDragging;

    /// <summary>拖动开始时鼠标的屏幕坐标</summary>
    private Point _dragStartCursor;

    /// <summary>拖动开始时窗口左上角的屏幕坐标</summary>
    private Point _dragStartWindow;

    // ==================== 位置记忆 ====================
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BootUptime");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    /// <summary>
    /// 初始化主窗口：设置外观、创建控件、绑定事件
    /// </summary>
    public MainForm()
    {
        _startupManager = new StartupManager();

        // ---- 窗口基本属性 ----
        Text = "电脑启动时间";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(300, 52);

        // 恢复上次的位置与置顶偏好；坐标越界（换显示器 / 改分辨率）时停靠到屏幕右上角
        var saved = LoadSettings();
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Location = saved != null && saved.HasValidPosition(area)
            ? new Point(saved.X, saved.Y)
            : new Point(area.Right - 310, area.Top + 60);
        TopMost = saved?.TopMost ?? true;

        Opacity = WindowOpacity;
        BackColor = BgColor;
        ShowInTaskbar = true;
        MinimumSize = new Size(MinWidth, MinHeight);

        // ---- 齿轮图标 (左上角) ----
        _gearLabel = new Label
        {
            Text = "\u2699",                    // ⚙ Unicode 齿轮
            Font = new Font("Segoe UI", 10f, FontStyle.Regular),
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(4, 4),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _gearLabel.Click += GearLabel_Click;

        // ---- 运行时间文本 ----
        _uptimeLabel = new Label
        {
            Text = UptimeService.GetUptimeString(true),
            Font = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Point),
            ForeColor = TextColor,
            BackColor = Color.Transparent,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter
        };
        _uptimeLabel.Location = new Point(
            (ClientSize.Width - _uptimeLabel.PreferredWidth) / 2,
            (ClientSize.Height - _uptimeLabel.PreferredHeight) / 2
        );

        // ---- 右键菜单 ----
        _contextMenu = new ContextMenuStrip();
        _menuStartup = new ToolStripMenuItem("开机自启");
        _menuTopMost = new ToolStripMenuItem("窗口置顶");
        _menuDesktopShortcut = new ToolStripMenuItem("桌面快捷方式");
        _menuClose = new ToolStripMenuItem("关闭程序");

        _menuStartup.Click += MenuStartup_Click;
        _menuTopMost.Click += MenuTopMost_Click;
        _menuDesktopShortcut.Click += MenuDesktopShortcut_Click;
        _menuClose.Click += MenuClose_Click;

        _contextMenu.Items.Add(_menuStartup);
        _contextMenu.Items.Add(_menuTopMost);
        _contextMenu.Items.Add(_menuDesktopShortcut);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_menuClose);

        _contextMenu.Opening += ContextMenu_Opening;

        // ---- 定时刷新 ----
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _refreshTimer.Tick += RefreshTimer_Tick;

        // ---- 添加控件到窗体 ----
        Controls.Add(_gearLabel);
        Controls.Add(_uptimeLabel);

        // ---- 事件绑定 ----
        Load += MainForm_Load;
        Resize += MainForm_Resize;

        // 左键拖拽必须逐个挂到**所有**子控件上：
        // Label 拥有自己的窗口句柄，鼠标落在文字上时消息被 Label 吃掉，
        // 只订阅窗体自身会导致"在运行时间文字上按住拖不动窗口"。
        AttachDrag(this);
    }

    // ==================== 事件处理 ====================

    /// <summary>
    /// 窗体加载后：启动定时器、居中文字
    /// </summary>
    private void MainForm_Load(object? sender, EventArgs e)
    {
        _refreshTimer.Start();
        CenterUptimeLabel();
    }

    /// <summary>
    /// 定时器每秒触发：刷新运行时间文字、更新菜单位置
    /// </summary>
    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        _uptimeLabel.Text = UptimeService.GetUptimeString(true);
        CenterUptimeLabel();
    }

    /// <summary>
    /// 窗体大小改变时：重新居中文字、限制最小尺寸
    /// </summary>
    private void MainForm_Resize(object? sender, EventArgs e)
    {
        // 强制最小尺寸（常量是逻辑像素，需按当前 DPI 换算才能和 MinimumSize 属性一致）
        int minWidth = S(MinWidth);
        int minHeight = S(MinHeight);
        if (Width < minWidth) Width = minWidth;
        if (Height < minHeight) Height = minHeight;
        CenterUptimeLabel();
    }

    /// <summary>
    /// 点击齿轮图标：在齿轮右下方弹出菜单
    /// </summary>
    private void GearLabel_Click(object? sender, EventArgs e)
    {
        _contextMenu.Show(_gearLabel, new Point(0, _gearLabel.Height));
    }

    /// <summary>
    /// 菜单即将打开时：同步各项勾选状态
    /// </summary>
    private void ContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        _menuStartup.Checked = _startupManager.IsStartupEnabled();
        _menuTopMost.Checked = TopMost;
        _menuDesktopShortcut.Checked = _startupManager.IsDesktopShortcutEnabled();
    }

    /// <summary>
    /// 菜单项"开机自启"：切换 Startup 文件夹快捷方式
    /// </summary>
    private void MenuStartup_Click(object? sender, EventArgs e)
    {
        if (_startupManager.IsStartupEnabled())
            _startupManager.RemoveStartupShortcut();
        else
            _startupManager.CreateStartupShortcut();
    }

    /// <summary>
    /// 菜单项"窗口置顶"：切换 TopMost 属性
    /// </summary>
    private void MenuTopMost_Click(object? sender, EventArgs e)
    {
        TopMost = !TopMost;
    }

    /// <summary>
    /// 菜单项"桌面快捷方式"：切换桌面快捷方式
    /// </summary>
    private void MenuDesktopShortcut_Click(object? sender, EventArgs e)
    {
        if (_startupManager.IsDesktopShortcutEnabled())
            _startupManager.RemoveDesktopShortcut();
        else
            _startupManager.CreateDesktopShortcut();
    }

    /// <summary>
    /// 菜单项"关闭程序"：彻底退出应用
    /// </summary>
    private void MenuClose_Click(object? sender, EventArgs e)
    {
        Application.Exit();
    }

    // ==================== 窗口拖拽与缩放 ====================

    /// <summary>
    /// 重写 WndProc，处理窗口拖拽（全域 HTCAPTION）和边缘缩放（四边四角）
    /// </summary>
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            // 屏幕坐标存在 LParam 低 32 位（x 在低 16 位、y 在高 16 位），而且是**带符号**的。
            // 必须用 short 截断取值：若写成 (int)(v & 0xFFFF)，副屏上的负坐标会被当成
            // 65436 之类的大正数，PointToClient 换算出的客户区坐标全错，
            // 表现为那台显示器上四条边的缩放热区完全失灵。
            long lParam = m.LParam.ToInt64();
            int screenX = unchecked((short)(lParam & 0xFFFF));
            int screenY = unchecked((short)((lParam >> 16) & 0xFFFF));
            Point clientPoint = PointToClient(new Point(screenX, screenY));

            int edge = S(ResizeBorderSize);   // 热区按 DPI 换算，高缩放下手感才一致
            bool left = clientPoint.X <= edge;
            bool right = clientPoint.X >= ClientSize.Width - edge;
            bool top = clientPoint.Y <= edge;
            bool bottom = clientPoint.Y >= ClientSize.Height - edge;

            if (top && left) m.Result = (IntPtr)HTTOPLEFT;
            else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
            else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (left) m.Result = (IntPtr)HTLEFT;
            else if (right) m.Result = (IntPtr)HTRIGHT;
            else if (top) m.Result = (IntPtr)HTTOP;
            else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            else m.Result = (IntPtr)HTCLIENT; // 内部区域由 MouseDown/MouseMove 处理左键拖拽
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// 递归为窗体及其全部子控件挂载左键拖拽能力。
    /// </summary>
    /// <param name="control">目标控件（会一并处理其所有子控件）</param>
    private void AttachDrag(Control control)
    {
        control.MouseDown += DragMouseDown;
        control.MouseMove += DragMouseMove;
        control.MouseUp += DragMouseUp;

        foreach (Control child in control.Controls)
            AttachDrag(child);
    }

    /// <summary>
    /// 左键按下：进入拖拽状态，记录鼠标与窗口的起始屏幕坐标。
    /// </summary>
    /// <param name="sender">事件源（窗体或任一子控件）</param>
    /// <param name="e">鼠标事件参数</param>
    /// <remarks>
    /// 统一以屏幕坐标（<see cref="Cursor"/>.Position）为基准，而不是 e.Location：
    /// 后者是"相对当前控件"的坐标，事件源换成子控件后数值含义就变了，
    /// 位移量会算错。
    /// </remarks>
    private void DragMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        _isDragging = true;
        _dragStartCursor = Cursor.Position;
        _dragStartWindow = Location;
    }

    /// <summary>
    /// 鼠标移动：拖拽状态下按鼠标位移整体移动窗口。
    /// </summary>
    /// <param name="sender">事件源（窗体或任一子控件）</param>
    /// <param name="e">鼠标事件参数</param>
    private void DragMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        Point now = Cursor.Position;
        Location = new Point(
            _dragStartWindow.X + (now.X - _dragStartCursor.X),
            _dragStartWindow.Y + (now.Y - _dragStartCursor.Y));
    }

    /// <summary>
    /// 松开鼠标：结束拖拽状态。
    /// </summary>
    /// <param name="sender">事件源（窗体或任一子控件）</param>
    /// <param name="e">鼠标事件参数</param>
    private void DragMouseUp(object? sender, MouseEventArgs e) => _isDragging = false;

    // ==================== 辅助方法 ====================

    /// <summary>
    /// 将运行时间标签居中放置在窗体客户区
    /// </summary>
    private void CenterUptimeLabel()
    {
        _uptimeLabel.Location = new Point(
            (ClientSize.Width - _uptimeLabel.PreferredWidth) / 2,
            (ClientSize.Height - _uptimeLabel.PreferredHeight) / 2
        );
    }

    /// <summary>
    /// 窗体关闭时：保存窗口位置、停止定时器、释放资源
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SaveSettings(Location, TopMost);
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _contextMenu.Dispose();
        base.OnFormClosed(e);
    }

    /// <summary>
    /// 从 JSON 文件加载上次保存的窗口位置与外观偏好。
    /// </summary>
    /// <returns>读取到的配置；文件不存在或内容损坏时返回 null</returns>
    /// <remarks>
    /// 本方法只负责"读"，坐标是否可用交给 <see cref="SettingsData.HasValidPosition"/> 判断 ——
    /// 拆开之后，即便坐标已越界，"窗口置顶"这类与坐标无关的偏好仍能保留下来。
    /// </remarks>
    private static SettingsData? LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return null;
            return JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SettingsFile));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 把窗口位置与外观偏好写入 JSON 文件。
    /// </summary>
    /// <param name="location">当前窗口左上角坐标</param>
    /// <param name="topMost">当前是否置顶</param>
    private static void SaveSettings(Point location, bool topMost)
    {
        try
        {
            if (!Directory.Exists(SettingsDir))
                Directory.CreateDirectory(SettingsDir);
            var data = new SettingsData { X = location.X, Y = location.Y, TopMost = topMost };
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(data));
        }
        catch
        {
            // 静默失败：位置记忆属于锦上添花，不能因此打断用户
        }
    }

    /// <summary>
    /// 配置持久化数据结构
    /// </summary>
    private class SettingsData
    {
        /// <summary>窗口左上角 X 坐标</summary>
        public int X { get; set; } = int.MinValue;

        /// <summary>窗口左上角 Y 坐标</summary>
        public int Y { get; set; } = int.MinValue;

        /// <summary>是否保持窗口置顶，默认开启</summary>
        public bool TopMost { get; set; } = true;

        /// <summary>
        /// 判断保存的坐标是否落在指定屏幕工作区内。
        /// </summary>
        /// <param name="screen">屏幕工作区矩形</param>
        /// <returns>坐标可用返回 true</returns>
        /// <remarks>
        /// 换了显示器或改了分辨率后，旧坐标可能已经落到屏幕之外；
        /// 不校验的话会出现"程序启动了但窗口找不着"的情况。
        /// </remarks>
        public bool HasValidPosition(Rectangle screen)
        {
            return X >= screen.Left && X < screen.Right - 100
                && Y >= screen.Top && Y < screen.Bottom - 30;
        }
    }
}