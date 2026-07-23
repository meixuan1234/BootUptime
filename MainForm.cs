using System.ComponentModel;

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
        Size = new Size(280, 52);
        // 默认位置：屏幕右上角偏下，避开右上角可能存在的其他悬浮窗
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(screen.Right - 290, screen.Top + 60);
        TopMost = true;
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
            Text = UptimeService.GetUptimeString(),
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
        _uptimeLabel.Text = UptimeService.GetUptimeString();
        CenterUptimeLabel();
    }

    /// <summary>
    /// 窗体大小改变时：重新居中文字、限制最小尺寸
    /// </summary>
    private void MainForm_Resize(object? sender, EventArgs e)
    {
        // 强制最小尺寸
        if (Width < MinWidth) Width = MinWidth;
        if (Height < MinHeight) Height = MinHeight;
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
            // 将屏幕坐标转换为客户区坐标
            int x = (int)(m.LParam.ToInt64() & 0xFFFF);
            int y = (int)((m.LParam.ToInt64() >> 16) & 0xFFFF);
            Point clientPoint = PointToClient(new Point(x, y));

            bool left = clientPoint.X <= ResizeBorderSize;
            bool right = clientPoint.X >= ClientSize.Width - ResizeBorderSize;
            bool top = clientPoint.Y <= ResizeBorderSize;
            bool bottom = clientPoint.Y >= ClientSize.Height - ResizeBorderSize;

            if (top && left) m.Result = (IntPtr)HTTOPLEFT;
            else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
            else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (left) m.Result = (IntPtr)HTLEFT;
            else if (right) m.Result = (IntPtr)HTRIGHT;
            else if (top) m.Result = (IntPtr)HTTOP;
            else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            else m.Result = (IntPtr)HTCAPTION; // 其余区域视为标题栏，可拖拽
            return;
        }
        base.WndProc(ref m);
    }

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
    /// 窗体关闭时：停止定时器、释放资源
    /// </summary>
    /// <param name="e"></param>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        _contextMenu.Dispose();
        base.OnFormClosed(e);
    }
}