using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace WorkCountdown
{
    public class SettingsForm : Form
    {
        private readonly SettingsStore store;
        private readonly Settings s;

        private RadioButton rbFixed;
        private RadioButton rbFlex;
        private TextBox tbStart;
        private TextBox tbEnd;
        private NumericUpDown numFlexH;
        private NumericUpDown numFlexM;
        private CheckBox cbLunch;
        private TextBox tbLunchStart;
        private TextBox tbLunchEnd;
        private readonly CheckBox[] cbDays = new CheckBox[7];
        private ListBox lstExceptions;
        private DateTimePicker dpDate;
        private ComboBox cmbKind;

        private CheckBox cbLark;
        private TextBox tbChatId;
        private TextBox tbBotName;
        private NumericUpDown numPoll;
        private Button btnDiscover;
        private Button btnTest;
        private Label lblTest;

        private NumericUpDown numOpacity;
        private NumericUpDown numAutoCollapse;
        private ComboBox cmbReminderEffect;
        private CheckBox cbReducedMotion;
        private CheckBox cbFinalAlert;
        private ComboBox cmbAlertAccent;
        private static readonly string[] AlertAccentIds = { "theme", "red", "amber" };
        private NumericUpDown numReminderHold;
        private static readonly string[] ReminderEffectIds = { "none", "glow", "particles", "aurora" };
        private CheckBox cbCalendar;
        private NumericUpDown numLead;
        private NumericUpDown numLeadEvent;

        private TextBox tbEventTitle;
        private TextBox tbEventDetail;
        private TextBox tbEventPlace;
        private DateTimePicker dpEvent;
        private DateTimePicker dpEventEnd;
        private ListBox lstEvents;
        private bool titlePlaceholder = true;
        private const string TitleHint = "标题（必填）";

        private ThemePicker picker;
        private Label lblThemeName;
        private CheckBox cbTopMost;
        private CheckBox cbAutoStart;
        private CheckBox cbShowLunch;

        private TabControl tabs;
        private Control host;

        private static readonly Color PageBack = Color.FromArgb(243, 245, 249);
        private static readonly Color Ink = Color.FromArgb(38, 46, 64);
        private static readonly Color Muted = Color.FromArgb(120, 128, 146);
        private static readonly Color AccentColor = Color.FromArgb(64, 124, 245);

        private readonly Font titleFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        private readonly Font bodyFont = new Font("Microsoft YaHei UI", 9f);

        public SettingsForm(SettingsStore store, Settings settings)
        {
            this.store = store;
            this.s = settings;

            Text = "下班倒计时 · 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(524, 640);
            Font = bodyFont;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = PageBack;

            BuildTabs();
            BuildButtons();

            float dpiScale = 1f;
            using (var screen = Graphics.FromHwnd(IntPtr.Zero)) dpiScale = screen.DpiX / 96f;
            if (dpiScale > 1.01f || dpiScale < 0.99f) ScaleAll(dpiScale);

            LoadValues();
            UpdateModeState();
        }

        private void ScaleAll(float s)
        {
            SuspendLayout();
            foreach (Control c in Controls) ScaleControlTree(c, s);
            ClientSize = new Size(
                (int)Math.Round(ClientSize.Width * s),
                (int)Math.Round(ClientSize.Height * s));
            ResumeLayout(true);
        }

        private static void ScaleControlTree(Control c, float s)
        {
            c.Bounds = new Rectangle(
                (int)Math.Round(c.Left * s),
                (int)Math.Round(c.Top * s),
                (int)Math.Round(c.Width * s),
                (int)Math.Round(c.Height * s));
            foreach (Control child in c.Controls) ScaleControlTree(child, s);
        }

        // ---------------- 外观脚手架 ----------------

        private TabPage Page(string title)
        {
            var page = new TabPage(title);
            page.BackColor = PageBack;
            page.Padding = new Padding(0);
            tabs.TabPages.Add(page);
            return page;
        }

        private void BuildTabs()
        {
            tabs = new TabControl();
            tabs.SetBounds(8, 8, 508, 578);
            tabs.Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular);
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.ItemSize = new Size(112, 34);
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.Padding = new Point(14, 6);
            tabs.DrawItem += delegate (object sender, DrawItemEventArgs e)
            {
                var page = tabs.TabPages[e.Index];
                bool selected = e.Index == tabs.SelectedIndex;
                using (var b = new SolidBrush(selected ? Color.White : PageBack))
                    e.Graphics.FillRectangle(b, e.Bounds);
                if (selected)
                {
                    e.Graphics.FillRectangle(new SolidBrush(AccentColor),
                        new Rectangle(e.Bounds.X + 12, e.Bounds.Bottom - 4, e.Bounds.Width - 24, 3));
                }
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using (var b = new SolidBrush(selected ? Ink : Muted))
                    e.Graphics.DrawString(page.Text, tabs.Font, b, e.Bounds, sf);
            };
            Controls.Add(tabs);

            host = Page("考勤");
            BuildPolicy();
            BuildDays();
            BuildExceptions();

            host = Page("提醒");
            BuildLark();
            BuildSchedule();

            host = Page("外观");
            BuildThemes();
            BuildLook();

            host = null;
        }

        private CardPanel CardBox(string title, int x, int y, int w, int h)
        {
            var g = new CardPanel();
            g.Text = title;
            g.Font = titleFont;
            g.ForeColor = Ink;
            g.SetBounds(x, y, w, h);
            var container = host ?? (Control)this;
            container.Controls.Add(g);
            return g;
        }

        private static Label Lbl(string text, int x, int y, int w, int h)
        {
            var l = new Label();
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.Text = text;
            l.SetBounds(x, y, w, h);
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = Color.FromArgb(70, 78, 96);
            return l;
        }

        private static Label Hint(string text, int x, int y, int w, int h)
        {
            var l = Lbl(text, x, y, w, h);
            l.ForeColor = Muted;
            l.Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
            return l;
        }

        private static TextBox Tb(string text, int x, int y, int w)
        {
            var t = new TextBox();
            t.Text = text;
            t.BorderStyle = BorderStyle.FixedSingle;
            t.SetBounds(x, y, w, 24);
            t.TextAlign = HorizontalAlignment.Center;
            return t;
        }

        private static NumericUpDown Num(int value, int x, int y, int w, int min, int max)
        {
            var n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Value = Math.Max(min, Math.Min(max, value));
            n.BorderStyle = BorderStyle.FixedSingle;
            n.SetBounds(x, y, w, 24);
            n.TextAlign = HorizontalAlignment.Center;
            return n;
        }

        private static Button FlatButton(string text, int x, int y, int w, int h)
        {
            var b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(214, 220, 232);
            b.FlatAppearance.BorderSize = 1;
            b.BackColor = Color.White;
            b.ForeColor = Ink;
            b.SetBounds(x, y, w, h);
            return b;
        }

        // ---------------- 考勤 ----------------

        private void BuildPolicy()
        {
            var g = CardBox("班次与工时", 8, 8, 492, 166);

            rbFixed = new RadioButton();
            rbFixed.Text = "固定班次";
            rbFixed.SetBounds(14, 26, 90, 22);
            rbFixed.CheckedChanged += delegate { UpdateModeState(); };
            g.Controls.Add(rbFixed);

            rbFlex = new RadioButton();
            rbFlex.Text = "弹性工时（按实际上班打卡推算）";
            rbFlex.SetBounds(108, 26, 250, 22);
            rbFlex.CheckedChanged += delegate { UpdateModeState(); };
            g.Controls.Add(rbFlex);

            g.Controls.Add(Lbl("上班", 14, 58, 36, 24));
            tbStart = Tb("09:00", 50, 58, 62);
            g.Controls.Add(tbStart);

            g.Controls.Add(Lbl("下班", 122, 58, 36, 24));
            tbEnd = Tb("18:00", 158, 58, 62);
            g.Controls.Add(tbEnd);

            g.Controls.Add(Lbl("每日应勤", 236, 58, 60, 24));
            numFlexH = Num(10, 298, 58, 46, 0, 23);
            g.Controls.Add(numFlexH);
            g.Controls.Add(Lbl("时", 346, 58, 16, 24));
            numFlexM = Num(30, 362, 58, 46, 0, 59);
            g.Controls.Add(numFlexM);
            g.Controls.Add(Lbl("分", 410, 58, 16, 24));

            cbLunch = new CheckBox();
            cbLunch.Text = "扣除午休";
            cbLunch.SetBounds(14, 94, 84, 22);
            g.Controls.Add(cbLunch);

            tbLunchStart = Tb("11:50", 100, 94, 62);
            g.Controls.Add(tbLunchStart);
            g.Controls.Add(Lbl("至", 168, 94, 20, 24));
            tbLunchEnd = Tb("13:30", 190, 94, 62);
            g.Controls.Add(tbLunchEnd);

            g.Controls.Add(Hint("固定班次：按班次下班时间倒计时；弹性工时：上班打卡时间 + 每日应勤。", 14, 128, 466, 22));
        }

        private void BuildDays()
        {
            var g = CardBox("工作日", 8, 182, 492, 68);
            string[] names = new string[] { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };
            for (int i = 0; i < 7; i++)
            {
                var cb = new CheckBox();
                cb.Text = names[i];
                cb.SetBounds(14 + i * 66, 26, 64, 22);
                cbDays[i] = cb;
                g.Controls.Add(cb);
            }
        }

        private void BuildExceptions()
        {
            var g = CardBox("节假日与调休", 8, 258, 492, 152);

            lstExceptions = new ListBox();
            lstExceptions.SetBounds(14, 26, 286, 92);
            lstExceptions.IntegralHeight = false;
            lstExceptions.BorderStyle = BorderStyle.FixedSingle;
            g.Controls.Add(lstExceptions);

            dpDate = new DateTimePicker();
            dpDate.Format = DateTimePickerFormat.Custom;
            dpDate.CustomFormat = "yyyy-MM-dd";
            dpDate.SetBounds(312, 26, 118, 24);
            g.Controls.Add(dpDate);

            cmbKind = new ComboBox();
            cmbKind.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbKind.Items.Add("休息");
            cmbKind.Items.Add("上班（调休）");
            cmbKind.SelectedIndex = 0;
            cmbKind.SetBounds(312, 56, 118, 24);
            g.Controls.Add(cmbKind);

            var btnAdd = FlatButton("添加", 312, 86, 56, 26);
            btnAdd.Click += delegate { AddException(); };
            g.Controls.Add(btnAdd);

            var btnDel = FlatButton("删除选中", 374, 86, 76, 26);
            btnDel.Click += delegate { RemoveException(); };
            g.Controls.Add(btnDel);

            g.Controls.Add(Hint("用于法定节假日与周末调休；右键悬浮窗也能把今天临时设为休息日。", 14, 124, 466, 20));
        }

        // ---------------- 提醒 ----------------

        private void BuildLark()
        {
            var g = CardBox("飞书考勤对话", 8, 8, 492, 152);

            cbLark = new CheckBox();
            cbLark.Text = "读取飞书考勤对话中的上下班打卡";
            cbLark.SetBounds(14, 26, 300, 22);
            g.Controls.Add(cbLark);

            g.Controls.Add(Lbl("会话 ID", 14, 56, 58, 24));
            tbChatId = Tb("", 76, 56, 292);
            tbChatId.TextAlign = HorizontalAlignment.Left;
            g.Controls.Add(tbChatId);

            btnDiscover = FlatButton("自动发现", 376, 55, 74, 26);
            btnDiscover.Click += delegate { Discover(); };
            g.Controls.Add(btnDiscover);

            g.Controls.Add(Lbl("机器人名", 14, 86, 58, 24));
            tbBotName = Tb("", 76, 86, 150);
            g.Controls.Add(tbBotName);

            g.Controls.Add(Lbl("刷新", 240, 86, 34, 24));
            numPoll = Num(5, 278, 86, 50, 1, 180);
            g.Controls.Add(numPoll);
            g.Controls.Add(Lbl("分钟", 332, 86, 34, 24));

            btnTest = FlatButton("测试连接", 14, 116, 84, 26);
            btnTest.Click += delegate { TestConnection(); };
            g.Controls.Add(btnTest);

            lblTest = Lbl("", 106, 112, 372, 32);
            lblTest.ForeColor = Color.FromArgb(70, 78, 96);
            g.Controls.Add(lblTest);
        }

        private void BuildSchedule()
        {
            var g = CardBox("日程与提醒", 8, 168, 492, 258);

            cbCalendar = new CheckBox();
            cbCalendar.Text = "读取飞书日程";
            cbCalendar.SetBounds(14, 26, 140, 22);
            g.Controls.Add(cbCalendar);

            g.Controls.Add(Lbl("会议提前", 160, 26, 60, 24));
            numLead = Num(5, 222, 26, 44, 0, 120);
            numLead.ValueChanged += delegate { RefreshEvents(); };
            g.Controls.Add(numLead);

            g.Controls.Add(Lbl("日程提前", 284, 26, 60, 24));
            numLeadEvent = Num(10, 346, 26, 44, 0, 120);
            numLeadEvent.ValueChanged += delegate { RefreshEvents(); };
            g.Controls.Add(numLeadEvent);

            g.Controls.Add(Lbl("分钟", 394, 26, 34, 24));

            g.Controls.Add(Hint("左边列表写的是真正会响的时刻；飞书日程算会议，手动加的是日程。", 14, 52, 466, 18));

            lstEvents = new ListBox();
            lstEvents.SetBounds(14, 70, 300, 128);
            lstEvents.IntegralHeight = false;
            lstEvents.BorderStyle = BorderStyle.FixedSingle;
            g.Controls.Add(lstEvents);

            tbEventTitle = new TextBox();
            tbEventTitle.SetBounds(324, 70, 154, 24);
            tbEventTitle.BorderStyle = BorderStyle.FixedSingle;
            tbEventTitle.Text = TitleHint;
            tbEventTitle.ForeColor = Color.FromArgb(150, 154, 164);
            tbEventTitle.GotFocus += delegate
            {
                if (!titlePlaceholder) return;
                titlePlaceholder = false;
                tbEventTitle.Text = "";
                tbEventTitle.ForeColor = Ink;
            };
            tbEventTitle.LostFocus += delegate
            {
                if (tbEventTitle.Text.Trim().Length != 0) return;
                titlePlaceholder = true;
                tbEventTitle.Text = TitleHint;
                tbEventTitle.ForeColor = Color.FromArgb(150, 154, 164);
            };
            g.Controls.Add(tbEventTitle);

            tbEventDetail = new TextBox();
            tbEventDetail.Multiline = true;
            tbEventDetail.ScrollBars = ScrollBars.Vertical;
            tbEventDetail.BorderStyle = BorderStyle.FixedSingle;
            tbEventDetail.SetBounds(324, 96, 154, 40);
            tbEventDetail.ForeColor = Color.FromArgb(150, 154, 164);
            tbEventDetail.GotFocus += delegate { SetDetailPlaceholder(false); };
            tbEventDetail.LostFocus += delegate { SetDetailPlaceholder(true); };
            g.Controls.Add(tbEventDetail);
            SetDetailPlaceholder(true);

            dpEvent = new DateTimePicker();
            dpEvent.Format = DateTimePickerFormat.Custom;
            dpEvent.CustomFormat = "yyyy-MM-dd HH:mm";
            dpEvent.ShowUpDown = true;
            dpEvent.SetBounds(324, 140, 154, 24);
            g.Controls.Add(dpEvent);

            dpEventEnd = new DateTimePicker();
            dpEventEnd.Format = DateTimePickerFormat.Custom;
            dpEventEnd.CustomFormat = "yyyy-MM-dd HH:mm";
            dpEventEnd.ShowUpDown = true;
            dpEventEnd.SetBounds(324, 166, 154, 24);
            g.Controls.Add(dpEventEnd);

            g.Controls.Add(Lbl("会议室", 324, 192, 44, 24));
            tbEventPlace = Tb("", 370, 192, 108);
            tbEventPlace.TextAlign = HorizontalAlignment.Left;
            g.Controls.Add(tbEventPlace);

            var btnAdd = FlatButton("添加日程", 324, 220, 72, 26);
            btnAdd.FlatAppearance.BorderColor = AccentColor;
            btnAdd.ForeColor = AccentColor;
            btnAdd.Click += delegate { AddEvent(); };
            g.Controls.Add(btnAdd);

            var btnDel = FlatButton("删除", 400, 220, 78, 26);
            btnDel.Click += delegate { RemoveEvent(); };
            g.Controls.Add(btnDel);

            g.Controls.Add(Hint("标题必填；内容会显示在提醒动画里。", 14, 204, 300, 40));
        }

        private void SetDetailPlaceholder(bool on)
        {
            if (on)
            {
                if (tbEventDetail.Text.Trim().Length != 0) return;
                tbEventDetail.Text = "内容（可选）";
                tbEventDetail.ForeColor = Color.FromArgb(150, 154, 164);
                tbEventDetail.Tag = "ph";
            }
            else if (tbEventDetail.Tag as string == "ph")
            {
                tbEventDetail.Text = "";
                tbEventDetail.ForeColor = Ink;
                tbEventDetail.Tag = null;
            }
        }

        private void AddEvent()
        {
            string title = titlePlaceholder ? "" : tbEventTitle.Text.Trim();
            if (title.Length == 0)
            {
                MessageBox.Show(this, "给日程起个名字，比如「需求对齐会」。", "日程", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DateTime v = dpEvent.Value;
            DateTime vEnd = dpEventEnd.Value;
            if (vEnd <= v) vEnd = v.AddMinutes(30);

            var e = new ReminderEvent();
            e.id = "local:" + Guid.NewGuid().ToString("N");
            e.title = title;
            e.start = Reminders.FormatStart(new DateTime(v.Year, v.Month, v.Day, v.Hour, v.Minute, 0));
            e.end = Reminders.FormatStart(new DateTime(vEnd.Year, vEnd.Month, vEnd.Day, vEnd.Hour, vEnd.Minute, 0));
            e.place = tbEventPlace.Text.Trim();
            string detail = (tbEventDetail.Tag as string == "ph") ? "" : tbEventDetail.Text.Trim();
            e.detail = detail.Replace("\r", " ").Replace("\n", " ");
            e.source = "local";
            s.events.Add(e);

            titlePlaceholder = true;
            tbEventTitle.Text = TitleHint;
            tbEventTitle.ForeColor = Color.FromArgb(150, 154, 164);
            tbEventDetail.Text = "";
            SetDetailPlaceholder(true);
            tbEventPlace.Text = "";
            dpEvent.Value = DateTime.Now.AddMinutes(30);
            dpEventEnd.Value = DateTime.Now.AddMinutes(60);
            RefreshEvents();
        }

        private void RemoveEvent()
        {
            int index = lstEvents.SelectedIndex;
            if (index < 0) return;
            var sorted = SortedEvents();
            if (index < sorted.Count) s.events.Remove(sorted[index]);
            RefreshEvents();
        }

        private List<ReminderEvent> SortedEvents()
        {
            var sorted = new List<ReminderEvent>(s.events);
            sorted.Sort(delegate (ReminderEvent a, ReminderEvent b) { return Reminders.ParseStart(a).CompareTo(Reminders.ParseStart(b)); });
            return sorted;
        }

        private void RefreshEvents()
        {
            if (lstEvents == null) return;
            lstEvents.BeginUpdate();
            lstEvents.Items.Clear();
            DateTime now = DateTime.Now;
            foreach (var e in SortedEvents())
            {
                DateTime start = Reminders.ParseStart(e);
                if (start == DateTime.MinValue)
                {
                    lstEvents.Items.Add("时间无效   " + e.title);
                    continue;
                }
                DateTime fire = start.AddMinutes(-Reminders.LeadOf(s, e));
                string head = fire.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture) + " 提醒";
                if (start < now) head = "已过";
                string room = string.IsNullOrEmpty(e.place) ? "" : " · " + e.place;
                lstEvents.Items.Add(head + "   " + start.ToString("HH:mm", CultureInfo.InvariantCulture) + " " + e.title + room);
            }
            lstEvents.EndUpdate();
        }

        // ---------------- 外观 ----------------

        private void BuildThemes()
        {
            var g = CardBox("主题配色", 8, 8, 492, 168);
            picker = new ThemePicker();
            picker.SetBounds(14, 30, 456, 104);
            picker.SelectedIdChanged += delegate { UpdateThemeName(); };
            g.Controls.Add(picker);

            lblThemeName = Lbl("", 14, 138, 456, 18);
            lblThemeName.ForeColor = Muted;
            g.Controls.Add(lblThemeName);
        }

        private void UpdateThemeName()
        {
            if (lblThemeName == null || picker == null) return;
            var t = Themes.Get(picker.SelectedId);
            lblThemeName.Text = "当前：" + t.name;
        }

        private void BuildLook()
        {
            var g = CardBox("外观与启动", 8, 184, 492, 212);

            g.Controls.Add(Lbl("不透明度", 14, 28, 58, 24));
            numOpacity = Num(94, 76, 28, 56, 30, 100);
            g.Controls.Add(numOpacity);
            g.Controls.Add(Lbl("%", 136, 28, 20, 24));

            g.Controls.Add(Lbl("自动收起", 180, 28, 58, 24));
            numAutoCollapse = Num(20, 240, 28, 56, 0, 120);
            g.Controls.Add(numAutoCollapse);
            g.Controls.Add(Lbl("秒（0=不自动）", 300, 28, 110, 24));

            cbTopMost = new CheckBox();
            cbTopMost.Text = "窗口置顶";
            cbTopMost.SetBounds(14, 58, 96, 22);
            g.Controls.Add(cbTopMost);

            cbAutoStart = new CheckBox();
            cbAutoStart.Text = "开机自启";
            cbAutoStart.SetBounds(120, 58, 100, 22);
            g.Controls.Add(cbAutoStart);

            cbShowLunch = new CheckBox();
            cbShowLunch.Text = "显示午休倒计时";
            cbShowLunch.SetBounds(230, 58, 150, 22);
            g.Controls.Add(cbShowLunch);

            g.Controls.Add(Lbl("提醒特效", 14, 90, 58, 26));
            cmbReminderEffect = new ComboBox();
            cmbReminderEffect.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbReminderEffect.Items.AddRange(new object[] { "无", "柔光", "粒子", "极光" });
            cmbReminderEffect.SetBounds(76, 90, 140, 26);
            cmbReminderEffect.SelectedIndex = 1;
            g.Controls.Add(cmbReminderEffect);

            g.Controls.Add(Lbl("告警色", 232, 90, 52, 26));
            cmbAlertAccent = new ComboBox();
            cmbAlertAccent.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbAlertAccent.Items.AddRange(new object[] { "跟随主题", "红警", "琥珀" });
            cmbAlertAccent.SetBounds(288, 90, 108, 26);
            cmbAlertAccent.SelectedIndex = 0;
            g.Controls.Add(cmbAlertAccent);
            g.Controls.Add(Hint("无：关闭装饰特效；柔光 / 粒子 / 极光：不同提醒氛围。", 14, 120, 466, 22));

            cbReducedMotion = new CheckBox();
            cbReducedMotion.Text = "减少动态效果";
            cbReducedMotion.SetBounds(14, 146, 142, 22);
            g.Controls.Add(cbReducedMotion);
            g.Controls.Add(Hint("减少移动与闪动，保留提醒内容。", 164, 146, 310, 22));

            g.Controls.Add(Lbl("提醒停留", 14, 178, 58, 24));
            numReminderHold = Num(6, 76, 178, 56, 3, 20);
            g.Controls.Add(numReminderHold);
            g.Controls.Add(Lbl("秒（3–20）", 140, 178, 100, 24));

            cbFinalAlert = new CheckBox();
            cbFinalAlert.Text = "终盘告警（前十分钟）";
            cbFinalAlert.SetBounds(240, 178, 200, 22);
            g.Controls.Add(cbFinalAlert);

            var g2 = CardBox("配置备份", 8, 408, 492, 120);
            g2.Controls.Add(Hint("换电脑或给同事复用：导出一份，带过去再导入。", 14, 28, 466, 18));

            var btnExport = FlatButton("导出配置…", 14, 54, 108, 30);
            btnExport.Click += delegate { ExportConfig(); };
            g2.Controls.Add(btnExport);

            var btnImport = FlatButton("导入配置…", 132, 54, 108, 30);
            btnImport.Click += delegate { ImportConfig(); };
            g2.Controls.Add(btnImport);

            var btnReset = FlatButton("恢复默认", 250, 54, 108, 30);
            btnReset.Click += delegate { ResetDefaults(); };
            g2.Controls.Add(btnReset);

            g2.Controls.Add(Hint("导入后重启悬浮窗完全生效；飞书会话 ID 会一起带过来。", 14, 92, 466, 18));
        }

        private void ExportConfig()
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "配置文件 (*.json)|*.json";
            dlg.FileName = "work-countdown-" + DateTime.Now.ToString("yyyyMMdd") + ".json";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            if (!store.Export(s, dlg.FileName))
            {
                MessageBox.Show(this, "导出失败，详情见 widget.log。", "导出配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show(this, "已导出到：\r\n" + dlg.FileName, "导出配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ImportConfig()
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "配置文件 (*.json)|*.json";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var loaded = store.Import(dlg.FileName);
                s.CopyFrom(loaded);
                store.Save(s);
                LoadValues();
                UpdateModeState();
                MessageBox.Show(this, "已导入。重启悬浮窗后完全生效。", "导入配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导入失败：" + ex.Message, "导入配置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ResetDefaults()
        {
            if (MessageBox.Show(this, "把班次、提醒、外观都恢复成默认？（日程和飞书会话保留）", "恢复默认",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            var def = new Settings();
            def.lark.chatId = s.lark.chatId;
            def.lark.botName = s.lark.botName;
            def.lark.command = s.lark.command;
            def.events = s.events;
            def.exceptions = s.exceptions;
            s.CopyFrom(def);
            LoadValues();
            UpdateModeState();
        }

        private void BuildButtons()
        {
            var cancel = FlatButton("取消", 316, 596, 84, 32);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            var ok = new Button();
            ok.Text = "保存";
            ok.FlatStyle = FlatStyle.Flat;
            ok.FlatAppearance.BorderSize = 0;
            ok.BackColor = AccentColor;
            ok.ForeColor = Color.White;
            ok.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            ok.SetBounds(408, 596, 84, 32);
            ok.Click += delegate { Save(); };
            Controls.Add(ok);

            var tip = Lbl("配置存在 %APPDATA%\\WorkCountdown", 14, 604, 290, 24);
            tip.ForeColor = Color.FromArgb(150, 156, 170);
            Controls.Add(tip);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void LoadValues()
        {
            rbFixed.Checked = s.mode == "fixed";
            rbFlex.Checked = s.mode != "fixed";
            tbStart.Text = s.startTime;
            tbEnd.Text = s.endTime;
            int fm = s.flexMinutes;
            numFlexH.Value = Math.Max(0, Math.Min(23, fm / 60));
            numFlexM.Value = fm % 60;
            cbLunch.Checked = s.lunchDeduct;
            tbLunchStart.Text = s.lunchStart;
            tbLunchEnd.Text = s.lunchEnd;
            for (int i = 0; i < 7; i++) cbDays[i].Checked = s.workdays[i];

            RefreshExceptions();

            cbLark.Checked = s.lark.enabled;
            tbChatId.Text = s.lark.chatId;
            tbBotName.Text = s.lark.botName;
            numPoll.Value = Math.Max(1, Math.Min(180, s.lark.pollMinutes));

            cbCalendar.Checked = s.calendarEnabled;
            numLead.Value = Math.Max(0, Math.Min(120, s.calendarLeadMinutes));
            numLeadEvent.Value = Math.Max(0, Math.Min(120, s.eventLeadMinutes));
            dpEvent.Value = DateTime.Now.AddMinutes(30);
            dpEventEnd.Value = DateTime.Now.AddMinutes(60);
            RefreshEvents();

            numOpacity.Value = Math.Max(30, Math.Min(100, s.ui.opacity));
            numAutoCollapse.Value = Math.Max(0, Math.Min(120, s.ui.autoCollapseSeconds));
            picker.SelectedId = Themes.Get(s.ui.theme).id;
            picker.Invalidate();
            UpdateThemeName();
            cbTopMost.Checked = s.ui.alwaysOnTop;
            cbAutoStart.Checked = Startup.IsEnabled();
            cbShowLunch.Checked = s.ui.showLunch;
            int effectIndex = Array.IndexOf(ReminderEffectIds, s.ui.reminderEffect);
            cmbReminderEffect.SelectedIndex = effectIndex >= 0 ? effectIndex : 1;
            cbReducedMotion.Checked = s.ui.reducedMotion;
            cbFinalAlert.Checked = s.ui.finalAlert != false;
            int alertIndex = Array.IndexOf(AlertAccentIds, s.ui.alertAccent);
            cmbAlertAccent.SelectedIndex = alertIndex >= 0 ? alertIndex : 0;
            numReminderHold.Value = Math.Max(3, Math.Min(20, s.ui.reminderHoldSeconds));
        }

        private void UpdateModeState()
        {
            bool fixedMode = rbFixed.Checked;
            tbStart.Enabled = true;
            tbEnd.Enabled = fixedMode;
            numFlexH.Enabled = !fixedMode;
            numFlexM.Enabled = !fixedMode;
        }

        private void RefreshExceptions()
        {
            lstExceptions.BeginUpdate();
            lstExceptions.Items.Clear();
            var sorted = new List<HolidayEntry>(s.exceptions);
            sorted.Sort(delegate (HolidayEntry a, HolidayEntry b) { return string.CompareOrdinal(a.date, b.date); });
            foreach (var ex in sorted)
            {
                DateTime d;
                string week = DateTime.TryParseExact(ex.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                    ? "  " + Policy.WeekdayName(d.DayOfWeek) : "";
                lstExceptions.Items.Add(ex.date + week + "   " + (ex.kind == "off" ? "休息" : "上班"));
            }
            lstExceptions.EndUpdate();
        }

        private void AddException()
        {
            string key = dpDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string kind = cmbKind.SelectedIndex == 1 ? "work" : "off";
            foreach (var ex in s.exceptions)
            {
                if (ex.date == key) { ex.kind = kind; RefreshExceptions(); return; }
            }
            s.exceptions.Add(new HolidayEntry { date = key, kind = kind });
            RefreshExceptions();
        }

        private void RemoveException()
        {
            int index = lstExceptions.SelectedIndex;
            if (index < 0) return;
            var sorted = new List<HolidayEntry>(s.exceptions);
            sorted.Sort(delegate (HolidayEntry a, HolidayEntry b) { return string.CompareOrdinal(a.date, b.date); });
            if (index < sorted.Count) s.exceptions.Remove(sorted[index]);
            RefreshExceptions();
        }

        private void Discover()
        {
            btnDiscover.Enabled = false;
            lblTest.Text = "正在列出飞书会话…";
            var cfg = new LarkSettings();
            cfg.command = s.lark.command;
            cfg.botName = tbBotName.Text.Trim();
            var client = new LarkClient(store);
            string error;
            string chatId = client.DiscoverChatId(cfg, out error);
            btnDiscover.Enabled = true;
            if (chatId == null)
            {
                lblTest.ForeColor = Color.FromArgb(200, 60, 60);
                lblTest.Text = "未找到：" + error;
                return;
            }
            tbChatId.Text = chatId;
            lblTest.ForeColor = Color.FromArgb(30, 140, 90);
            lblTest.Text = "已找到会话：" + chatId;
        }

        private void TestConnection()
        {
            btnTest.Enabled = false;
            lblTest.ForeColor = Color.FromArgb(70, 78, 96);
            lblTest.Text = "正在读取今天的打卡…";
            var cfg = new LarkSettings();
            cfg.enabled = true;
            cfg.command = s.lark.command;
            cfg.chatId = tbChatId.Text.Trim();
            cfg.botName = tbBotName.Text.Trim();
            var client = new LarkClient(store);
            var snap = client.ReadToday(cfg);
            btnTest.Enabled = true;
            if (!snap.ok)
            {
                lblTest.ForeColor = Color.FromArgb(200, 60, 60);
                lblTest.Text = "读取失败：" + LarkClient.Shorten(snap.error, 120);
                return;
            }
            lblTest.ForeColor = Color.FromArgb(30, 140, 90);
            string text = "读取成功（" + snap.messageCount + " 条消息）：";
            text += snap.clockIn.HasValue ? "上班 " + snap.clockIn.Value.ToString("HH:mm") : "无上班记录";
            text += "，";
            text += snap.clockOut.HasValue ? "下班 " + snap.clockOut.Value.ToString("HH:mm") : "尚未下班";
            lblTest.Text = text;
        }

        private void Save()
        {
            int startMin, endMin, lunchA, lunchB;
            if (!SettingsExtensions.TryParseTime(tbStart.Text, out startMin))
            {
                tabs.SelectedIndex = 0;
                MessageBox.Show(this, "上班时间的格式应为 HH:mm，例如 09:00。", "时间格式有误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!SettingsExtensions.TryParseTime(tbEnd.Text, out endMin))
            {
                tabs.SelectedIndex = 0;
                MessageBox.Show(this, "下班时间的格式应为 HH:mm，例如 18:00。", "时间格式有误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!SettingsExtensions.TryParseTime(tbLunchStart.Text, out lunchA) || !SettingsExtensions.TryParseTime(tbLunchEnd.Text, out lunchB))
            {
                tabs.SelectedIndex = 0;
                MessageBox.Show(this, "午休时间的格式应为 HH:mm。", "时间格式有误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int flex = (int)numFlexH.Value * 60 + (int)numFlexM.Value;
            if (rbFlex.Checked && flex <= 0)
            {
                MessageBox.Show(this, "弹性工时需要大于 0 的每日应勤时长。", "设置不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            s.mode = rbFixed.Checked ? "fixed" : "flex";
            s.startTime = SettingsExtensions.FormatTime(startMin);
            s.endTime = SettingsExtensions.FormatTime(endMin);
            s.flexMinutes = flex;
            s.lunchDeduct = cbLunch.Checked;
            s.lunchStart = SettingsExtensions.FormatTime(lunchA);
            s.lunchEnd = SettingsExtensions.FormatTime(lunchB);
            for (int i = 0; i < 7; i++) s.workdays[i] = cbDays[i].Checked;

            s.lark.enabled = cbLark.Checked;
            s.lark.chatId = tbChatId.Text.Trim();
            s.lark.botName = tbBotName.Text.Trim();
            s.lark.pollMinutes = (int)numPoll.Value;

            s.calendarEnabled = cbCalendar.Checked;
            s.calendarLeadMinutes = (int)numLead.Value;
            s.eventLeadMinutes = (int)numLeadEvent.Value;

            s.ui.opacity = (int)numOpacity.Value;
            s.ui.autoCollapseSeconds = (int)numAutoCollapse.Value;
            s.ui.theme = Themes.Get(picker.SelectedId).id;
            s.ui.alwaysOnTop = cbTopMost.Checked;
            s.ui.showLunch = cbShowLunch.Checked;
            s.ui.reminderEffect = ReminderEffectIds[cmbReminderEffect.SelectedIndex >= 0 ? cmbReminderEffect.SelectedIndex : 1];
            s.ui.reducedMotion = cbReducedMotion.Checked;
            s.ui.finalAlert = cbFinalAlert.Checked;
            s.ui.alertAccent = AlertAccentIds[cmbAlertAccent.SelectedIndex >= 0 ? cmbAlertAccent.SelectedIndex : 0];
            s.ui.reminderHoldSeconds = (int)numReminderHold.Value;
            if (cbAutoStart.Checked != Startup.IsEnabled()) Startup.SetEnabled(cbAutoStart.Checked);

            store.Save(s);
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>圆角白卡片：画个标题和细边，替代原生 GroupBox 的凹陷边框。</summary>
    public class CardPanel : GroupBox
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundRect(rect, 10))
            {
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(226, 231, 240))) g.DrawPath(p, path);
            }
            using (var b = new SolidBrush(Color.FromArgb(38, 46, 64)))
                g.DrawString(Text, Font, b, new PointF(14, 8));
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>主题选择器：一格一个主题，上色是卡片底色、下方一条是强调色。</summary>
    public class ThemePicker : Control
    {
        public string SelectedId;
        public event EventHandler SelectedIdChanged;

        private const int Cols = 10;
        private readonly ToolTip tip = new ToolTip();
        private int hoverIndex = -1;

        public ThemePicker()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            tip.InitialDelay = 250;
        }

        private static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private int ChipW { get { return Math.Max(16, Width / Cols); } }
        private int ChipH { get { return Math.Max(16, Height / Rows); } }
        private int Rows { get { return Math.Max(1, (Themes.All.Length + Cols - 1) / Cols); } }

        private Rectangle ChipRect(int index)
        {
            int cw = ChipW, ch = Math.Min(ChipH, Math.Max(18, Height / Rows));
            int col = index % Cols;
            int row = index / Cols;
            return new Rectangle(col * cw + 3, row * ch + 3, cw - 6, ch - 6);
        }

        private int HitTest(Point p)
        {
            for (int i = 0; i < Themes.All.Length; i++)
                if (ChipRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);
            for (int i = 0; i < Themes.All.Length; i++)
            {
                ThemeDef t = Themes.All[i];
                Rectangle r = ChipRect(i);
                using (var path = RoundRect(r, Math.Max(3, r.Height / 5)))
                {
                    using (var b = new SolidBrush(t.accent)) g.FillPath(b, path);
                    var inner = new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, (int)((r.Height - 4) * 0.56));
                    if (inner.Width > 2 && inner.Height > 2)
                    {
                        using (var ip = RoundRect(inner, Math.Max(2, inner.Height / 4)))
                        using (var b = new LinearGradientBrush(inner, t.bgTop, t.bgBottom, 90f))
                            g.FillPath(b, ip);
                    }
                    bool selected = t.id == SelectedId;
                    if (selected)
                    {
                        using (var p = new Pen(Color.FromArgb(64, 124, 245), 2f))
                            g.DrawPath(p, RoundRect(Rectangle.Inflate(r, 1, 1), Math.Max(3, r.Height / 5)));
                        int badge = Math.Max(10, r.Height / 3);
                        var br = new Rectangle(r.Right - badge - 3, r.Bottom - badge - 3, badge, badge);
                        using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, br);
                        using (var p = new Pen(Color.FromArgb(64, 124, 245), Math.Max(1.6f, badge / 6f)))
                        {
                            p.StartCap = LineCap.Round; p.EndCap = LineCap.Round;
                            g.DrawLines(p, new PointF[] {
                                new PointF(br.X + br.Width * 0.28f, br.Y + br.Height * 0.52f),
                                new PointF(br.X + br.Width * 0.45f, br.Y + br.Height * 0.70f),
                                new PointF(br.X + br.Width * 0.74f, br.Y + br.Height * 0.32f) });
                        }
                    }
                    else if (i == hoverIndex)
                    {
                        using (var p = new Pen(Color.FromArgb(120, 130, 150), 1f))
                            g.DrawPath(p, RoundRect(r, Math.Max(3, r.Height / 5)));
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = HitTest(e.Location);
            if (index != hoverIndex)
            {
                hoverIndex = index;
                if (index >= 0)
                {
                    tip.SetToolTip(this, Themes.All[index].name);
                    Invalidate();
                }
                else Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverIndex != -1) { hoverIndex = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int index = HitTest(e.Location);
            if (index < 0) return;
            SelectedId = Themes.All[index].id;
            Invalidate();
            if (SelectedIdChanged != null) SelectedIdChanged(this, EventArgs.Empty);
        }
    }
}
