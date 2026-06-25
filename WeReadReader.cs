using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal sealed class ReaderSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Left { get; set; }
    public int Top { get; set; }
    public double ZoomFactor { get; set; }
    public bool AutoHideEnabled { get; set; }
    public string CurrentUrl { get; set; }
}

internal static class Program
{
    private const string ToggleEventName = @"Local\WeReadSideReader.Toggle";
    private const string MutexName = @"Local\WeReadSideReader.SingleInstance";

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [STAThread]
    private static void Main()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID("WeRead.SideReader.Portable");
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            using (var toggleEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ToggleEventName))
            {
                if (!createdNew)
                {
                    toggleEvent.Set();
                    return;
                }

                var root = AppDomain.CurrentDomain.BaseDirectory;
                var lib = Path.Combine(root, "lib");
                Environment.SetEnvironmentVariable("PATH", lib + ";" + Environment.GetEnvironmentVariable("PATH"));

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var form = new ReaderForm(root))
                {
                    var watcher = new Thread(delegate()
                    {
                        while (!form.IsDisposed)
                        {
                            toggleEvent.WaitOne();
                            if (form.IsDisposed) break;
                            try
                            {
                                form.BeginInvoke((Action)form.ToggleVisible);
                            }
                            catch (InvalidOperationException)
                            {
                                break;
                            }
                        }
                    });
                    watcher.IsBackground = true;
                    watcher.Start();
                    Application.Run(form);
                }
            }
        }
        catch (Exception error)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
            try { File.WriteAllText(path, error.ToString()); }
            catch { }
            MessageBox.Show(error.Message, "WeRead startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal sealed class ReaderForm : Form
{
    private const int HotkeyId = 1;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const int VkY = 0x59;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsSysMenu = 0x00080000;

    private readonly string root;
    private readonly string settingsPath;
    private readonly string profilePath;
    private readonly WebView2 webView;
    private readonly Panel titleBar;
    private readonly ContextMenuStrip readerMenu;
    private readonly ToolStripMenuItem autoMenuItem;
    private readonly ToolTip toolTip;
    private readonly System.Windows.Forms.Timer autoHideTimer;
    private readonly System.Windows.Forms.Timer saveTimer;
    private readonly NotifyIcon tray;
    private readonly JavaScriptSerializer json = new JavaScriptSerializer();

    private ReaderSettings settings;
    private Rectangle expandedBounds;
    private DateTime lastAutoAction = DateTime.MinValue;
    private DateTime lastHeaderChange = DateTime.MinValue;
    private bool collapsed;
    private bool transitioning;
    private bool settingsReady;
    private bool autoHideArmed;
    private string collapsedEdge = String.Empty;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, int key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style |= WsMinimizeBox | WsSysMenu;
            return parameters;
        }
    }

    public ReaderForm(string appRoot)
    {
        root = appRoot;
        settingsPath = Path.Combine(root, "settings.json");
        profilePath = Path.Combine(root, "profile");
        settings = LoadSettings();
        var hasSavedBounds = File.Exists(settingsPath) && settings.Width >= 240 && settings.Height >= 360;

        Text = "WeRead";
        StartPosition = FormStartPosition.Manual;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(320, 520);
        KeyPreview = true;
        ShowInTaskbar = true;

        var work = Screen.PrimaryScreen.WorkingArea;
        Width = settings.Width >= 240 ? settings.Width : 390;
        Height = settings.Height >= 360 ? Math.Min(settings.Height, work.Height) : Math.Min(1040, work.Height);
        Left = hasSavedBounds ? settings.Left : work.Right - Width;
        Top = hasSavedBounds ? settings.Top : work.Top;
        ClampToWorkArea();

        var iconPath = Path.Combine(root, "assets", "weread.ico");
        if (File.Exists(iconPath)) Icon = new Icon(iconPath);

        titleBar = new Panel();
        titleBar.Dock = DockStyle.Top;
        titleBar.Height = 36;
        titleBar.BackColor = Color.FromArgb(248, 250, 252);
        titleBar.Paint += delegate(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(229, 232, 235)))
                e.Graphics.DrawLine(pen, 0, titleBar.Height - 1, titleBar.Width, titleBar.Height - 1);
        };

        toolTip = new ToolTip();
        toolTip.InitialDelay = 350;
        toolTip.ReshowDelay = 100;

        var appIcon = new PictureBox();
        appIcon.Size = new Size(22, 22);
        appIcon.Location = new Point(8, 7);
        appIcon.SizeMode = PictureBoxSizeMode.Zoom;
        var titleIconPath = Path.Combine(root, "assets", "weread-rounded.png");
        if (File.Exists(titleIconPath))
        {
            using (var sourceIcon = Image.FromFile(titleIconPath))
                appIcon.Image = new Bitmap(sourceIcon);
        }

        var appTitle = new Label();
        appTitle.AutoSize = true;
        appTitle.Text = "WeRead";
        appTitle.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
        appTitle.ForeColor = Color.FromArgb(44, 51, 58);
        appTitle.Location = new Point(35, 10);

        var closeButton = MakeTitleButton(CreateCloseIcon(), "Close");
        closeButton.Dock = DockStyle.Right;
        closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
        var minimizeButton = MakeTitleButton(CreateMinimizeIcon(), "Minimize");
        minimizeButton.Dock = DockStyle.Right;
        var menuButton = MakeTitleButton(CreateMenuIcon(), "Reader settings");
        menuButton.Dock = DockStyle.Right;

        readerMenu = new ContextMenuStrip();
        readerMenu.ShowImageMargin = false;
        readerMenu.Font = new Font("Segoe UI", 9f);
        readerMenu.Items.Add("\u8fd4\u56de", null, delegate { if (webView.CanGoBack) webView.GoBack(); });
        readerMenu.Items.Add("\u9996\u9875", null, delegate { Navigate("https://weread.qq.com/"); });
        readerMenu.Items.Add("\u5237\u65b0", null, delegate { webView.Reload(); });
        readerMenu.Items.Add(new ToolStripSeparator());
        readerMenu.Items.Add("\u7f29\u5c0f\u5b57\u53f7", null, delegate { SetZoom(webView.ZoomFactor - 0.1); });
        readerMenu.Items.Add("\u653e\u5927\u5b57\u53f7", null, delegate { SetZoom(webView.ZoomFactor + 0.1); });
        readerMenu.Items.Add(new ToolStripSeparator());
        autoMenuItem = new ToolStripMenuItem("\u8d34\u8fb9\u81ea\u52a8\u9690\u85cf");
        autoMenuItem.Checked = settings.AutoHideEnabled;
        autoMenuItem.Click += delegate { ToggleAutoHide(); };
        readerMenu.Items.Add(autoMenuItem);

        titleBar.Controls.Add(appIcon);
        titleBar.Controls.Add(appTitle);
        titleBar.Controls.Add(menuButton);
        titleBar.Controls.Add(minimizeButton);
        titleBar.Controls.Add(closeButton);

        webView = new WebView2();
        webView.Dock = DockStyle.Fill;
        webView.ZoomFactor = ValidZoom(settings.ZoomFactor);
        webView.CreationProperties = new CoreWebView2CreationProperties();
        webView.CreationProperties.UserDataFolder = profilePath;

        Controls.Add(webView);
        Controls.Add(titleBar);

        expandedBounds = Bounds;

        menuButton.Click += delegate { readerMenu.Show(menuButton, new Point(0, menuButton.Height)); };
        minimizeButton.Click += delegate { MinimizeReader(); };
        closeButton.Click += delegate { Close(); };
        MouseEventHandler dragWindow = delegate(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
        };
        titleBar.MouseDown += dragWindow;
        appIcon.MouseDown += dragWindow;
        appTitle.MouseDown += dragWindow;

        autoHideTimer = new System.Windows.Forms.Timer();
        autoHideTimer.Interval = 500;
        autoHideTimer.Tick += AutoHideTick;
        autoHideTimer.Start();

        saveTimer = new System.Windows.Forms.Timer();
        saveTimer.Interval = 500;
        saveTimer.Tick += delegate { saveTimer.Stop(); SaveSettings(); };

        tray = new NotifyIcon();
        tray.Text = "WeRead";
        tray.Icon = Icon ?? SystemIcons.Application;
        tray.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show", null, delegate { ShowReader(); });
        menu.Items.Add("Minimize", null, delegate { MinimizeReader(); });
        menu.Items.Add("Home", null, delegate { ShowReader(); Navigate("https://weread.qq.com/"); });
        menu.Items.Add("Reload", null, delegate { webView.Reload(); });
        menu.Items.Add("Exit", null, delegate { Close(); });
        tray.ContextMenuStrip = menu;
        tray.MouseClick += delegate(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) ToggleVisible();
        };

        Shown += async delegate
        {
            try
            {
                settingsReady = true;
                RegisterHotKey(Handle, HotkeyId, ModControl | ModShift, VkY);
                await webView.EnsureCoreWebView2Async(null);
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.WebMessageReceived += delegate(object sender, CoreWebView2WebMessageReceivedEventArgs args)
                {
                    var message = args.TryGetWebMessageAsString();
                    if (message == "header:hide") SetTitleBarVisible(false);
                    else if (message == "header:show") SetTitleBarVisible(true);
                };
                await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
(function () {
  if (window.__wereadHeaderScrollInstalled) return;
  window.__wereadHeaderScrollInstalled = true;
  const positions = new WeakMap();
  let lastWindowY = window.scrollY || 0;
  let lastSent = 0;

  function send(direction) {
    const now = Date.now();
    if (now - lastSent < 100) return;
    lastSent = now;
    window.chrome.webview.postMessage(direction > 0 ? 'header:hide' : 'header:show');
  }

  window.addEventListener('wheel', function (event) {
    if (Math.abs(event.deltaY) >= 8) send(event.deltaY);
  }, { passive: true, capture: true });

  window.addEventListener('scroll', function (event) {
    const target = event.target;
    if (target && target !== document && typeof target.scrollTop === 'number') {
      const current = target.scrollTop;
      const previous = positions.has(target) ? positions.get(target) : current;
      positions.set(target, current);
      const delta = current - previous;
      if (Math.abs(delta) >= 8) send(delta);
      return;
    }

    const current = window.scrollY || document.documentElement.scrollTop || document.body.scrollTop || 0;
    const delta = current - lastWindowY;
    lastWindowY = current;
    if (Math.abs(delta) >= 8) send(delta);
  }, true);
})();");
                webView.CoreWebView2.NavigationCompleted += delegate { RememberUrl(); };
                webView.CoreWebView2.SourceChanged += delegate { RememberUrl(); };
                SetZoom(ValidZoom(settings.ZoomFactor));
                webView.CoreWebView2.Navigate(ValidUrl(settings.CurrentUrl));
                SaveSettings();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "WebView2 initialization failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        Move += delegate { RememberBounds(); };
        Resize += delegate { RememberBounds(); };
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && (int)m.WParam == HotkeyId)
        {
            ToggleVisible();
            return;
        }
        base.WndProc(ref m);

        if (m.Msg == 0x0084 && !collapsed)
        {
            var value = m.LParam.ToInt64();
            var screenPoint = new Point((short)(value & 0xFFFF), (short)((value >> 16) & 0xFFFF));
            var point = PointToClient(screenPoint);
            const int grip = 6;
            var left = point.X <= grip;
            var right = point.X >= ClientSize.Width - grip;
            var top = point.Y <= grip;
            var bottom = point.Y >= ClientSize.Height - grip;

            if (left && top) m.Result = (IntPtr)13;
            else if (right && top) m.Result = (IntPtr)14;
            else if (left && bottom) m.Result = (IntPtr)16;
            else if (right && bottom) m.Result = (IntPtr)17;
            else if (left) m.Result = (IntPtr)10;
            else if (right) m.Result = (IntPtr)11;
            else if (top) m.Result = (IntPtr)12;
            else if (bottom) m.Result = (IntPtr)15;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        UnregisterHotKey(Handle, HotkeyId);
        autoHideTimer.Stop();
        saveTimer.Stop();
        SaveSettings();
        tray.Visible = false;
        if (tray.ContextMenuStrip != null) tray.ContextMenuStrip.Dispose();
        tray.Dispose();
        readerMenu.Dispose();
        toolTip.Dispose();
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            MinimizeReader();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    public void ToggleVisible()
    {
        if (!Visible || WindowState == FormWindowState.Minimized)
            ShowReader();
        else
            MinimizeReader();
    }

    public void ShowReader()
    {
        if (collapsed) ExpandFromEdge(true);
        Show();
        WindowState = FormWindowState.Normal;
        autoHideArmed = Bounds.Contains(Cursor.Position);
        lastAutoAction = DateTime.Now;
        SetTitleBarVisible(true, true);
        BringToFront();
        Activate();
    }

    private void MinimizeReader()
    {
        if (collapsed) ExpandFromEdge(true);
        autoHideArmed = false;
        WindowState = FormWindowState.Minimized;
    }

    private Button MakeTitleButton(Image image, string hint)
    {
        var button = new Button();
        button.Width = 40;
        button.Height = 35;
        button.Margin = new Padding(0);
        button.Padding = new Padding(0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(244, 246, 248);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(229, 235, 241);
        button.UseVisualStyleBackColor = false;
        button.BackColor = Color.FromArgb(248, 250, 252);
        button.Image = image;
        button.ImageAlign = ContentAlignment.MiddleCenter;
        button.Cursor = Cursors.Hand;
        toolTip.SetToolTip(button, hint);
        return button;
    }

    private Bitmap CreateIcon(Action<Graphics, Pen, Brush> draw)
    {
        var bitmap = new Bitmap(20, 20);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var pen = new Pen(Color.FromArgb(65, 72, 80), 1.8f))
        using (var brush = new SolidBrush(Color.FromArgb(65, 72, 80)))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
            draw(graphics, pen, brush);
        }
        return bitmap;
    }

    private Bitmap CreateMenuIcon()
    {
        return CreateIcon(delegate(Graphics g, Pen p, Brush b)
        {
            g.DrawLine(p, 5, 6.5f, 15, 6.5f);
            g.DrawLine(p, 5, 10, 15, 10);
            g.DrawLine(p, 5, 13.5f, 15, 13.5f);
        });
    }

    private Bitmap CreateMinimizeIcon()
    {
        return CreateIcon(delegate(Graphics g, Pen p, Brush b)
        {
            g.DrawLine(p, 5, 13, 15, 13);
        });
    }

    private Bitmap CreateCloseIcon()
    {
        return CreateIcon(delegate(Graphics g, Pen p, Brush b)
        {
            g.DrawLine(p, 6, 6, 14, 14);
            g.DrawLine(p, 14, 6, 6, 14);
        });
    }

    private ReaderSettings LoadSettings()
    {
        try
        {
            if (!File.Exists(settingsPath)) return Defaults();
            var loaded = json.Deserialize<ReaderSettings>(File.ReadAllText(settingsPath));
            return loaded ?? Defaults();
        }
        catch
        {
            return Defaults();
        }
    }

    private ReaderSettings Defaults()
    {
        return new ReaderSettings
        {
            Width = 390,
            Height = 1040,
            ZoomFactor = 1.0,
            CurrentUrl = "https://weread.qq.com/"
        };
    }

    private void SaveSettings()
    {
        if (!settingsReady || collapsed || transitioning) return;
        var bounds = Bounds;
        if (bounds.Width < 240 || bounds.Height < 360) return;
        settings.Width = bounds.Width;
        settings.Height = bounds.Height;
        settings.Left = bounds.Left;
        settings.Top = bounds.Top;
        settings.ZoomFactor = webView.ZoomFactor;
        try
        {
            var temporaryPath = settingsPath + ".tmp";
            File.WriteAllText(temporaryPath, json.Serialize(settings));
            if (File.Exists(settingsPath))
                File.Replace(temporaryPath, settingsPath, null);
            else
                File.Move(temporaryPath, settingsPath);
        }
        catch (IOException)
        {
            // Settings persistence is best-effort and must not interrupt reading.
        }
        catch (UnauthorizedAccessException)
        {
            // Portable folders can be read-only.
        }
    }

    private void SaveSoon()
    {
        saveTimer.Stop();
        saveTimer.Start();
    }

    private void RememberBounds()
    {
        if (collapsed || transitioning || WindowState != FormWindowState.Normal) return;
        expandedBounds = Bounds;
        SaveSoon();
    }

    private void ClampToWorkArea()
    {
        var work = Screen.FromControl(this).WorkingArea;
        Width = Math.Min(Width, work.Width);
        Height = Math.Min(Height, work.Height);
        Left = Math.Max(work.Left, Math.Min(Left, work.Right - Width));
        Top = Math.Max(work.Top, Math.Min(Top, work.Bottom - Height));
    }

    private string ValidUrl(string value)
    {
        if (!String.IsNullOrEmpty(value) &&
            (value.StartsWith("https://weread.qq.com/") || value.StartsWith("https://i.weread.qq.com/")))
            return value;
        return "https://weread.qq.com/";
    }

    private double ValidZoom(double value)
    {
        return value >= 0.8 && value <= 1.8 ? value : 1.0;
    }

    private void Navigate(string url)
    {
        settings.CurrentUrl = url;
        SaveSettings();
        if (webView.CoreWebView2 != null) webView.CoreWebView2.Navigate(url);
    }

    private void RememberUrl()
    {
        var source = webView.CoreWebView2.Source;
        if (source.StartsWith("https://weread.qq.com/") || source.StartsWith("https://i.weread.qq.com/"))
        {
            settings.CurrentUrl = source;
            SaveSoon();
        }
    }

    private void SetZoom(double value)
    {
        webView.ZoomFactor = Math.Max(0.8, Math.Min(1.8, value));
        settings.ZoomFactor = webView.ZoomFactor;
        SaveSettings();
    }

    private void SetTitleBarVisible(bool visible, bool force = false)
    {
        if (!Visible || collapsed || titleBar.Visible == visible) return;
        if (!visible && readerMenu.Visible) return;
        if (!force && (DateTime.Now - lastHeaderChange).TotalMilliseconds < 120) return;

        titleBar.Visible = visible;
        lastHeaderChange = DateTime.Now;
    }

    private string DockedEdge()
    {
        var work = Screen.FromControl(this).WorkingArea;
        if (Math.Abs(Right - work.Right) <= 16) return "Right";
        if (Math.Abs(Left - work.Left) <= 16) return "Left";
        if (Math.Abs(Top - work.Top) <= 16) return "Top";
        return String.Empty;
    }

    private void ToggleAutoHide()
    {
        settings.AutoHideEnabled = !settings.AutoHideEnabled;
        autoMenuItem.Checked = settings.AutoHideEnabled;
        if (!settings.AutoHideEnabled && collapsed) ExpandFromEdge(true);
        SaveSettings();
    }

    private void AutoHideTick(object sender, EventArgs e)
    {
        if (!settings.AutoHideEnabled || !Visible) return;
        if (collapsed)
        {
            if (PointerInCollapsedTrigger()) ExpandFromEdge();
            return;
        }

        if (!autoHideArmed)
        {
            if (Bounds.Contains(Cursor.Position)) autoHideArmed = true;
            return;
        }
        var edge = DockedEdge();
        if (edge == "Top" && PointerNearTopTrigger()) return;
        if (!Bounds.Contains(Cursor.Position) && edge.Length > 0) CollapseToEdge(edge);
    }

    private bool PointerNearTopTrigger()
    {
        var point = Cursor.Position;
        var work = Screen.FromControl(this).WorkingArea;
        return point.Y >= work.Top && point.Y <= work.Top + 18 && point.X >= Left && point.X <= Right;
    }

    private bool PointerInCollapsedTrigger()
    {
        var point = Cursor.Position;
        var work = Screen.FromControl(this).WorkingArea;
        const int trigger = 12;

        if (collapsedEdge == "Right")
            return point.X >= work.Right - trigger && point.X <= work.Right &&
                   point.Y >= expandedBounds.Top && point.Y <= expandedBounds.Bottom;
        if (collapsedEdge == "Left")
            return point.X >= work.Left && point.X <= work.Left + trigger &&
                   point.Y >= expandedBounds.Top && point.Y <= expandedBounds.Bottom;
        if (collapsedEdge == "Top")
            return point.Y >= work.Top && point.Y <= work.Top + trigger &&
                   point.X >= expandedBounds.Left && point.X <= expandedBounds.Right;
        return false;
    }

    private void CollapseToEdge(string edge)
    {
        if (collapsed || transitioning) return;
        if ((DateTime.Now - lastAutoAction).TotalMilliseconds < 900) return;

        expandedBounds = Bounds;
        var work = Screen.FromControl(this).WorkingArea;
        transitioning = true;
        collapsed = true;
        collapsedEdge = edge;

        var target = expandedBounds.Location;
        if (edge == "Right")
            target = new Point(work.Right - 8, ClampTop(work, expandedBounds.Top, expandedBounds.Height));
        else if (edge == "Left")
            target = new Point(work.Left - expandedBounds.Width + 8, ClampTop(work, expandedBounds.Top, expandedBounds.Height));
        else
            target = new Point(
                Math.Max(work.Left, Math.Min(expandedBounds.Left, work.Right - expandedBounds.Width)),
                work.Top - expandedBounds.Height + 8);

        SetWindowPos(Handle, new IntPtr(-1), target.X, target.Y, 0, 0, 0x0001 | 0x0010);
        transitioning = false;
        lastAutoAction = DateTime.Now;
    }

    private int ClampTop(Rectangle work, int top, int height)
    {
        var actualHeight = Math.Min(height, work.Height);
        return Math.Max(work.Top, Math.Min(top, work.Bottom - actualHeight));
    }

    private void ExpandFromEdge(bool force = false)
    {
        if (!collapsed || transitioning) return;
        if (!force && (DateTime.Now - lastAutoAction).TotalMilliseconds < 180) return;

        transitioning = true;
        collapsed = false;
        collapsedEdge = String.Empty;
        SetWindowPos(
            Handle,
            new IntPtr(-2),
            expandedBounds.Left,
            expandedBounds.Top,
            expandedBounds.Width,
            expandedBounds.Height,
            0x0010);
        autoHideArmed = Bounds.Contains(Cursor.Position);
        if (force) Activate();
        transitioning = false;
        lastAutoAction = DateTime.Now;
    }

}
