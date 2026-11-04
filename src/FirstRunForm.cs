using System;
using System.Drawing;
using System.Windows.Forms;

namespace WorkCountdown
{
    /// <summary>首次启动引导：只问必需的几件事，其余留默认，之后都能在设置里改。</summary>
    public class FirstRunForm : Form
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
        private CheckBox cbLark;
        private TextBox tbBot;
        private TextBox tbChat;
        private Button btnDiscover;
        private Label lblLark;

        private readonly Font titleFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        private readonly Font bodyFont = new Font("Microsoft YaHei UI", 9f);

        public FirstRunForm(SettingsStore store, Settings settings)
        {
            this.store = store;
            this.s = settings;

            Text = "下班倒计时 · 第一次使用";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(470, 470);
            Font = bodyFont;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(245, 246, 250);

            BuildHeader();
            BuildPolicy();
            BuildLunch();
            BuildLark();
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

        private GroupBox Group(string title, int x, int y, int w, int h)
        {
            var g = new GroupBox();
            g.Text = title;
            g.Font = titleFont;
            g.ForeColor = Color.FromArgb(40, 48, 66);
            g.SetBounds(x, y, w, h);
            Controls.Add(g);
            return g;
        }

        private static Label Lbl(string text, int x, int y, int w, int h)
        {
            var l = new Label();
            l.Text = text;
            l.SetBounds(x, y, w, h);
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.ForeColor = Color.FromArgb(70, 78, 96);
            return l;
        }

        private static TextBox Tb(string text, int x, int y, int w)
        {
            var t = new TextBox();
            t.Text = text;
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
            n.SetBounds(x, y, w, 24);
            n.TextAlign = HorizontalAlignment.Center;
            return n;
        }

        private void BuildHeader()
        {
            var title = new Label();
            title.Text = "先把上班时间填一下";
            title.Font = new Font("Microsoft YaHei UI", 12f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(32, 40, 56);
            title.SetBounds(16, 14, 438, 30);
            Controls.Add(title);

            var sub = Lbl("填完点「开始用」，之后右键菜单「设置…」里随时能改。", 16, 46, 438, 20);
            sub.ForeColor = Color.FromArgb(120, 128, 146);
            Controls.Add(sub);
        }

        private void BuildPolicy()
        {
            var g = Group("1 · 上下班时间", 12, 76, 446, 120);

            rbFixed = new RadioButton();
            rbFixed.Text = "固定班次";
            rbFixed.SetBounds(14, 24, 84, 22);
            rbFixed.CheckedChanged += delegate { UpdateModeState(); };
            g.Controls.Add(rbFixed);

            rbFlex = new RadioButton();
            rbFlex.Text = "弹性工时";
            rbFlex.SetBounds(102, 24, 90, 22);
            rbFlex.CheckedChanged += delegate { UpdateModeState(); };
            g.Controls.Add(rbFlex);

            g.Controls.Add(Lbl("上班", 14, 56, 36, 24));
            tbStart = Tb("09:00", 50, 56, 62);
            g.Controls.Add(tbStart);
            g.Controls.Add(Lbl("下班", 122, 56, 36, 24));
            tbEnd = Tb("18:00", 158, 56, 62);
            g.Controls.Add(tbEnd);

            g.Controls.Add(Lbl("每日应勤", 236, 56, 60, 24));
            numFlexH = Num(10, 298, 56, 46, 0, 23);
            g.Controls.Add(numFlexH);
            g.Controls.Add(Lbl("时", 346, 56, 16, 24));
            numFlexM = Num(30, 362, 56, 46, 0, 59);
            g.Controls.Add(numFlexM);
            g.Controls.Add(Lbl("分", 410, 56, 16, 24));

            var hint = Lbl("固定班次按「下班」时间倒计时；弹性工时按上班打卡 + 每日应勤推算。", 14, 88, 420, 22);
            hint.ForeColor = Color.FromArgb(120, 128, 146);
            g.Controls.Add(hint);
        }

        private void BuildLunch()
        {
            var g = Group("2 · 午休", 12, 202, 446, 84);

            cbLunch = new CheckBox();
            cbLunch.Text = "扣除午休";
            cbLunch.SetBounds(14, 24, 84, 22);
            g.Controls.Add(cbLunch);

            tbLunchStart = Tb("11:50", 100, 24, 62);
            g.Controls.Add(tbLunchStart);
            g.Controls.Add(Lbl("至", 168, 24, 20, 24));
            tbLunchEnd = Tb("13:30", 190, 24, 62);
            g.Controls.Add(tbLunchEnd);

            var hint = Lbl("这个时间段也决定卡片上「午休倒计时」的起止。", 14, 54, 420, 20);
            hint.ForeColor = Color.FromArgb(120, 128, 146);
            g.Controls.Add(hint);
        }

        private void BuildLark()
        {
            var g = Group("3 · 飞书打卡（可选，不接也能用）", 12, 292, 446, 120);

            lblLark = Lbl("", 14, 22, 418, 20);
            lblLark.ForeColor = Color.FromArgb(120, 128, 146);
            g.Controls.Add(lblLark);

            cbLark = new CheckBox();
            cbLark.Text = "读飞书里的上下班打卡";
            cbLark.SetBounds(14, 48, 190, 22);
            g.Controls.Add(cbLark);

            g.Controls.Add(Lbl("机器人", 14, 78, 46, 24));
            tbBot = Tb("考勤", 62, 78, 96);
            g.Controls.Add(tbBot);

            btnDiscover = new Button();
            btnDiscover.Text = "自动找";
            btnDiscover.SetBounds(164, 77, 58, 26);
            btnDiscover.Click += delegate { Discover(); };
            g.Controls.Add(btnDiscover);

            g.Controls.Add(Lbl("会话", 226, 78, 34, 24));
            tbChat = new TextBox();
            tbChat.SetBounds(262, 78, 140, 24);
            g.Controls.Add(tbChat);
        }

        private void BuildButtons()
        {
            var ok = new Button();
            ok.Text = "开始用";
            ok.SetBounds(356, 426, 100, 32);
            ok.Click += delegate { Finish(true); };
            Controls.Add(ok);

            var skip = new Button();
            skip.Text = "先跳过";
            skip.SetBounds(248, 426, 96, 32);
            skip.Click += delegate { Finish(false); };
            Controls.Add(skip);

            AcceptButton = ok;
        }

        private void LoadValues()
        {
            rbFixed.Checked = s.mode != "flex";
            rbFlex.Checked = s.mode == "flex";
            tbStart.Text = s.startTime;
            tbEnd.Text = s.endTime;
            numFlexH.Value = Math.Max(0, Math.Min(23, s.flexMinutes / 60));
            numFlexM.Value = Math.Max(0, Math.Min(59, s.flexMinutes % 60));
            cbLunch.Checked = s.lunchDeduct;
            tbLunchStart.Text = s.lunchStart;
            tbLunchEnd.Text = s.lunchEnd;
            tbBot.Text = string.IsNullOrWhiteSpace(s.lark.botName) ? "考勤" : s.lark.botName;
            tbChat.Text = s.lark.chatId;

            string exe = LarkClient.ResolveExecutable(s.lark.command);
            if (exe == null)
            {
                lblLark.ForeColor = Color.FromArgb(200, 120, 40);
                lblLark.Text = "这台机器上没有 lark-cli，先按班次倒计时也能用";
                cbLark.Checked = false;
                cbLark.Enabled = false;
                btnDiscover.Enabled = false;
            }
            else
            {
                lblLark.ForeColor = Color.FromArgb(30, 140, 90);
                lblLark.Text = "已找到 lark-cli，可以试着读打卡";
                cbLark.Checked = s.lark.enabled && !string.IsNullOrWhiteSpace(s.lark.chatId);
            }
        }

        private void UpdateModeState()
        {
            bool flex = rbFlex.Checked;
            if (numFlexH != null) numFlexH.Enabled = flex;
            if (numFlexM != null) numFlexM.Enabled = flex;
            if (tbEnd != null) tbEnd.Enabled = !flex;
        }

        private void Discover()
        {
            btnDiscover.Enabled = false;
            lblLark.ForeColor = Color.FromArgb(70, 78, 96);
            lblLark.Text = "正在列出飞书会话…";
            var cfg = new LarkSettings();
            cfg.command = s.lark.command;
            cfg.botName = tbBot.Text.Trim();
            var client = new LarkClient(store);
            string error;
            string chatId = client.DiscoverChatId(cfg, out error);
            btnDiscover.Enabled = true;
            if (chatId == null)
            {
                lblLark.ForeColor = Color.FromArgb(200, 60, 60);
                lblLark.Text = "没找到：" + LarkClient.Shorten(error, 90);
                return;
            }
            tbChat.Text = chatId;
            cbLark.Checked = true;
            lblLark.ForeColor = Color.FromArgb(30, 140, 90);
            lblLark.Text = "已找到会话：" + chatId;
        }

        private void Finish(bool save)
        {
            if (save)
            {
                s.mode = rbFlex.Checked ? "flex" : "fixed";
                s.startTime = tbStart.Text.Trim();
                s.endTime = tbEnd.Text.Trim();
                s.flexMinutes = (int)numFlexH.Value * 60 + (int)numFlexM.Value;
                s.lunchDeduct = cbLunch.Checked;
                s.lunchStart = tbLunchStart.Text.Trim();
                s.lunchEnd = tbLunchEnd.Text.Trim();
                s.lark.botName = tbBot.Text.Trim();
                s.lark.chatId = tbChat.Text.Trim();
                s.lark.enabled = cbLark.Checked && s.lark.chatId.Length > 0;
                if (s.lark.enabled) s.calendarEnabled = true;
                store.Save(s);
            }
            else
            {
                store.Save(s);   // 先落一份默认配置，下次不再弹
            }
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
