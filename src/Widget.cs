using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WorkCountdown
{
    public class WidgetForm : Form
    {
        // 96 DPI 下的设计尺寸，实际按 scale 放大
        private const int DesignExpandedW = 280;
        private const int DesignExpandedH = 186;        // 不含午休行
        private const int DesignExpandedHLunch = 210;   // 含午休行
        private const int DesignCollapsedW = 224;
        private const int DesignCollapsedH = 68;

        private readonly SettingsStore store;
        private Settings settings;
        private readonly LarkClient lark;

        private AttendanceSnapshot snapshot;
        private CardStatus status;
        private ThemeDef theme;
        private float scale = 1f;
        private bool fetching;
        private bool lastFetchFailed;
        private DateTime lastDay = DateTime.Today;

        private bool collapsed;
        private readonly System.Windows.Forms.Timer tick;
        private readonly System.Windows.Forms.Timer poll;
        private readonly System.Windows.Forms.Timer autoCollapse;
        private readonly System.Windows.Forms.Timer animT;
        private readonly System.Windows.Forms.Timer calPoll;
        private readonly System.Windows.Forms.Timer remindT;

        // 日程提醒
        private List<ReminderEvent> larkEvents = new List<ReminderEvent>();
        private string larkEventError;
        private bool calFetching;
        private readonly HashSet<string> notified = new HashSet<string>();
        private bool reminding;
        private DateTime remindAt;
        private ReminderEvent remindEvent;
        private const int RemindInMs = 1000;    // 两块色块先后扫入（A 0-300ms，B 260-660ms）
        private int RemindHoldMs { get { return Math.Max(3, Math.Min(20, settings.ui.reminderHoldSeconds)) * 1000; } } // // 完全覆盖
        private const int RemindOutMs = 960;   // 两块色块扫出
        // A = 第一块（整卡），B = 第二块（压在底部一半），B 稍晚到、稍晚走
        private const double RemindAMs = 300;
        private const double RemindBDelayMs = 260;
        private const double RemindBMs = 380;
        private const double RemindOutAMs = 300;
        private const double RemindOutBDelayMs = 150;
        private const double RemindOutBMs = 360;
        private Font fontRemindBig;
        private Font fontRemindSmall;
        private Font fontRemindTitle;
        private Font fontRemindMeta;
        private Font fontRemindTime;
        private Font fontCollapsedText;

        // 终盘告警态（ADR 0006）
        private readonly Font fontAlertBig;
        private readonly Font fontAlertCs;
        private readonly Font fontAlertMeta;
        private readonly Font fontAlertLabel;
        private readonly Font fontAlertStrip;
        private readonly Font fontAlertStripCs;
        private readonly Font fontAlertDone;
        private System.Windows.Forms.Timer alertT;
        private bool alertOn;
        private int alertEnteredAt;
        private bool alertDismissed;          // 用户在本次终盘里手动收起过
        private bool alertPreview;            // 右键菜单里手动放的预览
        private DateTime alertDoneAt = DateTime.MinValue;
        private DateTime alertTarget;
        private bool alertLunch;
        private string alertLabel, alertEn, alertTargetText;

        private bool animating;
        private int animFromW, animFromH, animFromX, animToW, animToH, animToX;
        private int animStart;
        private const int AnimMs = 150;

        private bool dragging;
        private bool moved;
        private Point dragOrigin;
        private Point formOrigin;

        private readonly ContextMenuStrip menu;
        private NotifyIcon tray;
        private readonly ToolTip reminderTip = new ToolTip { InitialDelay = 800, ReshowDelay = 200, AutoPopDelay = 20000, ShowAlways = true };

        private readonly Font fontBig;
        private readonly Font fontBigText;
        private readonly Font fontStatus;
        private readonly Font fontTiny;
        private readonly Font fontSmall;
        private readonly Font fontCollapsedNum;
        private readonly Font fontCollapsedLabel;

        private readonly StringFormat sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        private readonly StringFormat sfNear = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        private readonly StringFormat sfFar = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

        private Rectangle rectGear, rectRefresh, rectChevron;

        public WidgetForm(SettingsStore store, Settings settings)
        {
            this.store = store;
            this.settings = settings;
            this.lark = new LarkClient(store);
            this.theme = Themes.Get(settings.ui.theme);

            using (var screen = Graphics.FromHwnd(IntPtr.Zero))
            {
                scale = screen.DpiX / 96f;
            }
            if (scale < 0.75f) scale = 0.75f;
            if (scale > 3f) scale = 3f;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            AutoScaleMode = AutoScaleMode.None;
            Text = "下班倒计时";
            MinimumSize = new Size(S(DesignCollapsedW), S(DesignCollapsedH));

            fontBig = CreateRoleFont(FontRole.Timer, 28f, FontStyle.Regular);
            fontBigText = CreateRoleFont(FontRole.Body, 17f, FontStyle.Regular);
            fontStatus = CreateRoleFont(FontRole.Body, 9f, FontStyle.Regular);
            fontTiny = CreateRoleFont(FontRole.Body, 8.5f, FontStyle.Regular);
            fontSmall = CreateRoleFont(FontRole.Body, 9f, FontStyle.Regular);
            fontCollapsedNum = CreateRoleFont(FontRole.Timer, 17f, FontStyle.Regular);
            fontCollapsedLabel = CreateRoleFont(FontRole.Body, 9f, FontStyle.Regular);
            fontCollapsedText = CreateRoleFont(FontRole.Body, 13f, FontStyle.Regular);
            fontRemindBig = CreateRoleFont(FontRole.Body, 17f, FontStyle.Regular);
            fontRemindSmall = CreateRoleFont(FontRole.Body, 10f, FontStyle.Regular);
            fontRemindTitle = CreateRoleFont(FontRole.Body, 17f, FontStyle.Regular);
            fontRemindMeta = CreateRoleFont(FontRole.Body, 10f, FontStyle.Regular);
            fontRemindTime = CreateRoleFont(FontRole.Timer, 12f, FontStyle.Regular);
            fontAlertBig = CreateRoleFont(FontRole.Timer, 42f, FontStyle.Regular);
            fontAlertCs = CreateRoleFont(FontRole.Timer, 22f, FontStyle.Regular);
            fontAlertMeta = CreateRoleFont(FontRole.Body, 6.5f, FontStyle.Regular);
            fontAlertLabel = CreateRoleFont(FontRole.Body, 8f, FontStyle.Regular);
            fontAlertStrip = CreateRoleFont(FontRole.Timer, 17f, FontStyle.Regular);
            fontAlertStripCs = CreateRoleFont(FontRole.Timer, 10f, FontStyle.Regular);
            fontAlertDone = CreateRoleFont(FontRole.Timer, 26f, FontStyle.Regular);

            menu = BuildMenu();
            ContextMenuStrip = menu;

            tick = new System.Windows.Forms.Timer();
            tick.Interval = 1000;
            tick.Tick += delegate
            {
                if (lastDay != DateTime.Today)
                {
                    lastDay = DateTime.Today;
                    snapshot = null;
                    RequestRefresh(false);
                    RequestCalendar(false);
                }
                Recompute();
                CheckReminders();
            };

            poll = new System.Windows.Forms.Timer();
            poll.Tick += delegate { RequestRefresh(false); };

            autoCollapse = new System.Windows.Forms.Timer();
            autoCollapse.Tick += delegate { autoCollapse.Stop(); MaybeAutoCollapse(); };

            animT = new System.Windows.Forms.Timer();
            animT.Interval = 15;
            animT.Tick += delegate { StepAnim(); };

            calPoll = new System.Windows.Forms.Timer();
            calPoll.Interval = Math.Max(3, settings.calendarPollMinutes) * 60 * 1000;
            calPoll.Tick += delegate { RequestCalendar(false); };

            // 终盘只在这十分钟里跑；读数带百分秒，刷新要快到能看见它在走
            alertT = new System.Windows.Forms.Timer();
            alertT.Interval = 33;
            alertT.Tick += delegate { AlertTick(); };

            remindT = new System.Windows.Forms.Timer();
            remindT.Interval = 16;
            remindT.Tick += delegate { StepReminder(); };

            ApplySettings();
            BackColor = theme.bgBottom;
            collapsed = settings.ui.edge != "free";
            Size = collapsed ? new Size(S(DesignCollapsedW), S(DesignCollapsedH)) : new Size(S(DesignExpandedW), ExpandedH);
            PlaceInitial();
            ApplyRegion();
        }

        /// <summary>把 96 DPI 的设计坐标换算成当前屏幕的物理像素。</summary>
        private int S(float value)
        {
            return (int)Math.Round(value * scale);
        }

        /// <summary>是否显示第二段时间（午休倒计时）。</summary>
        private bool ShowLunchRow
        {
            get { return settings.ui.showLunch && status != null && status.lunch != LunchState.None; }
        }

        private int ExpandedH
        {
            get { return S(ShowLunchRow ? DesignExpandedHLunch : DesignExpandedH); }
        }

        /// <summary>字体角色：计时数字用等宽族，正文用界面族。</summary>
        private enum FontRole
        {
            Timer,
            Body,
        }

        /// <summary>整串判定：整体是时刻（09:57、09:57:00、25:03:00）、时长（9h30m）或时刻区间（15:30–16:00）才算读数；含中文一律归正文（ADR 0005）。</summary>
        private static readonly Regex ReadingPattern = new Regex(
            @"^\d{1,3}(:\d{2}){1,2}$|^\d+h\d+m$|^\d{1,2}:\d{2}\s*[–—~-]\s*\d{1,2}:\d{2}$",
            RegexOptions.Compiled);

        private static bool IsPureReading(string text)
        {
            return !string.IsNullOrEmpty(text) && ReadingPattern.IsMatch(text);
        }

        // 内置字体子集：由 tools/make-font-subsets.py 生成，经 csc /res 内嵌进 exe（ADR 0005）。
        private const string TimerFontResource = "WorkCountdown.fonts.MapleMono-NF-CN-ExtraBold-subset.ttf";
        private const string BodyFontResource = "WorkCountdown.fonts.SarasaMonoSC-SemiBold-subset.ttf";
        private static readonly object fontInitLock = new object();
        private static byte[] timerFontData, bodyFontData;   // AddMemoryFont 之后仍持有数据，防止被回收
        private static string embeddedTimerFamily, embeddedBodyFamily;
        private static bool embeddedFontsTried;

        /// <summary>把内嵌 ttf 读进 PrivateFontCollection，返回集合内的真实族名；失败返回 null（out 持有数据防回收）。</summary>
        private static string TryLoadEmbeddedFont(string resourceName, PrivateFontCollection target, out byte[] data)
        {
            data = null;
            try
            {
                using (System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    byte[] bytes = new byte[stream.Length];
                    int read = 0;
                    while (read < bytes.Length) { int n = stream.Read(bytes, read, (int)bytes.Length - read); if (n <= 0) break; read += n; }
                    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    try { target.AddMemoryFont(handle.AddrOfPinnedObject(), bytes.Length); }
                    finally { handle.Free(); }
                    if (target.Families.Length == 0) return null;
                    data = bytes;
                    return target.Families[target.Families.Length - 1].Name;
                }
            }
            catch { return null; }
        }

        /// <summary>惰性加载内置子集；只跑一次。</summary>
        private static void EnsureEmbeddedFonts()
        {
            lock (fontInitLock)
            {
                if (embeddedFontsTried) return;
                embeddedFontsTried = true;
                var fonts = new PrivateFontCollection();
                embeddedTimerFamily = TryLoadEmbeddedFont(TimerFontResource, fonts, out timerFontData);
                embeddedBodyFamily = TryLoadEmbeddedFont(BodyFontResource, fonts, out bodyFontData);
            }
        }

        /// <summary>角色族三级解析（ADR 0005）：内置子集 → 系统已装同族 → 回退 Consolas / Microsoft YaHei UI。</summary>
        private static string ResolveRoleFamily(FontRole role)
        {
            EnsureEmbeddedFonts();
            string embedded = role == FontRole.Timer ? embeddedTimerFamily : embeddedBodyFamily;
            if (!string.IsNullOrEmpty(embedded)) return embedded;
            // 二级：按关键字找系统已安装的同族（GDI+ 的 Font 构造会静默替换成 Microsoft Sans Serif，
            // 所以这里必须先在 InstalledFontCollection 里确认存在，绝不盲试族名。）
            string contains = role == FontRole.Timer ? "Maple Mono" : "Sarasa Mono";
            try
            {
                var installed = new InstalledFontCollection();
                foreach (var family in installed.Families)
                    if (family.Name.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0) return family.Name;
            }
            catch { }
            // 三级：已知存在的系统族；构造仍包 try/catch，最后兜底 GenericSansSerif。
            return role == FontRole.Timer ? "Consolas" : "Microsoft YaHei UI";
        }

        /// <summary>角色字体唯一入口：按角色解析字体族，Widget 内不直接散落字体名。</summary>
        private static Font CreateRoleFont(FontRole role, float size, FontStyle style)
        {
            string family = ResolveRoleFamily(role);
            try { return new Font(family, size, style, GraphicsUnit.Point); }
            catch { return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point); }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW：不出现在 Alt+Tab
                return cp;
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Recompute();
            RequestRefresh(false);
            RequestCalendar(false);
            tick.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            tick.Stop(); poll.Stop(); autoCollapse.Stop(); animT.Stop(); calPoll.Stop(); remindT.Stop();
            if (alertT != null) alertT.Stop();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                reminderTip.Dispose();
                if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
                foreach (var timer in new System.Windows.Forms.Timer[] { tick, poll, autoCollapse, animT, calPoll, remindT, alertT })
                    if (timer != null) timer.Dispose();
                foreach (var font in new Font[] { fontBig, fontBigText, fontStatus, fontTiny, fontSmall,
                    fontCollapsedNum, fontCollapsedLabel, fontCollapsedText, fontRemindBig, fontRemindSmall,
                    fontRemindTitle, fontRemindMeta, fontRemindTime,
                    fontAlertBig, fontAlertCs, fontAlertMeta, fontAlertLabel, fontAlertStrip,
                    fontAlertStripCs, fontAlertDone }) if (font != null) font.Dispose();
                sfCenter.Dispose(); sfNear.Dispose(); sfFar.Dispose();
                if (menu != null) menu.Dispose();
            }
            base.Dispose(disposing);
        }

        // ---------------- 状态 ----------------

        private void Recompute()
        {
            theme = Themes.Get(settings.ui.theme);
            // 窗口底色必须和卡片一致：圆角抗锯齿边缘会与底色混合，浅灰底会出现一圈白边
            if (BackColor != theme.bgBottom) BackColor = theme.bgBottom;
            status = StatusBuilder.Build(settings, snapshot, DateTime.Now, theme);
            UpdateAlert();
            Invalidate();
        }

        private void RequestRefresh(bool manual)
        {
            if (!settings.lark.enabled)
            {
                snapshot = null;
                Recompute();
                return;
            }
            if (fetching) return;
            fetching = true;
            Invalidate();
            var cfg = settings.lark;
            ThreadPool.QueueUserWorkItem(delegate (object state)
            {
                AttendanceSnapshot result;
                try { result = lark.ReadToday(cfg); }
                catch (Exception ex)
                {
                    result = new AttendanceSnapshot();
                    result.error = ex.Message;
                }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        fetching = false;
                        snapshot = result;
                        lastFetchFailed = !result.ok;
                        if (!result.ok) store.Log("飞书读取失败: " + result.error);
                        Recompute();
                        if (manual) ShowBalloon(result.ok
                            ? "已同步：" + DescribeSnapshot(result)
                            : "同步失败：" + LarkClient.Shorten(result.error, 90));
                    });
                }
                catch { fetching = false; }
            });
        }

        private static string DescribeSnapshot(AttendanceSnapshot s)
        {
            string text;
            if (s.clockOut.HasValue) text = "今日 " + s.clockIn.GetValueOrDefault().ToString("HH:mm") + " → " + s.clockOut.Value.ToString("HH:mm") + " 已下班";
            else if (s.clockIn.HasValue) text = "今日 " + s.clockIn.Value.ToString("HH:mm") + " 上班打卡";
            else text = "今日暂无打卡记录";
            if (!string.IsNullOrEmpty(s.note)) text += "\r\n" + s.note;
            return text;
        }

        // ---------------- 日程提醒 ----------------

        private void RequestCalendar(bool manual)
        {
            if (!settings.calendarEnabled || !settings.lark.enabled)
            {
                larkEvents = new List<ReminderEvent>();
                larkEventError = null;
                return;
            }
            if (calFetching) return;
            calFetching = true;
            var cfg = settings.lark;
            ThreadPool.QueueUserWorkItem(delegate (object state)
            {
                List<ReminderEvent> list = null;
                string error = null;
                try { list = lark.ReadCalendar(cfg, DateTime.Today, DateTime.Today.AddDays(2), out error); }
                catch (Exception ex) { error = ex.Message; }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        calFetching = false;
                        if (list != null) larkEvents = list;
                        string previous = larkEventError;
                        larkEventError = error;
                        if (!string.IsNullOrEmpty(error) && error != previous)
                            store.Log("飞书日程读取失败: " + error);
                        if (manual)
                        {
                            if (error != null) ShowBalloon("日程同步失败：" + LarkClient.Shorten(error, 90));
                            else if (larkEvents.Count == 0) ShowBalloon("飞书日程：这两天没有日程");
                            else ShowBalloon("飞书日程：" + larkEvents.Count + " 个 · " + NextEventText());
                        }
                        Invalidate();
                    });
                }
                catch { calFetching = false; }
            });
        }

        private string NextEventText()
        {
            var all = Reminders.Merge(settings, larkEvents);
            var next = Reminders.Next(settings, all, DateTime.Now);
            if (next == null) return "没有待提醒的日程";
            DateTime start = Reminders.ParseStart(next);
            return start.ToString("HH:mm") + " " + next.title;
        }

        /// <summary>每秒检查一次：到点（开始时间 - 提前量）就放一次提醒动画。</summary>
        private void CheckReminders()
        {
            if (reminding || animating) return;
            var all = Reminders.Merge(settings, larkEvents);
            if (all.Count == 0) return;
            DateTime now = DateTime.Now;
            if (notified.Count > 300) notified.Clear();
            foreach (var e in all)
            {
                DateTime start = Reminders.ParseStart(e);
                string key = (e.id ?? e.title) + "@" + start.Ticks;
                if (notified.Contains(key)) continue;
                if (Reminders.IsDue(settings, e, now))
                {
                    notified.Add(key);
                    StartReminder(e);
                    return;
                }
            }
        }

        private void StartReminder(ReminderEvent e)
        {
            if (animating) { animT.Stop(); animating = false; SetBounds(animToX, Top, animToW, animToH); collapsed = animToW == S(DesignCollapsedW); ApplyRegion(); }
            remindEvent = e;
            AccessibleDescription = e.title + " · " + Reminders.TimeRange(e) + " · " + e.place + " · " + e.detail;
            reminderTip.SetToolTip(this, "最近提醒\r\n" + e.title + "\r\n" + Reminders.TimeRange(e) + " · " + e.place + "\r\n" + e.detail);
            remindAt = DateTime.Now;
            reminding = true;
            autoCollapse.Stop();
            remindT.Interval = (settings.ui.reducedMotion || SystemInformation.HighContrast) ? 100 : 33; remindT.Start();
            Invalidate();
            store.Log("日程提醒: " + e.title + " @" + Reminders.ParseStart(e).ToString("yyyy-MM-dd HH:mm"));
            ShowBalloon("日程提醒 · " + e.title + "\r\n" + Reminders.ParseStart(e).ToString("HH:mm") +
                " 开始，" + Reminders.CountdownText(settings, e, DateTime.Now));
        }

        private void StepReminder()
        {
            double elapsed = (DateTime.Now - remindAt).TotalMilliseconds;
            if (elapsed > RemindInMs + RemindHoldMs + RemindOutMs)
            {
                remindT.Stop();
                reminding = false;
                remindEvent = null;
                ResetAutoCollapse();
                Invalidate();
                return;
            }
            Invalidate();
        }

        private static double EaseOutCubic(double t)
        {
            double u = 1 - Math.Max(0, Math.Min(1, t));
            return 1 - u * u * u;
        }

        private static double EaseInCubic(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * t;
        }

        private static double Luminance(Color c)
        {
            return (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) / 255.0;
        }

        private static Color MixColor(Color a, Color b, double t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)Math.Round(a.R + (b.R - a.R) * t),
                (int)Math.Round(a.G + (b.G - a.G) * t),
                (int)Math.Round(a.B + (b.B - a.B) * t));
        }

        // Text and background have independent timelines. No wipe ever clips a glyph.
        private void PaintReminder(Graphics g)
        {
            if (remindEvent == null) return;
            double t = (DateTime.Now - remindAt).TotalMilliseconds;
            bool quiet = settings.ui.reducedMotion || SystemInformation.HighContrast;
            bool meeting = Reminders.IsMeeting(remindEvent);
            Color a = meeting ? Color.FromArgb(244, 145, 53) : MixColor(theme.accent, Color.Black, 0.50);
            Color b = meeting ? Color.FromArgb(255, 205, 105) : MixColor(theme.accent, Color.Black, 0.68);
            double exit = t - RemindInMs - RemindHoldMs;
            double pa = 1, pb = 1;
            if (!quiet)
            {
                // A completes before B begins. First 140 ms are reserved for old text fade-out.
                pa = EaseOutCubic((t - 140) / 280);
                pb = EaseOutCubic((t - 440) / 320);
                if (exit > 0)
                {
                    // Text fades first, then upper layer B peels off, then A.
                    pb = 1 - EaseInCubic((exit - 180) / 260);
                    pa = 1 - EaseInCubic((exit - 460) / 300);
                }
            }
            int wa = (int)(Width * pa), wb = (int)(Width * pb);
            int seam = collapsed ? S(37) : S(98);
            if (wa > 0) using (var brush = new SolidBrush(a)) g.FillRectangle(brush, 0, 0, wa, Height);
            if (wb > 0) using (var brush = new SolidBrush(b)) g.FillRectangle(brush, 0, seam, wb, Height - seam);
            var saved = g.Save();
            using (var union = new Region(new Rectangle(0,0,Math.Max(0,wa),Height)))
            {
                union.Union(new Rectangle(0,seam,Math.Max(0,wb),Height-seam));
                g.SetClip(union, CombineMode.Intersect);
                PaintAtmosphere(g, meeting ? Color.FromArgb(255, 238, 188) : theme.accent, t / 1000, true);
            }
            g.Restore(saved);
            double alpha = quiet ? 1 : Math.Min(Clamp01((t - 780) / 180), 1 - Clamp01(exit / 150));
            if (alpha > 0)
            {
                Color ink = meeting ? Color.FromArgb(52, 30, 15) : Color.FromArgb(246, 249, 255);
                DrawReminderContent(g, meeting, ink, alpha);
            }
        }

        private static double Clamp01(double v) { return Math.Max(0, Math.Min(1, v)); }

        private void Line(Graphics g, string text, Font font, Color color, RectangleF rect, double alpha, bool far)
        {
            string clean = (text ?? "").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            using (var format = new StringFormat { Alignment = far ? StringAlignment.Far : StringAlignment.Near,
                LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap })
            using (var brush = new SolidBrush(Color.FromArgb((int)(255 * Clamp01(alpha)), color)))
                g.DrawString(clean, font, brush, rect, format);
        }

        private void DrawReminderContent(Graphics g, bool meeting, Color ink, double alpha)
        {
            int pad = S(collapsed ? 14 : 18);
            float w = Width - pad * 2;
            string kind = meeting ? "会议提醒" : "日程提醒";
            string when = Reminders.TimeRange(remindEvent);
            string remaining = Reminders.CountdownText(settings, remindEvent, DateTime.Now);
            if (collapsed)
            {
                Line(g, remindEvent.title, fontCollapsedText, ink, new RectangleF(pad,S(9),w,S(25)),alpha,false);
                Line(g, when + " · " + remaining, fontCollapsedLabel, ink, new RectangleF(pad,S(40),w,S(20)),alpha,false);
                return;
            }
            Line(g, kind, fontStatus, ink, new RectangleF(pad,S(13),w,S(20)),alpha,false);
            Line(g, remindEvent.title, fontRemindTitle, ink, new RectangleF(pad,S(35),w,S(35)),alpha,false);
            Line(g, when, fontRemindTime, ink, new RectangleF(pad,S(104),w,S(24)),alpha,false);
            string place = string.IsNullOrWhiteSpace(remindEvent.place) ? "未设置地点" : remindEvent.place;
            Line(g, place, fontRemindMeta, ink, new RectangleF(pad,S(130),w,S(21)),alpha,false);
            // Content keeps a fixed position; optional rows never cause title jumps.
            if (!string.IsNullOrWhiteSpace(remindEvent.detail))
                Line(g, remindEvent.detail, fontSmall, ink, new RectangleF(pad,S(74),w,S(18)),alpha,false);
            Line(g, remaining, fontStatus, ink, new RectangleF(pad,Height-S(33),w,S(22)),alpha,false);
        }

        // Effects remain in the periphery, behind text. No timers run for idle decoration.
        private void PaintAtmosphere(Graphics g, Color tint, double seconds, bool active)
        {
            string effect = settings.ui.reminderEffect;
            if (effect == "none" || settings.ui.reducedMotion || SystemInformation.HighContrast) return;
            if (!active) seconds = 0;
            if (effect == "glow" || effect == "aurora")
            {
                int count = effect == "aurora" ? 3 : 1;
                for (int i=0; i<count; i++)
                {
                    float x = Width * (0.80f - i * 0.34f) + S(8) * (float)Math.Sin(seconds * 0.65 + i);
                    float y = i % 2 == 0 ? -S(45) : Height - S(12);
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(x-S(70), y, S(160), S(110));
                        using (var glow = new PathGradientBrush(path))
                        {
                            glow.CenterColor = Color.FromArgb(active ? 42 : 23, tint);
                            glow.SurroundColors = new Color[] { Color.FromArgb(0,tint) };
                            g.FillPath(glow,path);
                        }
                    }
                }
            }
            if (effect == "particles" && active)
            {
                for (int i=0; i<12; i++)
                {
                    // Deterministic trajectories: no allocation/random jump between frames.
                    double phase = (i * 0.173 + seconds * (0.035 + i * 0.001)) % 1;
                    float x = i % 2 == 0 ? S(5 + i % 3 * 2) : Width-S(6 + i % 3 * 2);
                    float y = (float)((1-phase) * Height);
                    float r = S(i % 3 == 0 ? 1.6f : 0.9f);
                    using (var brush = new SolidBrush(Color.FromArgb(60+(i%3)*15, Color.White)))
                        g.FillEllipse(brush,x-r,y-r,2*r,2*r);
                }
            }
        }

        /// <summary>按宽度把字号缩到放得下，返回的可能是一个新 Font（用完要释放）。</summary>
        private Font FitFont(Graphics g, string text, Font font, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return font;
            SizeF m = g.MeasureString(text, font);
            if (m.Width <= maxWidth || m.Width <= 0) return font;
            return new Font(font.FontFamily, Math.Max(8f, font.Size * maxWidth / m.Width), font.Style);
        }

        private static string FitWidth(Graphics g, string text, Font font, float maxWidth)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (g.MeasureString(text, font).Width <= maxWidth) return text;
            string cut = text;
            while (cut.Length > 1 && g.MeasureString(cut + "…", font).Width > maxWidth)
                cut = cut.Substring(0, cut.Length - 1);
            return cut + "…";
        }

        private void ShowBalloon(string text)
        {
            try
            {
                if (tray == null) return;
                tray.BalloonTipTitle = "下班倒计时";
                tray.BalloonTipText = text;
                tray.ShowBalloonTip(2500);
            }
            catch { }
        }

        private void ApplySettings()
        {
            Opacity = Math.Max(0.3, Math.Min(1.0, settings.ui.opacity / 100.0));
            TopMost = settings.ui.alwaysOnTop;
            int ms = Math.Max(1, settings.lark.pollMinutes) * 60 * 1000;
            poll.Interval = ms;
            poll.Stop();
            if (settings.lark.enabled) poll.Start();

            if (alertT != null) alertT.Interval = AlertQuiet ? 100 : 33;
            calPoll.Interval = Math.Max(3, settings.calendarPollMinutes) * 60 * 1000;
            calPoll.Stop();
            if (settings.calendarEnabled && settings.lark.enabled) calPoll.Start();
            if (tray != null)
            {
                try
                {
                    Icon old = tray.Icon;
                    tray.Icon = MakeTrayIcon();
                    if (old != null) old.Dispose();
                }
                catch { }
            }
            Recompute();
            if (!collapsed && !animating && Height != ExpandedH)
            {
                Size = new Size(S(DesignExpandedW), ExpandedH);
                ApplyRegion();
            }
        }

        // ---------------- 位置与尺寸 ----------------

        private Screen CurrentScreen()
        {
            return Screen.FromPoint(new Point(Math.Max(0, Left) + S(20), Math.Max(0, Top) + S(20)));
        }

        private void PlaceInitial()
        {
            Screen scr = Screen.PrimaryScreen;
            if (settings.ui.x >= 0 && IsPointVisible(settings.ui.x, settings.ui.y))
            {
                scr = Screen.FromPoint(new Point(settings.ui.x, settings.ui.y));
            }
            int y = settings.ui.y;
            if (y < -5000 || y > 20000) y = S(260);
            int width = collapsed ? S(DesignCollapsedW) : S(DesignExpandedW);
            int x;
            if (settings.ui.edge == "left") x = scr.WorkingArea.Left;
            else if (settings.ui.edge == "free") x = IsPointVisible(settings.ui.x, settings.ui.y) ? settings.ui.x : scr.WorkingArea.Right - width - S(12);
            else x = scr.WorkingArea.Right - width;
            y = Math.Max(scr.WorkingArea.Top, Math.Min(scr.WorkingArea.Bottom - Height, y));
            Location = new Point(x, y);
        }

        private static bool IsPointVisible(int x, int y)
        {
            if (x < 0) return false;
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.WorkingArea.Contains(new Point(x, y + 10))) return true;
            }
            return false;
        }

        private int DockedX(int width)
        {
            Screen scr = CurrentScreen();
            if (settings.ui.edge == "left") return scr.WorkingArea.Left;
            return scr.WorkingArea.Right - width;
        }

        private void ApplyRegion()
        {
            int radius = S(collapsed ? 14 : 18);
            using (var path = RoundPath(new Rectangle(0, 0, Width, Height), radius))
            {
                var old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        private static GraphicsPath RoundPath(Rectangle r, int radius)
        {
            int d = radius * 2;
            if (d <= 1) d = 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
        }

        private void StartAnim(bool collapse)
        {
            if (animating) return;
            animating = true;
            animFromW = Width; animFromH = Height; animFromX = Left;
            animToW = collapse ? S(DesignCollapsedW) : S(DesignExpandedW);
            animToH = collapse ? S(DesignCollapsedH) : ExpandedH;
            animToX = settings.ui.edge == "free" ? Left : DockedX(animToW);
            animStart = Environment.TickCount;
            animT.Start();
        }

        private void StepAnim()
        {
            int elapsed = Environment.TickCount - animStart;
            double t = Math.Min(1.0, Math.Max(0.0, elapsed / (double)AnimMs));
            double e = 1 - Math.Pow(1 - t, 3);
            int w = (int)Math.Round(animFromW + (animToW - animFromW) * e);
            int h = (int)Math.Round(animFromH + (animToH - animFromH) * e);
            int x = (int)Math.Round(animFromX + (animToX - animFromX) * e);
            bool wasCollapsed = collapsed;
            collapsed = animToW == S(DesignCollapsedW);
            if (wasCollapsed != collapsed) ApplyRegion();
            SetBounds(x, Top, Math.Max(1, w), Math.Max(1, h));
            if (t >= 1.0)
            {
                animT.Stop();
                animating = false;
                collapsed = animToW == S(DesignCollapsedW);
                ApplyRegion();
                if (!collapsed) ResetAutoCollapse();
            }
        }

        private void SetCollapsed(bool value)
        {
            if (reminding) return;
            if (value && alertOn) alertDismissed = true;
            if (collapsed == value && !animating) return;
            StartAnim(value);
        }

        private void ResetAutoCollapse()
        {
            autoCollapse.Stop();
            if (collapsed) return;
            int seconds = settings.ui.autoCollapseSeconds;
            if (seconds <= 0) return;
            autoCollapse.Interval = Math.Max(1000, seconds * 1000);
            autoCollapse.Start();
        }

        private void MaybeAutoCollapse()
        {
            if (collapsed || animating || reminding || alertOn) return;
            if (ClientRectangle.Contains(PointToClient(Cursor.Position))) { ResetAutoCollapse(); return; }
            SetCollapsed(true);
        }

        // ---------------- 绘制 ----------------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            // Grayscale text avoids coloured fringes on translucent animated backgrounds.
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            if (status == null) Recompute();
            if (theme == null) theme = Themes.Get(settings.ui.theme);
            var saved = g.Save();
            using (var path = RoundPath(new Rectangle(0,0,Width-1,Height-1),S(collapsed ? 15 : 20)))
            {
                g.SetClip(path,CombineMode.Intersect);
                using (var brush = new LinearGradientBrush(ClientRectangle,theme.bgTop,theme.bgBottom,90f)) g.FillPath(brush,path);
                PaintAtmosphere(g,theme.accent,0,false);
                if (!reminding)
                {
                    if (alertOn || DateTime.Now < alertDoneAt) PaintAlert(g);
                    else if (collapsed) PaintCollapsed(g); else PaintExpanded(g);
                }
                else
                {
                    double elapsed = (DateTime.Now-remindAt).TotalMilliseconds;
                    // Render old content into an isolated layer: whole words fade, never get wiped.
                    double oldAlpha = (settings.ui.reducedMotion || SystemInformation.HighContrast) ? 0 : Math.Max(1-Clamp01(elapsed/130), Clamp01((elapsed-RemindInMs-RemindHoldMs-780)/160));
                    if (oldAlpha > 0)
                    {
                        using (var bitmap = new Bitmap(Width,Height))
                        {
                            bitmap.SetResolution(g.DpiX,g.DpiY);
                            using(var layer = Graphics.FromImage(bitmap))
                            {
                                layer.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
                                layer.SmoothingMode=SmoothingMode.AntiAlias;
                                if(collapsed) PaintCollapsed(layer); else PaintExpanded(layer);
                            }
                            using(var attr = new System.Drawing.Imaging.ImageAttributes())
                            {
                                var matrix = new System.Drawing.Imaging.ColorMatrix(); matrix.Matrix33=(float)oldAlpha;
                                attr.SetColorMatrix(matrix);
                                g.DrawImage(bitmap,ClientRectangle,0,0,Width,Height,GraphicsUnit.Pixel,attr);
                            }
                        }
                    }
                    PaintReminder(g);
                }
                using(var pen = new Pen(Color.FromArgb(24,theme.accent),Math.Max(1,scale))) g.DrawPath(pen,path);
            }
            g.Restore(saved);
        }

        private void PaintExpanded(Graphics g)
        {
            int pad=S(18); float w=Width-pad*2;
            Color muted=MixColor(theme.textMain,theme.bgBottom,0.28);
            Color dim=MixColor(theme.textMain,theme.bgBottom,0.40);
            using(var brush=new SolidBrush(status.accent)) g.FillEllipse(brush,pad,S(21),S(6),S(6));
            Line(g,status.headline,fontStatus,muted,new RectangleF(pad+S(13),S(13),S(140),S(22)),1,false);
            int iconSize=S(24);
            rectGear=new Rectangle(Width-S(90),S(12),iconSize,iconSize);
            rectRefresh=new Rectangle(Width-S(62),S(12),iconSize,iconSize);
            rectChevron=new Rectangle(Width-S(34),S(12),iconSize,iconSize);
            DrawGlyph(g,"gear",rectGear,dim); DrawGlyph(g,"refresh",rectRefresh,fetching?status.accent:dim); DrawGlyph(g,"chevron",rectChevron,dim);
            Line(g,status.bigLabel,fontSmall,muted,new RectangleF(pad,S(46),w,S(20)),1,false);
            Font basis=IsPureReading(status.big)?fontBig:fontBigText;
            Font fit=FitFont(g,status.big,basis,w);
            Line(g,status.big,fit,theme.textMain,new RectangleF(pad-S(2),S(66),w+S(2),S(46)),1,false);
            if(fit!=basis) fit.Dispose();
            DrawProgress(g,pad,S(124),Width-pad*2,S(4));
            Line(g,status.sub,fontSmall,muted,new RectangleF(pad,S(138),w,S(20)),1,false);
            int footer=160;
            if(ShowLunchRow)
            {
                Line(g,(status.lunchLabel??"")+"  "+(status.lunchValue??""),fontSmall,
                    status.lunchCounting?theme.textMain:muted,new RectangleF(pad,S(162),w,S(20)),1,false);
                footer=184;
            }
            Line(g,status.extra,fontTiny,dim,new RectangleF(pad,S(footer),w,S(18)),1,false);
        }

        private void DrawProgress(Graphics g,int x,int y,int width,int height)
        {
            using(var path=RoundPath(new Rectangle(x,y,width,height),height/2))
            using(var brush=new SolidBrush(theme.track)) g.FillPath(brush,path);
            int filled=(int)(width*Math.Max(0,Math.Min(1,status.progress)));
            if(filled>=height)
            using(var path=RoundPath(new Rectangle(x,y,filled,height),height/2))
            using(var brush=new LinearGradientBrush(new Rectangle(x,y,Math.Max(2,filled),height),
                MixColor(status.accent,theme.bgBottom,0.22),status.accent,0f)) g.FillPath(brush,path);
        }

        private void PaintCollapsed(Graphics g)
        {
            int pad=S(14); float w=Width-pad*2;
            Color muted=MixColor(theme.textMain,theme.bgBottom,0.28);
            using(var brush=new SolidBrush(status.accent)) g.FillEllipse(brush,pad,S(15),S(5),S(5));
            Line(g,status.bigLabel,fontCollapsedLabel,muted,new RectangleF(pad+S(10),S(7),S(90),S(20)),1,false);
            string right=string.IsNullOrEmpty(status.targetText)?"":"下班 "+status.targetText;
            if(ShowLunchRow && status.lunchCounting) right="休 "+(status.lunchValue??"").Replace("还剩 ","");
            Line(g,right,fontCollapsedLabel,muted,new RectangleF(Width-pad-S(86),S(7),S(86),S(20)),1,true);
            Line(g,status.big,IsPureReading(status.big)?fontCollapsedNum:fontCollapsedText,theme.textMain,
                new RectangleF(pad-S(1),S(27),w,S(27)),1,false);
            DrawProgress(g,pad,S(59),Width-pad*2,S(3));
            if(lastFetchFailed) Line(g,"!",fontStatus,Color.FromArgb(242,179,76),new RectangleF(Width-pad-S(14),S(32),S(14),S(20)),1,true);
        }

        // ================= 终盘告警态（ADR 0006） =================
        // 目标时刻前十分钟，卡片换一副面孔：黑底 + 滚动斜纹 + 巨号读数。
        // 只换样子，不换大小；读数带百分秒，靠 33ms 的计时器让它看起来在走。

        private Color AlertAccent()
        {
            string pick = settings.ui.alertAccent;
            if (pick == "red") return Color.FromArgb(229, 72, 77);
            if (pick == "amber") return Color.FromArgb(248, 183, 19);
            return theme != null ? theme.accent : Color.FromArgb(76, 141, 255);
        }

        private bool AlertQuiet
        {
            get { return settings.ui.reducedMotion || SystemInformation.HighContrast; }
        }

        /// <summary>进入/离开终盘：只在跨过边界时动一次，之后每次 Recompute 只同步目标时刻。</summary>
        private void UpdateAlert()
        {
            if (alertPreview) { Invalidate(); return; }
            bool wanted = status != null && status.alert && settings.ui.finalAlert != false;
            if (wanted)
            {
                bool entering = !alertOn;
                alertOn = true;
                alertLabel = status.alertLabel;
                alertEn = status.alertEn;
                alertTargetText = status.alertTargetText;
                alertTarget = status.alertTarget;
                alertLunch = status.alertLunch;
                if (entering)
                {
                    alertDismissed = false;
                    alertEnteredAt = Environment.TickCount;
                    alertDoneAt = DateTime.MinValue;
                }
                if (!alertT.Enabled) alertT.Start();
                // 进终盘自动展开一次；用户自己收起过就不再弹（ADR 0006）
                autoCollapse.Stop();
                if (!alertDismissed && collapsed && !animating && !reminding && IsHandleCreated)
                    SetCollapsed(false);
            }
            else if (alertOn)
            {
                alertOn = false;
                // 到点那一刻亮一下达标色，1.5 秒后回普通卡片；不是因为到点而离开（比如手动改了设置）就不闪
                if (alertDoneAt == DateTime.MinValue && DateTime.Now >= alertTarget && !alertLunch)
                    alertDoneAt = DateTime.Now.AddSeconds(1.5);
                if (!alertT.Enabled) alertT.Start();
            }
            Invalidate();
        }

        private void AlertTick()
        {
            if (alertPreview && Environment.TickCount - alertEnteredAt > 90000) { StopAlertPreview(); return; }
            if (!alertOn && DateTime.Now >= alertDoneAt)
            {
                alertT.Stop();
                alertDoneAt = DateTime.MinValue;
            }
            Invalidate();
        }

        /// <summary>右键菜单里的预览：不用等到真到点，也能看清这副面孔。90 秒后自己收。</summary>
        private void StartAlertPreview()
        {
            alertPreview = true;
            alertOn = true;
            alertDismissed = false;
            alertDoneAt = DateTime.MinValue;
            alertEnteredAt = Environment.TickCount;
            alertLunch = false;
            alertLabel = "距离下班";
            alertEn = "SHIFT END";
            alertTarget = DateTime.Now.AddMinutes(StatusBuilder.FinalAlertMinutes);
            alertTargetText = alertTarget.ToString("HH:mm", CultureInfo.InvariantCulture);
            alertT.Start();
            autoCollapse.Stop();
            if (collapsed && !animating && !reminding) SetCollapsed(false);
            Invalidate();
        }

        private void StopAlertPreview()
        {
            alertPreview = false;
            alertOn = false;
            alertDoneAt = DateTime.MinValue;
            alertT.Stop();
            Recompute();
        }

        /// <summary>终盘读数：从目标时刻倒数的百分秒。</summary>
        private string AlertClock(out string hundredths)
        {
            TimeSpan left = alertTarget - DateTime.Now;
            if (left < TimeSpan.Zero) left = TimeSpan.Zero;
            long total = (long)(left.TotalMilliseconds / 10);
            hundredths = "." + (total % 100).ToString("00", CultureInfo.InvariantCulture);
            return (total / 6000 % 100).ToString("00", CultureInfo.InvariantCulture) + ":"
                 + (total / 100 % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        private void PaintAlert(Graphics g)
        {
            Color accent = AlertAccent();
            bool done = DateTime.Now < alertDoneAt;
            using (var bg = new SolidBrush(Color.FromArgb(5, 6, 10)))
                g.FillRectangle(bg, 0, 0, Width, Height);
            if (done) PaintAlertDone(g);
            else if (collapsed) PaintAlertStrip(g, accent);
            else PaintAlertCard(g, accent);
        }

        /// <summary>字体的基线高度（像素），用来把两种字号对齐到同一条基线上。</summary>
        private static float AscentPx(Graphics g, Font f)
        {
            float em = f.FontFamily.GetEmHeight(f.Style);
            if (em <= 0) return f.Height * 0.8f;
            return f.Size * (g.DpiY / 72f) * f.FontFamily.GetCellAscent(f.Style) / em;
        }

        /// <summary>斜纹：45°、条与缝等宽，按 phase 横向滚动；缝透出黑底。</summary>
        private void DrawHazard(Graphics g, RectangleF rect, Color color, float phase, float stripe)
        {
            float sw = stripe;
            if (sw < 2f) sw = 2f;
            float period = sw * 2.8284271f;
            float start = rect.Left - rect.Height + (phase % period);
            if (start > rect.Left) start -= period;
            // 线必须画出带子外再靠 clip 裁：Pen 的平头端帽垂直于线身，停在边上会留下 45° 的三角缺口，
            // 条纹就贴不齐上下沿。两端各甩出 over，交点全在带子外，边缘自然被裁成一条横直线。
            float over = rect.Height + sw;
            var saved = g.Save();
            g.SetClip(rect, CombineMode.Intersect);
            using (var pen = new Pen(color, sw))
                for (float x = start - over; x < rect.Right + rect.Height + period; x += period)
                    g.DrawLine(pen, x, rect.Top - over, x + rect.Height + over * 2, rect.Bottom + over);
            g.Restore(saved);
        }

        private void DrawBrackets(Graphics g, RectangleF r, Color color)
        {
            float len = S(9);
            using (var pen = new Pen(color, Math.Max(1, scale)))
            {
                g.DrawLine(pen, r.Left, r.Top, r.Left + len, r.Top);
                g.DrawLine(pen, r.Left, r.Top, r.Left, r.Top + len);
                g.DrawLine(pen, r.Right, r.Top, r.Right - len, r.Top);
                g.DrawLine(pen, r.Right, r.Top, r.Right, r.Top + len);
                g.DrawLine(pen, r.Left, r.Bottom, r.Left + len, r.Bottom);
                g.DrawLine(pen, r.Left, r.Bottom, r.Left, r.Bottom - len);
                g.DrawLine(pen, r.Right, r.Bottom, r.Right - len, r.Bottom);
                g.DrawLine(pen, r.Right, r.Bottom, r.Right, r.Bottom - len);
            }
        }

        private float TrackedWidth(Graphics g, string text, Font font, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return g.MeasureString(text, font, 4096, StringFormat.GenericTypographic).Width
                 + tracking * Math.Max(0, text.Length - 1);
        }

        /// <summary>GDI+ 没有字距，逐字画。</summary>
        private float DrawTracked(Graphics g, string text, Font font, Color color, float x, float y, float tracking)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            using (var brush = new SolidBrush(color))
            {
                float cx = x;
                foreach (char ch in text)
                {
                    string one = ch.ToString();
                    g.DrawString(one, font, brush, cx, y, StringFormat.GenericTypographic);
                    cx += g.MeasureString(one, font, 4096, StringFormat.GenericTypographic).Width + tracking;
                }
                return cx - x;
            }
        }

        private void DrawScanlines(Graphics g, RectangleF rect)
        {
            using (var pen = new Pen(Color.FromArgb(16, 255, 255, 255), Math.Max(1, scale)))
                for (float y = rect.Top; y < rect.Bottom; y += S(3))
                    g.DrawLine(pen, rect.Left, y, rect.Right, y);
        }

        private void PaintAlertCard(Graphics g, Color accent)
        {
            bool quiet = AlertQuiet;
            int w = Width, h = Height;
            float bandH = S(26);
            float phase = quiet ? 0f : (Environment.TickCount - alertEnteredAt) * 0.012f;

            DrawHazard(g, new RectangleF(0, 0, w, bandH), accent, phase, S(9));
            DrawHazard(g, new RectangleF(0, h - bandH, w, bandH), accent, -phase, S(9));
            using (var pen = new Pen(Color.FromArgb(150, accent), Math.Max(1, scale)))
            {
                g.DrawLine(pen, 0, bandH, w, bandH);
                g.DrawLine(pen, 0, h - bandH, w, h - bandH);
            }

            int pad = S(22);
            // 顶行：英文标签 —— 细线 —— 警告
            float metaY = S(30);
            float w1 = DrawTracked(g, alertEn ?? "SHIFT END", fontAlertMeta, Color.FromArgb(190, 232, 237, 247), pad, metaY, S(1.7f));
            string warn = alertLunch ? "警告 · 午休接近" : "警告 · 终了接近";
            float w2 = TrackedWidth(g, warn, fontAlertMeta, S(1.0f));
            using (var pen = new Pen(Color.FromArgb(45, 255, 255, 255), Math.Max(1, scale)))
                g.DrawLine(pen, pad + w1 + S(8), metaY + S(5), w - pad - w2 - S(8), metaY + S(5));
            DrawTracked(g, warn, fontAlertMeta, accent, w - pad - w2, metaY, S(1.0f));

            // 标签
            string label = alertLabel ?? "距离下班";
            float lw = TrackedWidth(g, label, fontAlertLabel, S(2.6f));
            DrawTracked(g, label, fontAlertLabel, Color.FromArgb(235, 255, 255, 255), (w - lw) / 2f, S(44), S(2.6f));

            // 读数
            string cs;
            string mmss = AlertClock(out cs);
            var sf = StringFormat.GenericTypographic;
            SizeF big = g.MeasureString(mmss, fontAlertBig, 4096, sf);
            SizeF small = g.MeasureString(cs, fontAlertCs, 4096, sf);
            float total = big.Width + small.Width;
            float bx = (w - total) / 2f;
            float by = S(58);
            using (var ink = new SolidBrush(Color.White))
                g.DrawString(mmss, fontAlertBig, ink, bx, by, sf);
            using (var hot = new SolidBrush(accent))
                g.DrawString(cs, fontAlertCs, hot, bx + big.Width + S(1.5f), by + AscentPx(g, fontAlertBig) - AscentPx(g, fontAlertCs), sf);

            // 目标时刻
            string target = "目标 " + (alertTargetText ?? "");
            float tw = TrackedWidth(g, target, fontAlertMeta, S(0.8f));
            DrawTracked(g, target, fontAlertMeta, Color.FromArgb(120, 232, 237, 247), (w - tw) / 2f, S(128), S(0.8f));

            // 进度带：这十分钟走完了多少
            TimeSpan span = alertTarget - DateTime.Now;
            double frac = 1 - span.TotalSeconds / (StatusBuilder.FinalAlertMinutes * 60.0);
            frac = Math.Max(0, Math.Min(1, frac));
            int tx = S(16), ty = S(147), tw2 = w - S(32), th = S(4);
            using (var track = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                g.FillRectangle(track, tx, ty, tw2, th);
            if (frac > 0)
                using (var fill = new SolidBrush(accent))
                    g.FillRectangle(fill, tx, ty, (int)(tw2 * frac), th);
            using (var tick = new Pen(Color.FromArgb(150, 0, 0, 0), Math.Max(1, scale)))
                for (int x = tx; x < tx + tw2; x += S(12))
                    g.DrawLine(tick, x, ty, x, ty + th);

            DrawBrackets(g, new RectangleF(S(7), S(32), w - S(14), h - S(65)), Color.FromArgb(190, accent));
            if (!quiet) DrawScanlines(g, new RectangleF(0, bandH, w, h - bandH * 2));
        }

        /// <summary>收起后的告警横条：还是黑的，上下两条窄斜纹，读数跟着走。</summary>
        private void PaintAlertStrip(Graphics g, Color accent)
        {
            bool quiet = AlertQuiet;
            int w = Width, h = Height;
            float phase = quiet ? 0f : (Environment.TickCount - alertEnteredAt) * 0.012f;
            DrawHazard(g, new RectangleF(0, 0, w, S(6)), accent, phase, S(4));
            DrawHazard(g, new RectangleF(0, h - S(6), w, S(6)), accent, -phase, S(4));

            int pad = S(14);
            using (var dot = new SolidBrush(accent))
                g.FillEllipse(dot, pad, S(14), S(5), S(5));
            DrawTracked(g, alertLabel ?? "距离下班", fontAlertLabel,
                Color.FromArgb(220, 232, 237, 247), pad + S(10), S(11), S(1.4f));
            string right = "目标 " + (alertTargetText ?? "");
            float rw = TrackedWidth(g, right, fontAlertMeta, S(0.8f));
            DrawTracked(g, right, fontAlertMeta, Color.FromArgb(120, 232, 237, 247), w - pad - rw, S(11), S(0.8f));

            string cs;
            string mmss = AlertClock(out cs);
            var sf = StringFormat.GenericTypographic;
            SizeF big = g.MeasureString(mmss, fontAlertStrip, 4096, sf);
            float by = S(27);
            using (var ink = new SolidBrush(Color.White))
                g.DrawString(mmss, fontAlertStrip, ink, pad - S(1), by, sf);
            using (var hot = new SolidBrush(accent))
                g.DrawString(cs, fontAlertStripCs, hot, pad - S(1) + big.Width + S(1f),
                    by + AscentPx(g, fontAlertStrip) - AscentPx(g, fontAlertStripCs), sf);

            TimeSpan span = alertTarget - DateTime.Now;
            double frac = 1 - span.TotalSeconds / (StatusBuilder.FinalAlertMinutes * 60.0);
            frac = Math.Max(0, Math.Min(1, frac));
            int ty = h - S(12);
            using (var track = new SolidBrush(Color.FromArgb(28, 255, 255, 255)))
                g.FillRectangle(track, pad, ty, w - pad * 2, S(3));
            if (frac > 0)
                using (var fill = new SolidBrush(accent))
                    g.FillRectangle(fill, pad, ty, (int)((w - pad * 2) * frac), S(3));
        }

        /// <summary>到点那一瞬：整卡切达标色，然后交回普通卡片。</summary>
        private void PaintAlertDone(Graphics g)
        {
            Color ok = theme != null ? theme.success : Color.FromArgb(70, 211, 154);
            int w = Width, h = Height;
            float bandH = collapsed ? S(5) : S(7);
            DrawHazard(g, new RectangleF(0, 0, w, bandH), ok, 0, S(4));
            DrawHazard(g, new RectangleF(0, h - bandH, w, bandH), ok, 0, S(4));

            string title = "已达标";
            Font titleFont = collapsed ? fontAlertStrip : fontAlertDone;
            float tw = TrackedWidth(g, title, titleFont, S(3f));
            Color ink = Color.FromArgb(235, ok);
            DrawTracked(g, title, titleFont, ink, (w - tw) / 2f, collapsed ? S(22) : S(64), S(3f));

            if (!collapsed)
            {
                string sub = "可以下班了";
                float sw2 = TrackedWidth(g, sub, fontAlertMeta, S(2.2f));
                DrawTracked(g, sub, fontAlertMeta, Color.FromArgb(150, 232, 237, 247), (w - sw2) / 2f, S(120), S(2.2f));
            }
        }

        private void DrawGlyph(Graphics g, string kind, Rectangle r, Color color)
        {
            float f = scale;
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            using (var pen = new Pen(color, 1.5f * f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (kind == "gear")
                {
                    g.DrawEllipse(pen, cx - 4.4f * f, cy - 4.4f * f, 8.8f * f, 8.8f * f);
                    for (int i = 0; i < 6; i++)
                    {
                        double a = i * Math.PI / 3.0;
                        float x1 = cx + (float)Math.Cos(a) * 4.4f * f;
                        float y1 = cy + (float)Math.Sin(a) * 4.4f * f;
                        float x2 = cx + (float)Math.Cos(a) * 6.6f * f;
                        float y2 = cy + (float)Math.Sin(a) * 6.6f * f;
                        g.DrawLine(pen, x1, y1, x2, y2);
                    }
                    using (var b = new SolidBrush(color))
                        g.FillEllipse(b, cx - 1.7f * f, cy - 1.7f * f, 3.4f * f, 3.4f * f);
                }
                else if (kind == "refresh")
                {
                    g.DrawArc(pen, cx - 5f * f, cy - 4.6f * f, 10f * f, 9.2f * f, 35f, 275f);
                    using (var b = new SolidBrush(color))
                    {
                        var pts = new PointF[]
                        {
                            new PointF(cx + 4.3f * f, cy - 6.4f * f),
                            new PointF(cx + 6.6f * f, cy - 1.6f * f),
                            new PointF(cx + 2.0f * f, cy - 2.2f * f)
                        };
                        g.FillPolygon(b, pts);
                    }
                }
                else
                {
                    g.DrawLines(pen, new PointF[]
                    {
                        new PointF(cx - 2.0f * f, cy - 4.2f * f),
                        new PointF(cx + 2.0f * f, cy),
                        new PointF(cx - 2.0f * f, cy + 4.2f * f)
                    });
                }
            }
        }

        // ---------------- 交互 ----------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                moved = false;
                dragOrigin = Cursor.Position;
                formOrigin = Location;
                autoCollapse.Stop();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging && (Control.MouseButtons & MouseButtons.Left) != 0)
            {
                Point cur = Cursor.Position;
                int dx = cur.X - dragOrigin.X;
                int dy = cur.Y - dragOrigin.Y;
                if (!moved && (Math.Abs(dx) > S(3) || Math.Abs(dy) > S(3))) moved = true;
                if (moved)
                {
                    Location = new Point(formOrigin.X + dx, formOrigin.Y + dy);
                    settings.ui.edge = "free";
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            bool wasDragging = moved;
            dragging = false;
            moved = false;

            if (wasDragging)
            {
                SnapToEdge();
                return;
            }

            if (collapsed)
            {
                SetCollapsed(false);
                ResetAutoCollapse();
                return;
            }

            if (reminding || animating) return;
            if (rectGear.Contains(e.Location)) { OpenSettings(); return; }
            if (rectRefresh.Contains(e.Location)) { RequestRefresh(true); return; }
            if (rectChevron.Contains(e.Location)) { SetCollapsed(true); return; }
            ResetAutoCollapse();
        }

        private void SnapToEdge()
        {
            Screen scr = CurrentScreen();
            Rectangle wa = scr.WorkingArea;
            int centerX = Left + Width / 2;
            if (Left + Width > wa.Right - S(20) || centerX > wa.Right - S(80))
            {
                settings.ui.edge = "right";
                Left = wa.Right - Width;
            }
            else if (Left < wa.Left + S(20) || centerX < wa.Left + S(80))
            {
                settings.ui.edge = "left";
                Left = wa.Left;
            }
            else
            {
                settings.ui.edge = "free";
            }
            settings.ui.x = Left;
            settings.ui.y = Top;
            store.Save(settings);
        }

        public void OpenSettingsPublic()
        {
            OpenSettings();
        }

        private void OpenSettings()
        {
            autoCollapse.Stop();
            using (var dlg = new SettingsForm(store, settings))
            {
                dlg.TopMost = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    settings = store.Load();
                    ApplySettings();
                    RequestRefresh(false);
                    RequestCalendar(false);
                    var next = Reminders.Next(settings, Reminders.Merge(settings, larkEvents), DateTime.Now);
                    if (next != null)
                    {
                        DateTime startTime = Reminders.ParseStart(next);
                        DateTime fireAt = startTime.AddMinutes(-Reminders.LeadOf(settings, next));
                        ShowBalloon("已排好：" + next.title + " " + startTime.ToString("HH:mm") + " 开始，" +
                            fireAt.ToString("HH:mm") + " 提醒");
                    }
                }
            }
            ResetAutoCollapse();
        }

        private ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            var miSettings = new ToolStripMenuItem("设置…");
            miSettings.Click += delegate { OpenSettings(); };
            var miRrefresh = new ToolStripMenuItem("立即同步飞书");
            miRrefresh.Click += delegate { RequestRefresh(true); };
            var miCal = new ToolStripMenuItem("刷新飞书日程");
            miCal.Click += delegate { RequestCalendar(true); };
            var miNext = new ToolStripMenuItem("下一个日程");
            miNext.Enabled = false;
            var miPreview = new ToolStripMenuItem("预览提醒动画");
            miPreview.Click += delegate
            {
                var e = new ReminderEvent();
                e.id = "preview";
                e.title = "产品部周会";
                e.start = Reminders.FormatStart(DateTime.Now.AddMinutes(5));
                e.source = "local";
                StartReminder(e);
            };
            var miAlert = new ToolStripMenuItem("预览终盘告警");
            miAlert.Click += delegate
            {
                if (alertPreview) StopAlertPreview(); else StartAlertPreview();
            };
            var miIn = new ToolStripMenuItem("在飞书发「1」上班打卡");
            miIn.Click += delegate { SendClock(true); };
            var miOut = new ToolStripMenuItem("在飞书发「2」下班打卡");
            miOut.Click += delegate { SendClock(false); };
            var miExpand = new ToolStripMenuItem("展开 / 收起");
            miExpand.Click += delegate { SetCollapsed(!collapsed); };
            var miToday = new ToolStripMenuItem("今天设为休息日 / 上班日");
            miToday.Click += delegate { ToggleToday(); };
            var miTop = new ToolStripMenuItem("窗口置顶");
            miTop.CheckOnClick = true;
            miTop.Checked = settings.ui.alwaysOnTop;
            miTop.Click += delegate
            {
                settings.ui.alwaysOnTop = miTop.Checked;
                TopMost = miTop.Checked;
                store.Save(settings);
            };
            var miStart = new ToolStripMenuItem("开机自动启动");
            miStart.CheckOnClick = true;
            miStart.Checked = Startup.IsEnabled();
            miStart.Click += delegate
            {
                Startup.SetEnabled(miStart.Checked);
            };
            var miExport = new ToolStripMenuItem("导出配置…");
            miExport.Click += delegate { ExportSettings(); };
            var miImport = new ToolStripMenuItem("导入配置…");
            miImport.Click += delegate { ImportSettings(); };
            var miExit = new ToolStripMenuItem("退出");
            miExit.Click += delegate { Close(); };

            m.Items.Add(miSettings);
            m.Items.Add(miRrefresh);
            m.Items.Add(miCal);
            m.Items.Add(miNext);
            m.Items.Add(miPreview);
            m.Items.Add(miAlert);
            m.Items.Add(miIn);
            m.Items.Add(miOut);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(miExpand);
            m.Items.Add(miToday);
            m.Items.Add(miTop);
            m.Items.Add(miStart);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(miExport);
            m.Items.Add(miImport);
            m.Items.Add(miExit);
            m.Opening += delegate
            {
                miNext.Text = "下一个日程：" + NextEventText();
                miAlert.Text = alertPreview ? "结束终盘告警预览" : "预览终盘告警";
                miAlert.Enabled = settings.ui.finalAlert != false || alertPreview;
                bool hasChat = settings.lark.enabled && !string.IsNullOrWhiteSpace(settings.lark.chatId);
                miIn.Enabled = hasChat;
                miOut.Enabled = hasChat;
                miCal.Enabled = hasChat && settings.calendarEnabled;
            };
            return m;
        }

        private void ExportSettings()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Title = "导出配置";
                dlg.Filter = "配置文件 (*.json)|*.json";
                dlg.FileName = "下班倒计时配置.json";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (store.Export(settings, dlg.FileName))
                    ShowBalloon("配置已导出\r\n" + dlg.FileName);
                else
                    ShowBalloon("导出失败，详情见 widget.log");
            }
        }

        private void ImportSettings()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "导入配置";
                dlg.Filter = "配置文件 (*.json)|*.json";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var loaded = store.Import(dlg.FileName);
                if (loaded == null)
                {
                    ShowBalloon("导入失败：文件不是有效的配置文件");
                    return;
                }
                settings = loaded;
                store.Save(settings);
                ApplySettings();
                RequestRefresh(false);
                RequestCalendar(false);
                Invalidate();
                ShowBalloon("配置已导入");
            }
        }

        /// <summary>首次使用时的引导，回来之后重新应用设置。</summary>
        public void RunFirstRun()
        {
            autoCollapse.Stop();
            using (var dlg = new FirstRunForm(store, settings))
            {
                dlg.TopMost = true;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    settings = store.Load();
                    ApplySettings();
                    RequestRefresh(false);
                    RequestCalendar(false);
                    Invalidate();
                }
            }
            ResetAutoCollapse();
        }

        private void SendClock(bool clockIn)
        {
            string text = clockIn ? "1" : "2";
            string label = clockIn ? "上班" : "下班";
            if (!settings.lark.enabled || string.IsNullOrWhiteSpace(settings.lark.chatId))
            {
                MessageBox.Show(this, "还没有配置飞书考勤会话，请先在设置里填写会话 ID。", "飞书打卡", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string question = "将以你本人的身份，向「" + settings.lark.botName + "」发送「" + text + "」，记一条" + label + "打卡。\r\n\r\n确定发送吗？";
            if (MessageBox.Show(this, question, "飞书打卡", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            var cfg = settings.lark;
            autoCollapse.Stop();
            ThreadPool.QueueUserWorkItem(delegate (object state)
            {
                string error;
                bool ok = lark.SendText(cfg, text, out error);
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (ok)
                        {
                            ShowBalloon("已发送「" + text + "」· " + label + "打卡");
                            RequestRefresh(false);
                        }
                        else if (error != null && error.IndexOf("scope", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            MessageBox.Show(this,
                                "飞书拒绝以你本人身份发消息：当前授权缺少 im:message.send_as_user 权限。\r\n\r\n" +
                                "在终端执行一次下面这条命令并完成授权即可启用一键打卡：\r\n\r\n" +
                                "lark-cli auth login --scope \"im:message.send_as_user\"",
                                "需要补充飞书授权", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            ShowBalloon("没有发消息权限，已在弹窗中说明授权方法");
                        }
                        else
                        {
                            ShowBalloon("发送失败：" + LarkClient.Shorten(error, 90));
                        }
                        ResetAutoCollapse();
                    });
                }
                catch { }
            });
        }

        private void ToggleToday()
        {
            string key = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            HolidayEntry existing = null;
            foreach (var ex in settings.exceptions)
            {
                if (ex != null && ex.date == key) { existing = ex; break; }
            }
            if (existing != null)
            {
                settings.exceptions.Remove(existing);
            }
            else
            {
                var plan = Policy.Build(settings, DateTime.Today);
                settings.exceptions.Add(new HolidayEntry { date = key, kind = plan.isWorkday ? "off" : "work" });
            }
            store.Save(settings);
            Recompute();
        }

        public void EnableTray()
        {
            try
            {
                tray = new NotifyIcon();
                tray.Icon = MakeTrayIcon();
                tray.Text = "下班倒计时";
                tray.Visible = true;
                tray.ContextMenuStrip = menu;
                tray.DoubleClick += delegate
                {
                    if (!Visible) { Show(); }
                    SetCollapsed(!collapsed);
                };
            }
            catch { }
        }

        private Icon MakeTrayIcon()
        {
            int size = 32;
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    Color accent = theme != null ? theme.accent : Color.FromArgb(76, 141, 255);
                    Color bg = theme != null ? theme.bgBottom : Color.FromArgb(28, 34, 48);
                    Color fg = theme != null ? theme.textMain : Color.FromArgb(232, 237, 247);
                    using (var bgBrush = new SolidBrush(bg))
                        g.FillEllipse(bgBrush, 1, 1, 30, 30);
                    using (var pen = new Pen(accent, 2.4f))
                        g.DrawEllipse(pen, 4, 4, 24, 24);
                    using (var pen = new Pen(fg, 2.2f))
                    {
                        g.DrawLine(pen, 16, 16, 16, 9);
                        g.DrawLine(pen, 16, 16, 22, 19);
                    }
                }
                IntPtr h = bmp.GetHicon();
                return Icon.FromHandle(h);
            }
        }
    }

    public static class Startup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "WorkCountdown";

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch { return false; }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                    else key.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }
    }
}
