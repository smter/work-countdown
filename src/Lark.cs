using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace WorkCountdown
{
    public class JsonEnvelope
    {
        public bool ok { get; set; }
        public JsonData data { get; set; }
        public JsonError error { get; set; }
    }

    public class JsonData
    {
        public List<JsonMessage> messages { get; set; }
        public List<JsonMessage> items { get; set; }
        public List<JsonChat> chats { get; set; }
    }

    public class JsonError
    {
        public string message { get; set; }
        public string type { get; set; }
        public string subtype { get; set; }
        public int code { get; set; }
    }

    // 日历 +agenda 的返回：data 是一个数组
    public class JsonAgenda
    {
        public bool ok { get; set; }
        public List<JsonAgendaItem> data { get; set; }
        public JsonError error { get; set; }
    }

    public class JsonAgendaItem
    {
        public string event_id { get; set; }
        public string summary { get; set; }
        public string description { get; set; }
        public JsonTime start_time { get; set; }
        public JsonTime end_time { get; set; }
        public JsonLocation location { get; set; }
        public JsonVchat vchat { get; set; }
        public string self_rsvp_status { get; set; }
    }

    public class JsonLocation
    {
        public string name { get; set; }
        public string address { get; set; }
    }

    public class JsonVchat
    {
        public string meeting_url { get; set; }
        public string vc_type { get; set; }
    }

    public class JsonTime
    {
        public string datetime { get; set; }
        public string date { get; set; }
        public string timezone { get; set; }
    }

    public class JsonChat
    {
        public string chat_id { get; set; }
        public string name { get; set; }
        public string p2p_target_type { get; set; }
    }

    public class JsonMessage
    {
        public string create_time { get; set; }
        public string msg_type { get; set; }
        public string content { get; set; }
        public JsonSender sender { get; set; }
    }

    public class JsonSender
    {
        public string sender_type { get; set; }
        public string name { get; set; }
        public string id { get; set; }
    }

    /// <summary>通过本机 lark-cli 读取「考勤大王」的私聊记录，解析当天上下班打卡。</summary>
    public class LarkClient
    {
        private readonly SettingsStore store;

        public LarkClient(SettingsStore store)
        {
            this.store = store;
        }

        public AttendanceSnapshot ReadToday(LarkSettings cfg)
        {
            var snap = new AttendanceSnapshot();
            snap.fetchedAt = DateTime.Now;
            if (cfg == null || !cfg.enabled)
            {
                snap.error = "未启用飞书读取";
                return snap;
            }
            if (string.IsNullOrWhiteSpace(cfg.chatId))
            {
                snap.error = "未设置会话 ID";
                return snap;
            }

            DateTime today = DateTime.Today;
            string args = string.Format(
                "im +chat-messages-list --chat-id {0} --start {1} --end {2} --order asc --page-size 50 --page-all --page-limit 5 --no-reactions --json",
                cfg.chatId,
                today.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                DateTime.Now.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));

            string stderr;
            string stdout = Run(cfg.command, args, 25000, out stderr);
            if (stdout == null)
            {
                snap.error = string.IsNullOrEmpty(stderr) ? "无法执行 lark-cli" : Shorten(stderr, 120);
                return snap;
            }

            JsonEnvelope env;
            try
            {
                var ser = new JavaScriptSerializer();
                env = ser.Deserialize<JsonEnvelope>(stdout);
            }
            catch (Exception ex)
            {
                snap.error = "返回内容无法解析: " + Shorten(ex.Message, 80);
                return snap;
            }

            if (env == null || !env.ok)
            {
                snap.error = env != null && env.error != null && !string.IsNullOrEmpty(env.error.message)
                    ? Shorten(env.error.message, 120)
                    : "飞书接口返回失败";
                return snap;
            }

            var list = new List<JsonMessage>();
            if (env.data != null)
            {
                if (env.data.messages != null) list.AddRange(env.data.messages);
                else if (env.data.items != null) list.AddRange(env.data.items);
            }
            list.Sort(delegate (JsonMessage a, JsonMessage b) { return string.CompareOrdinal(a.create_time, b.create_time); });
            snap.messageCount = list.Count;

            DateTime? firstIn = null;
            DateTime? lastOut = null;
            DateTime? botIn = null;
            DateTime? botOut = null;
            DateTime? botEstEnd = null;
            string note = null;

            var reBotIn = new Regex(@"上班时间\s*\*{0,2}\s*[：:]\s*(\d{1,2})[：:](\d{2})", RegexOptions.Compiled);
            var reBotEst = new Regex(@"预计下班\s*\*{0,2}\s*[：:]\s*(\d{1,2})[：:](\d{2})", RegexOptions.Compiled);
            var reArrow = new Regex(@"(\d{1,2})[：:](\d{2})\s*(?:→|->|~)\s*(\d{1,2})[：:](\d{2})", RegexOptions.Compiled);

            foreach (var m in list)
            {
                if (m == null) continue;
                DateTime when;
                if (!TryParseWhen(m.create_time, out when)) continue;
                if (when.Date != today) continue;

                bool isUser = m.sender != null && m.sender.sender_type == "user";
                string text = m.content ?? "";

                if (isUser)
                {
                    string norm = Normalize(text);
                    if (LooksClockOut(norm))
                    {
                        if (!lastOut.HasValue || when > lastOut.Value) lastOut = when;
                    }
                    else if (LooksClockIn(norm))
                    {
                        if (!firstIn.HasValue || when < firstIn.Value) firstIn = when;
                    }
                    continue;
                }

                var mi = reBotIn.Match(text);
                if (mi.Success)
                {
                    var t = At(today, mi.Groups[1].Value, mi.Groups[2].Value);
                    if (t.HasValue) botIn = t;
                }
                var me = reBotEst.Match(text);
                if (me.Success)
                {
                    var t = At(today, me.Groups[1].Value, me.Groups[2].Value);
                    if (t.HasValue) botEstEnd = t;
                }
                if (text.IndexOf("收工", StringComparison.Ordinal) >= 0)
                {
                    var ma = reArrow.Match(text);
                    if (ma.Success)
                    {
                        var a = At(today, ma.Groups[1].Value, ma.Groups[2].Value);
                        var b = At(today, ma.Groups[3].Value, ma.Groups[4].Value);
                        if (a.HasValue) botIn = a;
                        if (b.HasValue) botOut = b;
                    }
                }
            }

            snap.clockIn = firstIn ?? botIn;
            snap.clockOut = lastOut ?? botOut;
            if (snap.clockOut.HasValue && snap.clockIn.HasValue && snap.clockOut.Value <= snap.clockIn.Value)
                snap.clockOut = null;
            // note 只留机器人自己给出的预计下班：本程序不采信它，但同步气泡里给用户对照
            if (botEstEnd.HasValue) note = "机器人预计 " + botEstEnd.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            snap.note = note;
            snap.ok = true;
            return snap;
        }

        /// <summary>按机器人名称自动发现私聊会话 ID。</summary>
        public string DiscoverChatId(LarkSettings cfg, out string error)
        {
            error = null;
            string stderr;
            string stdout = Run(cfg.command, "im +chat-list --types=p2p --page-all --json", 30000, out stderr);
            if (stdout == null)
            {
                error = string.IsNullOrEmpty(stderr) ? "无法执行 lark-cli" : Shorten(stderr, 120);
                return null;
            }
            try
            {
                var ser = new JavaScriptSerializer();
                var env = ser.Deserialize<JsonEnvelope>(stdout);
                if (env == null || !env.ok || env.data == null || env.data.chats == null)
                {
                    error = "会话列表为空";
                    return null;
                }
                string want = (cfg.botName ?? "").Trim();
                foreach (var c in env.data.chats)
                {
                    if (c == null || string.IsNullOrEmpty(c.chat_id)) continue;
                    if (!string.IsNullOrEmpty(want) && c.name != null && c.name.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0)
                        return c.chat_id;
                }
                foreach (var c in env.data.chats)
                {
                    if (c == null || string.IsNullOrEmpty(c.name)) continue;
                    if (c.name.IndexOf("考勤", StringComparison.Ordinal) >= 0) return c.chat_id;
                }
                error = "没有找到名称包含「" + want + "」的会话";
                return null;
            }
            catch (Exception ex)
            {
                error = "会话列表解析失败: " + Shorten(ex.Message, 80);
                return null;
            }
        }

        /// <summary>以本人身份向考勤会话发送一条文本（例如「1」上班、「2」下班）。</summary>
        public bool SendText(LarkSettings cfg, string text, out string error)
        {
            error = null;
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.chatId))
            {
                error = "未设置会话 ID";
                return false;
            }
            string stderr;
            string stdout = Run(cfg.command,
                "im +messages-send --chat-id " + cfg.chatId + " --as user --text \"" + text.Replace("\"", "'") + "\" --json",
                25000, out stderr);
            if (stdout == null)
            {
                error = string.IsNullOrEmpty(stderr) ? "无法执行 lark-cli" : Shorten(stderr, 120);
                return false;
            }
            try
            {
                var ser = new JavaScriptSerializer();
                var env = ser.Deserialize<JsonEnvelope>(stdout);
                if (env == null || !env.ok)
                {
                    error = env != null && env.error != null && !string.IsNullOrEmpty(env.error.message)
                        ? Shorten(env.error.message, 120) : "飞书发送失败";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "返回内容无法解析: " + Shorten(ex.Message, 80);
                return false;
            }
        }

        /// <summary>读取飞书日历在 [from, to] 内的日程（只取本人已接受/待定的）。</summary>
        /// <summary>飞书返回的时间点（带时区），转成本地时间字符串；没有就返回 null。</summary>
        private static string ParseMoment(JsonTime t)
        {
            if (t == null || string.IsNullOrEmpty(t.datetime)) return null;
            DateTimeOffset dto;
            if (!DateTimeOffset.TryParse(t.datetime, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto)) return null;
            return Reminders.FormatStart(dto.LocalDateTime);
        }

        /// <summary>会议室：优先用日程里的 location，退了会议的给「线上会议」。</summary>
        private static string Place(JsonAgendaItem item)
        {
            if (item.location != null && !string.IsNullOrWhiteSpace(item.location.name))
            {
                string name = item.location.name.Trim();
                if (!string.IsNullOrWhiteSpace(item.location.address) && item.location.address.Trim() != name)
                    name += " · " + item.location.address.Trim();
                return name;
            }
            if (item.vchat != null && !string.IsNullOrWhiteSpace(item.vchat.meeting_url)) return "线上会议";
            return null;
        }

        /// <summary>描述里的 Markdown 换行收成一行，动画里放得下。</summary>
        private static string Plain(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string s = text.Replace("\r", " ").Replace("\n", " ").Trim();
            while (s.IndexOf("  ") >= 0) s = s.Replace("  ", " ");
            return s;
        }

        public List<ReminderEvent> ReadCalendar(LarkSettings cfg, DateTime from, DateTime to, out string error)
        {
            error = null;
            var list = new List<ReminderEvent>();
            if (cfg == null || !cfg.enabled) return list;

            string stderr;
            string stdout = Run(cfg.command,
                "calendar +agenda --start " + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                " --end " + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                " --as user --json",
                30000, out stderr);
            if (stdout == null)
            {
                error = string.IsNullOrEmpty(stderr) ? "无法执行 lark-cli" : Shorten(stderr, 120);
                return list;
            }

            try
            {
                var ser = new JavaScriptSerializer();
                var env = ser.Deserialize<JsonAgenda>(stdout);
                if (env == null || !env.ok)
                {
                    if (env != null && env.error != null)
                    {
                        string detail = env.error.message;
                        if (string.IsNullOrEmpty(detail))
                        {
                            detail = env.error.type;
                            if (!string.IsNullOrEmpty(env.error.subtype)) detail += "/" + env.error.subtype;
                            if (env.error.code != 0) detail += " (" + env.error.code + ")";
                        }
                        error = Shorten(string.IsNullOrEmpty(detail) ? "飞书日程读取失败" : detail, 120);
                    }
                    else error = "飞书日程读取失败";
                    return list;
                }
                if (env.data != null)
                {
                    foreach (var item in env.data)
                    {
                        if (item == null || string.IsNullOrEmpty(item.summary)) continue;
                        if (item.self_rsvp_status == "decline") continue;
                        if (item.start_time == null || string.IsNullOrEmpty(item.start_time.datetime)) continue;
                        DateTimeOffset dto;
                        if (!DateTimeOffset.TryParse(item.start_time.datetime, CultureInfo.InvariantCulture, DateTimeStyles.None, out dto)) continue;
                        var e = new ReminderEvent();
                        long stamp = (long)(dto.UtcDateTime - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                        e.id = "lark:" + (item.event_id ?? item.summary) + "@" + stamp;
                        e.title = item.summary.Trim();
                        e.start = Reminders.FormatStart(dto.LocalDateTime);
                        e.end = ParseMoment(item.end_time);
                        e.place = Place(item);
                        e.detail = Plain(item.description);
                        e.source = "lark";
                        list.Add(e);
                    }
                }
            }
            catch (Exception ex)
            {
                error = "日程返回内容无法解析: " + Shorten(ex.Message, 80);
            }
            return list;
        }

        public string Run(string command, string args, int timeoutMs, out string stderr)
        {
            stderr = null;
            string exe = ResolveExecutable(command);
            if (exe == null)
            {
                stderr = "找不到命令 " + command;
                return null;
            }

            var psi = new ProcessStartInfo();
            string lower = exe.ToLowerInvariant();
            if (lower.EndsWith(".cmd") || lower.EndsWith(".bat"))
            {
                psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
                psi.Arguments = "/s /c \"\"" + exe + "\" " + args + "\"";
            }
            else
            {
                psi.FileName = exe;
                psi.Arguments = args;
            }
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = new UTF8Encoding(false);
            psi.StandardErrorEncoding = new UTF8Encoding(false);

            try
            {
                using (var proc = new Process())
                {
                    proc.StartInfo = psi;
                    proc.Start();
                    string errText = null;
                    var reader = new Thread(delegate ()
                    {
                        try { errText = proc.StandardError.ReadToEnd(); } catch { }
                    });
                    reader.IsBackground = true;
                    reader.Start();

                    string outText = proc.StandardOutput.ReadToEnd();
                    if (!proc.WaitForExit(timeoutMs))
                    {
                        try { proc.Kill(); } catch { }
                        stderr = "lark-cli 响应超时";
                        return null;
                    }
                    reader.Join(800);
                    stderr = errText;
                    return outText;
                }
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                return null;
            }
        }

        public static string ResolveExecutable(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) command = "lark-cli";
            if (command.IndexOf('\\') >= 0 || command.IndexOf('/') >= 0)
                return File.Exists(command) ? command : null;

            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] exts = new string[] { ".cmd", ".exe", ".bat", "" };
            foreach (string rawDir in path.Split(';'))
            {
                string dir = (rawDir ?? "").Trim().Trim('"');
                if (dir.Length == 0) continue;
                foreach (string ext in exts)
                {
                    try
                    {
                        string candidate = Path.Combine(dir, command + ext);
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { }
                }
            }
            return null;
        }

        private static bool TryParseWhen(string text, out DateTime when)
        {
            when = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string[] formats = new string[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss" };
            return DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out when);
        }

        private static DateTime? At(DateTime day, string hh, string mm)
        {
            int h, m;
            if (!int.TryParse(hh, out h) || !int.TryParse(mm, out m)) return null;
            if (h < 0 || h > 23 || m < 0 || m > 59) return null;
            return day.Date.AddHours(h).AddMinutes(m);
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                if (c == '，' || c == '。' || c == '！' || c == '!' || c == '~' || c == '～') continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool LooksClockIn(string norm)
        {
            if (norm.Length == 0) return false;
            if (norm == "1" || norm == "１") return true;
            if (norm == "打卡" || norm == "打卡了" || norm == "上班打卡") return true;
            if (norm.IndexOf("下班", StringComparison.Ordinal) >= 0) return false;
            if (norm.IndexOf("上班", StringComparison.Ordinal) >= 0 && norm.Length <= 12) return true;
            return false;
        }

        private static bool LooksClockOut(string norm)
        {
            if (norm.Length == 0) return false;
            if (norm == "2" || norm == "２") return true;
            if (norm.IndexOf("下班", StringComparison.Ordinal) >= 0 && norm.Length <= 12) return true;
            if (norm == "收工" || norm == "走了") return true;
            return false;
        }

        public static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }
}
