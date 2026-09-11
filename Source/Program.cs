using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace ByeDPIControl;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        if (args.Length >= 2 && args[0] == "--service-action")
        {
            string? resultPath = null;
            for (var i = 2; i < args.Length - 1; i++)
                if (args[i] == "--result") resultPath = args[i + 1];

            Environment.ExitCode = ElevatedServiceAction.Run(args[1], resultPath);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetDefaultFont(new Font("Segoe UI", 10F));
        Application.Run(new MainForm());
    }
}

internal enum NativeServiceState : uint
{
    Unknown = 0,
    Stopped = 1,
    StartPending = 2,
    StopPending = 3,
    Running = 4,
    ContinuePending = 5,
    PausePending = 6,
    Paused = 7
}

internal static class ServiceNative
{
    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceQueryStatus = 0x0004;
    private const uint ServiceStart = 0x0010;
    private const uint ServiceStop = 0x0020;
    private const uint ServiceControlStop = 0x00000001;
    private const int ScStatusProcessInfo = 0;
    private const int ErrorServiceAlreadyRunning = 1056;
    private const int ErrorServiceNotActive = 1062;

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenService(IntPtr serviceManager, string serviceName, uint desiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(IntPtr service, int infoLevel,
        ref ServiceStatusProcess buffer, int bufferSize, out int bytesNeeded);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool StartService(IntPtr service, int argumentCount, IntPtr arguments);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ControlService(IntPtr service, uint control, ref ServiceStatus status);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    public static NativeServiceState Query(string serviceName)
    {
        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero) return NativeServiceState.Unknown;
        try
        {
            var service = OpenService(manager, serviceName, ServiceQueryStatus);
            if (service == IntPtr.Zero) return NativeServiceState.Unknown;
            try
            {
                var status = new ServiceStatusProcess();
                return QueryServiceStatusEx(service, ScStatusProcessInfo, ref status,
                    Marshal.SizeOf<ServiceStatusProcess>(), out _)
                    ? (NativeServiceState)status.CurrentState
                    : NativeServiceState.Unknown;
            }
            finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }

    public static void Start(string serviceName)
    {
        WithService(serviceName, ServiceStart | ServiceQueryStatus, service =>
        {
            var state = QueryHandle(service);
            if (state == NativeServiceState.Running) return;
            if (!StartService(service, 0, IntPtr.Zero))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorServiceAlreadyRunning) throw new Win32Exception(error);
            }
            WaitFor(serviceName, NativeServiceState.Running, TimeSpan.FromSeconds(20));
        });
    }

    public static void Stop(string serviceName)
    {
        WithService(serviceName, ServiceStop | ServiceQueryStatus, service =>
        {
            var state = QueryHandle(service);
            if (state == NativeServiceState.Stopped) return;
            var status = new ServiceStatus();
            if (!ControlService(service, ServiceControlStop, ref status))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorServiceNotActive) throw new Win32Exception(error);
            }
            WaitFor(serviceName, NativeServiceState.Stopped, TimeSpan.FromSeconds(20));
        });
    }

    private static NativeServiceState QueryHandle(IntPtr service)
    {
        var status = new ServiceStatusProcess();
        if (!QueryServiceStatusEx(service, ScStatusProcessInfo, ref status,
                Marshal.SizeOf<ServiceStatusProcess>(), out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return (NativeServiceState)status.CurrentState;
    }

    private static void WithService(string name, uint access, Action<IntPtr> action)
    {
        var manager = OpenSCManager(null, null, ScManagerConnect);
        if (manager == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var service = OpenService(manager, name, access);
            if (service == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), $"تعذر فتح خدمة {name}");
            try { action(service); }
            finally { CloseServiceHandle(service); }
        }
        finally { CloseServiceHandle(manager); }
    }

    private static void WaitFor(string name, NativeServiceState desired, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < until)
        {
            if (Query(name) == desired) return;
            Thread.Sleep(300);
        }
        throw new TimeoutException($"انتهت مهلة انتظار خدمة {name}");
    }
}

internal static class ElevatedServiceAction
{
    private const string ByeDpi = "ByeDPI";
    private const string ProxiFyre = "ProxiFyreService";

    public static int Run(string action, string? resultPath)
    {
        try
        {
            switch (action)
            {
                case "start":
                    ServiceNative.Start(ByeDpi);
                    ServiceNative.Start(ProxiFyre);
                    break;
                case "stop":
                    ServiceNative.Stop(ProxiFyre);
                    ServiceNative.Stop(ByeDpi);
                    break;
                case "restart":
                    ServiceNative.Stop(ProxiFyre);
                    ServiceNative.Stop(ByeDpi);
                    ServiceNative.Start(ByeDpi);
                    ServiceNative.Start(ProxiFyre);
                    break;
                default:
                    throw new ArgumentException("إجراء غير معروف");
            }

            if (!string.IsNullOrWhiteSpace(resultPath)) File.WriteAllText(resultPath, "OK", Encoding.UTF8);
            return 0;
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(resultPath))
            {
                try { File.WriteAllText(resultPath, ex.Message, Encoding.UTF8); } catch { }
            }
            return 1;
        }
    }
}

internal sealed class RoundedPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 18;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = Color.Transparent;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderWidth { get; set; } = 1;

    public RoundedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = UiGeometry.RoundRect(rect, Radius);
        using var fill = new SolidBrush(BackColor);
        e.Graphics.FillPath(fill, path);
        if (BorderColor != Color.Transparent && BorderWidth > 0)
        {
            using var pen = new Pen(BorderColor, BorderWidth);
            e.Graphics.DrawPath(pen, path);
        }
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        using var path = UiGeometry.RoundRect(new Rectangle(0, 0, Width, Height), Radius);
        Region = new Region(path);
    }
}

internal sealed class RoundedButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Radius { get; set; } = 14;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor { get; set; }
    private Color _normalColor;

    public RoundedButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        _normalColor = BackColor;
        if (HoverColor == Color.Empty) HoverColor = ControlPaint.Light(BackColor, 0.12f);
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); BackColor = HoverColor; }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); BackColor = _normalColor; }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        using var path = UiGeometry.RoundRect(new Rectangle(0, 0, Width, Height), Radius);
        Region = new Region(path);
    }
}

internal static class UiGeometry
{
    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(2, radius * 2);
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class MainForm : Form
{
    private static readonly Color Navy = Color.FromArgb(15, 30, 53);
    private static readonly Color TextDark = Color.FromArgb(24, 39, 62);
    private static readonly Color Muted = Color.FromArgb(101, 116, 139);
    private static readonly Color Green = Color.FromArgb(22, 163, 74);
    private static readonly Color GreenSoft = Color.FromArgb(232, 248, 238);
    private static readonly Color Red = Color.FromArgb(220, 38, 38);
    private static readonly Color Orange = Color.FromArgb(217, 119, 6);
    private static readonly Color GraySoft = Color.FromArgb(241, 245, 249);
    private static readonly Color Blue = Color.FromArgb(37, 99, 235);

    private readonly Label _overallTitle = new();
    private readonly Label _overallDetail = new();
    private readonly Label _overallIcon = new();
    private readonly Label _byeStatus = new();
    private readonly Label _byeDot = new();
    private readonly Label _proxyStatus = new();
    private readonly Label _proxyDot = new();
    private readonly Label _listenerStatus = new();
    private readonly Label _robloxStatus = new();
    private readonly RoundedButton _startButton;
    private readonly RoundedButton _stopButton;
    private readonly RoundedButton _restartButton;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2500 };
    private bool _busy;

    public MainForm()
    {
        Text = "مركز تحكم Roblox";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 650);
        MinimumSize = new Size(916, 689);
        MaximumSize = new Size(916, 689);
        BackColor = Color.FromArgb(246, 248, 252);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;

        var header = new Panel { Dock = DockStyle.Top, Height = 116, BackColor = Navy };
        Controls.Add(header);

        var logo = new Label
        {
            Text = "R",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Blue,
            Size = new Size(58, 58),
            Location = new Point(810, 28)
        };
        using (var p = new GraphicsPath())
        {
            p.AddEllipse(0, 0, 58, 58);
            logo.Region = new Region(p);
        }
        header.Controls.Add(logo);

        header.Controls.Add(new Label
        {
            Text = "مركز تحكم Roblox",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(430, 23),
            Size = new Size(365, 38)
        });
        header.Controls.Add(new Label
        {
            Text = "إدارة اتصال ByeDPI وProxiFyre بسهولة وأمان",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = Color.FromArgb(190, 205, 225),
            Location = new Point(390, 63),
            Size = new Size(405, 28)
        });
        header.Controls.Add(new Label
        {
            Text = "الإصدار 1.0",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(28, 42),
            Size = new Size(120, 24)
        });

        var overall = new RoundedPanel
        {
            Radius = 20,
            BackColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            Location = new Point(28, 136),
            Size = new Size(844, 92)
        };
        Controls.Add(overall);
        _overallIcon.SetBounds(760, 19, 54, 54);
        _overallIcon.Font = new Font("Segoe UI Symbol", 25, FontStyle.Bold);
        _overallIcon.TextAlign = ContentAlignment.MiddleCenter;
        overall.Controls.Add(_overallIcon);
        _overallTitle.SetBounds(385, 16, 355, 31);
        _overallTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        _overallTitle.ForeColor = TextDark;
        _overallTitle.TextAlign = ContentAlignment.MiddleRight;
        overall.Controls.Add(_overallTitle);
        _overallDetail.SetBounds(250, 49, 490, 25);
        _overallDetail.Font = new Font("Segoe UI", 10);
        _overallDetail.ForeColor = Muted;
        _overallDetail.TextAlign = ContentAlignment.MiddleRight;
        overall.Controls.Add(_overallDetail);
        _robloxStatus.SetBounds(30, 28, 205, 35);
        _robloxStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _robloxStatus.TextAlign = ContentAlignment.MiddleCenter;
        overall.Controls.Add(_robloxStatus);

        var byeCard = CreateServiceCard(453, 246, "خادم ByeDPI", "تجاوز الحجب وإنشاء بروكسي محلي", "●", out _byeStatus, out _byeDot);
        var proxyCard = CreateServiceCard(28, 246, "موجّه Roblox", "تمرير اتصال Roblox عبر البروكسي", "●", out _proxyStatus, out _proxyDot);
        Controls.Add(byeCard);
        Controls.Add(proxyCard);

        var actions = new RoundedPanel
        {
            Radius = 18,
            BackColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            Location = new Point(28, 371),
            Size = new Size(844, 112)
        };
        Controls.Add(actions);
        actions.Controls.Add(new Label
        {
            Text = "التحكم بالخدمات",
            Location = new Point(630, 12),
            Size = new Size(185, 25),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = TextDark
        });

        _startButton = MakeButton("▶  تشغيل الكل", Green, new Point(563, 50), new Size(248, 45));
        _stopButton = MakeButton("■  إيقاف الكل", Red, new Point(298, 50), new Size(248, 45));
        _restartButton = MakeButton("↻  إعادة التشغيل", Orange, new Point(33, 50), new Size(248, 45));
        _startButton.Click += async (_, _) => await RunServiceAction("start", "تم تشغيل الخادم والبروكسي بنجاح.");
        _stopButton.Click += async (_, _) => await RunServiceAction("stop", "تم إيقاف الخادم والبروكسي.");
        _restartButton.Click += async (_, _) => await RunServiceAction("restart", "تمت إعادة تشغيل الخادم والبروكسي بنجاح.");
        actions.Controls.AddRange([_startButton, _stopButton, _restartButton]);

        var info = new RoundedPanel
        {
            Radius = 18,
            BackColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            Location = new Point(28, 500),
            Size = new Size(844, 103)
        };
        Controls.Add(info);

        _listenerStatus.SetBounds(584, 17, 230, 24);
        _listenerStatus.TextAlign = ContentAlignment.MiddleRight;
        _listenerStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        info.Controls.Add(_listenerStatus);
        info.Controls.Add(new Label
        {
            Text = "العنوان المحلي:  127.0.0.1:1080",
            Location = new Point(535, 48), Size = new Size(279, 25),
            TextAlign = ContentAlignment.MiddleRight, ForeColor = Muted
        });
        info.Controls.Add(new Label
        {
            Text = "يعمل تلقائيًا مع بدء ويندوز  •  Roblox فقط",
            Location = new Point(450, 73), Size = new Size(364, 22),
            TextAlign = ContentAlignment.MiddleRight, ForeColor = Muted,
            Font = new Font("Segoe UI", 9)
        });

        var robloxButton = MakeButton("تشغيل Roblox", Blue, new Point(265, 28), new Size(155, 47));
        robloxButton.Click += (_, _) => LaunchRoblox();
        var logsButton = MakeButton("فتح السجلات", Color.FromArgb(71, 85, 105), new Point(101, 28), new Size(145, 47));
        logsButton.Click += (_, _) => OpenLogs();
        var refreshButton = MakeButton("تحديث", Color.FromArgb(100, 116, 139), new Point(19, 28), new Size(70, 47));
        refreshButton.Click += (_, _) => RefreshStatus();
        info.Controls.AddRange([robloxButton, logsButton, refreshButton]);

        Controls.Add(new Label
        {
            Text = "الاتصال محصور بجهازك ولا يتم تمرير بقية البرامج عبر البروكسي.",
            Location = new Point(180, 611),
            Size = new Size(540, 25),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9)
        });

        Shown += (_, _) => RefreshStatus();
        _timer.Tick += (_, _) => { if (!_busy) RefreshStatus(); };
        _timer.Start();
    }

    private RoundedPanel CreateServiceCard(int x, int y, string title, string description, string dotText,
        out Label status, out Label dot)
    {
        var card = new RoundedPanel
        {
            Radius = 18,
            BackColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            Location = new Point(x, y),
            Size = new Size(419, 107)
        };
        dot = new Label
        {
            Text = dotText, Location = new Point(366, 19), Size = new Size(26, 26),
            Font = new Font("Segoe UI", 16, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter
        };
        card.Controls.Add(dot);
        card.Controls.Add(new Label
        {
            Text = title, Location = new Point(155, 15), Size = new Size(205, 29),
            TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = TextDark
        });
        card.Controls.Add(new Label
        {
            Text = description, Location = new Point(70, 47), Size = new Size(290, 23),
            TextAlign = ContentAlignment.MiddleRight, ForeColor = Muted, Font = new Font("Segoe UI", 9.5f)
        });
        status = new Label
        {
            Location = new Point(212, 75), Size = new Size(148, 23),
            TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        card.Controls.Add(status);
        return card;
    }

    private static RoundedButton MakeButton(string text, Color color, Point location, Size size)
    {
        return new RoundedButton
        {
            Text = text,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Location = location,
            Size = size,
            Radius = 13,
            TabStop = true
        };
    }

    private void RefreshStatus()
    {
        var bye = ServiceNative.Query("ByeDPI");
        var proxy = ServiceNative.Query("ProxiFyreService");
        var listener = IsProxyListening();
        var roblox = Process.GetProcessesByName("RobloxPlayerBeta").Length > 0;

        SetServiceLabel(_byeStatus, _byeDot, bye);
        SetServiceLabel(_proxyStatus, _proxyDot, proxy);

        if (bye == NativeServiceState.Running && proxy == NativeServiceState.Running && listener)
        {
            _overallIcon.Text = "✓";
            _overallIcon.ForeColor = Green;
            _overallTitle.Text = "الاتصال جاهز ويعمل";
            _overallDetail.Text = "ByeDPI وProxiFyre يعملان بصورة طبيعية";
            _listenerStatus.Text = "●  البروكسي المحلي متصل";
            _listenerStatus.ForeColor = Green;
        }
        else if (bye == NativeServiceState.Stopped && proxy == NativeServiceState.Stopped)
        {
            _overallIcon.Text = "○";
            _overallIcon.ForeColor = Muted;
            _overallTitle.Text = "الاتصال متوقف";
            _overallDetail.Text = "اضغط «تشغيل الكل» لتفعيل اتصال Roblox";
            _listenerStatus.Text = "●  البروكسي المحلي متوقف";
            _listenerStatus.ForeColor = Muted;
        }
        else
        {
            _overallIcon.Text = "!";
            _overallIcon.ForeColor = Orange;
            _overallTitle.Text = "يحتاج إلى انتباه";
            _overallDetail.Text = "إحدى الخدمات غير جاهزة؛ استخدم إعادة التشغيل";
            _listenerStatus.Text = "●  الاتصال غير مكتمل";
            _listenerStatus.ForeColor = Orange;
        }

        _robloxStatus.Text = roblox ? "●  Roblox يعمل الآن" : "○  Roblox غير مشغّل";
        _robloxStatus.ForeColor = roblox ? Blue : Muted;
    }

    private static void SetServiceLabel(Label status, Label dot, NativeServiceState state)
    {
        switch (state)
        {
            case NativeServiceState.Running:
                status.Text = "قيد التشغيل";
                status.ForeColor = Green;
                dot.ForeColor = Green;
                break;
            case NativeServiceState.Stopped:
                status.Text = "متوقف";
                status.ForeColor = Muted;
                dot.ForeColor = Color.FromArgb(148, 163, 184);
                break;
            case NativeServiceState.StartPending:
            case NativeServiceState.StopPending:
                status.Text = "جارٍ التنفيذ...";
                status.ForeColor = Orange;
                dot.ForeColor = Orange;
                break;
            default:
                status.Text = "غير متاح";
                status.ForeColor = Red;
                dot.ForeColor = Red;
                break;
        }
    }

    private static bool IsProxyListening()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                .Any(ep => ep.Port == 1080 && IPAddress.IsLoopback(ep.Address));
        }
        catch { return false; }
    }

    private async Task RunServiceAction(string action, string successMessage)
    {
        if (_busy) return;
        _busy = true;
        SetButtonsEnabled(false);
        _overallTitle.Text = "جارٍ تنفيذ الطلب...";
        _overallDetail.Text = "قد تظهر نافذة صلاحيات ويندوز؛ اختر نعم";
        _overallIcon.Text = "…";
        _overallIcon.ForeColor = Blue;

        var resultPath = Path.Combine(Path.GetTempPath(), $"byedpi-control-{Guid.NewGuid():N}.txt");
        try
        {
            var exe = Environment.ProcessPath ?? Application.ExecutablePath;
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--service-action {action} --result \"{resultPath}\"",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("تعذر بدء العملية الإدارية");
            await process.WaitForExitAsync();
            var result = File.Exists(resultPath) ? File.ReadAllText(resultPath, Encoding.UTF8).Trim('\uFEFF', '\r', '\n', ' ') : "";
            if (process.ExitCode != 0 || result != "OK")
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result) ? "لم يكتمل الطلب." : result);

            await Task.Delay(600);
            RefreshStatus();
            MessageBox.Show(this, successMessage, "تم بنجاح", MessageBoxButtons.OK, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            MessageBox.Show(this, "تم إلغاء طلب صلاحية المسؤول، لذلك لم يتم إجراء أي تغيير.", "تم الإلغاء",
                MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"تعذر تنفيذ الطلب:\n{ex.Message}", "حدث خطأ",
                MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
        finally
        {
            try { if (File.Exists(resultPath)) File.Delete(resultPath); } catch { }
            _busy = false;
            SetButtonsEnabled(true);
            RefreshStatus();
        }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _startButton.Enabled = enabled;
        _stopButton.Enabled = enabled;
        _restartButton.Enabled = enabled;
    }

    private void LaunchRoblox()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "shell:AppsFolder\\ROBLOXCorporation.RobloxGDK_55nm5eh3cm0pr!Game",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"تعذر تشغيل Roblox:\n{ex.Message}", "خطأ", MessageBoxButtons.OK,
                MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
        }
    }

    private void OpenLogs()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "ProxiFyre", "logs");
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }
}
