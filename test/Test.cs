using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using WorkCountdown;

internal static class Test
{
    private static int failures = 0;

    private static void Check(string name, string actual, string expected)
    {
        bool ok = actual == expected;
        if (!ok) failures++;
        Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + "  => " + actual + (ok ? "" : "   (expected " + expected + ")"));
    }

    private static void Main()
    {
        var s = new Settings();
        s.mode = "flex";
        s.flexMinutes = 630;
        s.startTime = "09:00";
        s.lunchDeduct = false;
        s.lark.enabled = true;              // 这些用例都假设接了飞书，有打卡记录
        s.lark.chatId = "oc_test";

        DayOfWeek dow = new DateTime(2026, 11, 3).DayOfWeek;
        Console.WriteLine("2026-11-03 is " + dow + " / " + Policy.WeekdayName(dow));

        // 1. 弹性工时，09:27 打卡，10:00 时
        var snap = new AttendanceSnapshot();
        snap.ok = true;
        snap.clockIn = new DateTime(2026, 11, 3, 9, 27, 0);
        var st = StatusBuilder.Build(s, snap, new DateTime(2026, 11, 3, 10, 0, 0), Themes.Get("midnight"));
        Check("10:00 状态", st.state.ToString(), "Working");
        Check("10:00 倒计时", st.big, "09:57:00");
        Check("10:00 预计下班", st.sub, "上班 09:27 · 预计下班 19:57");

        // 2. 刚到点
        st = StatusBuilder.Build(s, snap, new DateTime(2026, 11, 3, 19, 57, 0), Themes.Get("midnight"));
        Check("19:57 状态", st.state.ToString(), "Overtime");
        Check("19:57 倒计时", st.big, "00:00:00");

        // 3. 已下班 20:09
        snap.clockOut = new DateTime(2026, 11, 3, 20, 9, 0);
        st = StatusBuilder.Build(s, snap, new DateTime(2026, 11, 3, 20, 20, 0), Themes.Get("midnight"));
        Check("20:20 状态", st.state.ToString(), "Done");
        Check("20:20 工时", st.big, "10h42m");
        Check("20:20 区间", st.sub, "09:27 → 20:09");

        // 4. 没打卡且已过上班时间
        st = StatusBuilder.Build(s, null, new DateTime(2026, 11, 3, 9, 40, 0), Themes.Get("midnight"));
        Check("9:40 未打卡", st.state.ToString(), "NotClockedIn");
        Check("9:40 已过", st.big, "00:40:00");

        // 5. 早于上班时间
        st = StatusBuilder.Build(s, null, new DateTime(2026, 11, 3, 8, 30, 0), Themes.Get("midnight"));
        Check("8:30 状态", st.state.ToString(), "BeforeWork");
        Check("8:30 距上班", st.big, "00:30:00");

        // 6. 周六休息
        st = StatusBuilder.Build(s, null, new DateTime(2026, 11, 7, 10, 0, 0), Themes.Get("midnight"));
        Check("周六", st.state.ToString(), "Rest");
        Check("周六文案", st.big, "今天休息");

        // 7. 调休上班（周六设为上班）
        s.exceptions.Add(new HolidayEntry { date = "2026-11-07", kind = "work" });
        var plan = Policy.Build(s, new DateTime(2026, 11, 7));
        Check("周六调休", plan.isWorkday.ToString(), "True");
        s.exceptions.Clear();

        // 8. 工作日设为休息
        s.exceptions.Add(new HolidayEntry { date = "2026-11-04", kind = "off" });
        plan = Policy.Build(s, new DateTime(2026, 11, 4));
        Check("周三设为休息", plan.isWorkday.ToString(), "False");
        s.exceptions.Clear();

        // 9. 固定班次 09:00-18:00
        s.mode = "fixed";
        s.endTime = "18:00";
        var snap2 = new AttendanceSnapshot();
        snap2.ok = true;
        snap2.clockIn = new DateTime(2026, 11, 3, 8, 55, 0);
        st = StatusBuilder.Build(s, snap2, new DateTime(2026, 11, 3, 10, 0, 0), Themes.Get("midnight"));
        Check("固定班次 10:00", st.big, "08:00:00");
        Check("固定班次 预计下班", st.sub, "上班 08:55 · 预计下班 18:00");

        // 10. 固定班次 + 扣除午休（午休 12:00-13:00 落在班次内）
        s.lunchDeduct = true;
        s.lunchStart = "12:00";
        s.lunchEnd = "13:00";
        s.endTime = "18:00";
        st = StatusBuilder.Build(s, snap2, new DateTime(2026, 11, 3, 10, 0, 0), Themes.Get("midnight"));
        Check("固定班次扣午休", st.sub, "上班 08:55 · 预计下班 19:00");

        // 11. 弹性工时扣午休（打卡 09:00，应勤 9 小时，午休在区间内）
        s.mode = "flex";
        s.lunchDeduct = true;
        s.flexMinutes = 540;
        var snap3 = new AttendanceSnapshot();
        snap3.ok = true;
        snap3.clockIn = new DateTime(2026, 11, 3, 9, 0, 0);
        var p3 = Policy.Build(s, new DateTime(2026, 11, 3));
        Check("弹性扣午休下班", p3.TargetEnd(new DateTime(2026, 11, 3, 9, 0, 0)).ToString("HH:mm"), "19:00");

        // 12. 时间解析
        int m;
        Check("解析 9:05", SettingsExtensions.TryParseTime("9:05", out m) + "/" + m, "True/545");
        Check("解析 09：30", SettingsExtensions.TryParseTime("09：30", out m) + "/" + m, "True/570");
        Check("非法 25:00", SettingsExtensions.TryParseTime("25:00", out m).ToString(), "False");
        Check("格式化 545", SettingsExtensions.FormatTime(545), "09:05");

        // 13. 时长与时钟格式
        Check("时钟 25 小时", Policy.Clock(TimeSpan.FromHours(25) + TimeSpan.FromMinutes(3)), "25:03:00");
        Check("紧凑 10h38m", Policy.CompactDuration(TimeSpan.FromMinutes(638)), "10h38m");

        // 14. 午休倒计时（第二段时间）
        var theme = Themes.Get("midnight");
        s.lunchStart = "11:50";
        s.lunchEnd = "13:30";
        var snapL = new AttendanceSnapshot();
        snapL.ok = true;
        snapL.clockIn = new DateTime(2026, 11, 3, 9, 27, 0);
        var stL = StatusBuilder.Build(s, snapL, new DateTime(2026, 11, 3, 10, 15, 0), theme);
        Check("午休前", stL.lunchLabel + " " + stL.lunchValue, "距午休 1h35m");
        Check("午休前在倒数", stL.lunchCounting.ToString(), "True");
        stL = StatusBuilder.Build(s, snapL, new DateTime(2026, 11, 3, 12, 0, 0), theme);
        Check("午休中", stL.lunchLabel + " " + stL.lunchValue, "午休中 还剩 1h30m");
        stL = StatusBuilder.Build(s, snapL, new DateTime(2026, 11, 3, 13, 30, 0), theme);
        Check("午休结束", stL.lunch.ToString(), "After");
        stL = StatusBuilder.Build(s, snapL, new DateTime(2026, 11, 3, 14, 0, 0), theme);
        Check("午休结束后不再倒数", stL.lunchCounting.ToString(), "False");
        s.lunchStart = "25:00";
        stL = StatusBuilder.Build(s, snapL, new DateTime(2026, 11, 3, 10, 15, 0), theme);
        Check("午休时间非法则不显示", stL.lunch.ToString(), "None");

        // 15. 主题表
        Check("主题数量", Themes.All.Length.ToString(), "39");
        var ids = new HashSet<string>();
        int dup = 0;
        foreach (var th in Themes.All) if (!ids.Add(th.id)) dup++;
        Check("主题 id 不重复", dup.ToString(), "0");
        Check("按 id 取主题", Themes.Get("hong-fengye").name, "红 · 枫叶");
        Check("未知 id 回退默认", Themes.Get("nope").id, "midnight");

        int lowContrast = 0;
        foreach (var th in Themes.All)
        {
            double la = (0.2126 * th.accent.R + 0.7152 * th.accent.G + 0.0722 * th.accent.B) / 255.0;
            double lb = (0.2126 * th.bgBottom.R + 0.7152 * th.bgBottom.G + 0.0722 * th.bgBottom.B) / 255.0;
            if (Math.Abs(la - lb) < 0.12) lowContrast++;
        }
        Check("主色与底色对比足够", lowContrast.ToString(), "0");

        // 16. 日程提醒
        var s16 = new Settings();
        s16.Normalize();
        s16.calendarLeadMinutes = 5;
        s16.eventLeadMinutes = 5;
        var ev = new ReminderEvent();
        ev.id = "local:1";
        ev.title = "产品部周会";
        ev.start = "2026-11-04 10:05";
        ev.source = "local";
        Check("解析日程时间", Reminders.ParseStart(ev).ToString("HH:mm"), "10:05");
        Check("提前量到点触发", Reminders.IsDue(s16, ev, new DateTime(2026, 11, 4, 10, 0, 0)).ToString(), "True");
        Check("没到提醒时间", Reminders.IsDue(s16, ev, new DateTime(2026, 11, 4, 9, 59, 0)).ToString(), "False");
        Check("开始后 1 分钟内仍提醒", Reminders.IsDue(s16, ev, new DateTime(2026, 11, 4, 10, 5, 30)).ToString(), "True");
        Check("开始 1 分钟后过期", Reminders.IsDue(s16, ev, new DateTime(2026, 11, 4, 10, 7, 0)).ToString(), "False");
        Check("倒计时说法", Reminders.CountdownText(s16, ev, new DateTime(2026, 11, 4, 10, 0, 0)), "还有 5 分钟");
        Check("倒计时-马上开始", Reminders.CountdownText(s16, ev, new DateTime(2026, 11, 4, 10, 4, 40)), "马上开始");
        s16.events.Add(ev);
        Check("手动日程进入待提醒", Reminders.Merge(s16, null).Count.ToString(), "1");
        Check("下一个日程", Reminders.Next(s16, Reminders.Merge(s16, null), new DateTime(2026, 11, 4, 10, 0, 0)).title, "产品部周会");
        ev.leadMinutes = 30;
        Check("单条日程可覆盖提前量", Reminders.IsDue(s16, ev, new DateTime(2026, 11, 4, 9, 40, 0)).ToString(), "True");
        ev.leadMinutes = 0;
        Check("提前量为 0 也有窗口", Reminders.IsDue(s16, ev, new DateTime(2026, 11, 4, 10, 5, 10)).ToString(), "True");

        var larkList = new List<ReminderEvent>();
        var le = new ReminderEvent();
        le.id = "lark:1";
        le.title = "站会";
        le.start = "2026-11-04 10:30";
        le.source = "lark";
        larkList.Add(le);
        Check("飞书日程并入待提醒", Reminders.Merge(s16, larkList).Count.ToString(), "2");
        s16.calendarEnabled = false;
        Check("关掉飞书日程后只剩手动", Reminders.Merge(s16, larkList).Count.ToString(), "1");

        // 17. 完全没接飞书的人：按班次推算，不能显示「还没打卡」
        var s17 = new Settings();
        Check("默认就是固定班次", s17.mode, "fixed");
        Check("默认不接飞书", s17.lark.enabled.ToString(), "False");
        s17.startTime = "09:00";
        s17.endTime = "18:00";
        var st17 = StatusBuilder.Build(s17, null, new DateTime(2026, 11, 3, 10, 0, 0), Themes.Get("midnight"));
        Check("没接飞书按班次推算", st17.state.ToString(), "Working");
        Check("没接飞书 倒计时", st17.big, "08:00:00");
        Check("没接飞书 班次", st17.sub, "上班 09:00 · 预计下班 18:00");
        var st18 = StatusBuilder.Build(s17, null, new DateTime(2026, 11, 3, 8, 30, 0), Themes.Get("midnight"));
        Check("没接飞书 上班前", st18.state.ToString(), "BeforeWork");
        Check("没接飞书 距上班", st18.big, "00:30:00");

        // 18. 会议与单个日程的提前量各自独立；日程带结束时间 / 会议室 / 内容
        var s18 = new Settings();
        s18.calendarLeadMinutes = 5;
        s18.eventLeadMinutes = 12;
        var meet = new ReminderEvent();
        meet.id = "lark:m";
        meet.title = "产品部周会";
        meet.start = "2026-11-05 15:30";
        meet.end = "2026-11-05 16:00";
        meet.place = "会议室 A · 12 层";
        meet.source = "lark";
        Check("会议用会议提前量", Reminders.LeadOf(s18, meet).ToString(), "5");
        Check("会议到点触发", Reminders.IsDue(s18, meet, new DateTime(2026, 11, 5, 15, 25, 0)).ToString(), "True");
        Check("会议早 6 分钟不触发", Reminders.IsDue(s18, meet, new DateTime(2026, 11, 5, 15, 24, 0)).ToString(), "False");

        var local = new ReminderEvent();
        local.id = "local:m";
        local.title = "需求对齐会";
        local.start = "2026-11-04 14:30";
        local.end = "2026-11-04 15:00";
        local.place = "会议室 A";
        local.detail = "过一遍本周排期";
        local.source = "local";
        Check("手动日程用日程提前量", Reminders.LeadOf(s18, local).ToString(), "12");
        Check("手动日程到点触发", Reminders.IsDue(s18, local, new DateTime(2026, 11, 4, 14, 18, 0)).ToString(), "True");
        Check("手动日程早 1 分钟不触发", Reminders.IsDue(s18, local, new DateTime(2026, 11, 4, 14, 17, 0)).ToString(), "False");
        Check("时间区间", Reminders.TimeRange(local), "14:30–15:00");
        Check("只有开始时间时区间取开始", Reminders.TimeRange(le), "10:30");
        Check("结束时间解析", Reminders.ParseEnd(local).ToString("HH:mm"), "15:00");
        local.leadMinutes = 0;
        Check("0 表示跟随默认", Reminders.LeadOf(s18, local).ToString(), "12");
        local.leadMinutes = 3;
        Check("单条覆盖优先于类型默认", Reminders.LeadOf(s18, local).ToString(), "3");
        Check("会议室算是会议", Reminders.IsMeeting(meet).ToString(), "True");
        Check("手动日程不算会议", Reminders.IsMeeting(local).ToString(), "False");

        // 19. 提醒外观：默认值、旧配置兼容、归一化与 JSON 往返。
        var prefs = new Settings();
        Check("默认提醒特效", prefs.ui.reminderEffect, "glow");
        Check("默认减少动态", prefs.ui.reducedMotion.ToString(), "False");
        Check("默认提醒停留", prefs.ui.reminderHoldSeconds.ToString(), "6");
        var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
        var oldPrefs = serializer.Deserialize<Settings>("{\"ui\":{\"theme\":\"midnight\"}}");
        oldPrefs.Normalize();
        Check("旧 JSON 特效默认", oldPrefs.ui.reminderEffect, "glow");
        Check("旧 JSON 减少动态默认", oldPrefs.ui.reducedMotion.ToString(), "False");
        Check("旧 JSON 停留默认", oldPrefs.ui.reminderHoldSeconds.ToString(), "6");
        prefs.ui.reminderEffect = "unknown";
        prefs.Normalize();
        Check("未知特效回退", prefs.ui.reminderEffect, "glow");
        prefs.ui.reminderEffect = null;
        prefs.Normalize();
        Check("空特效回退", prefs.ui.reminderEffect, "glow");
        foreach (string effect in new string[] { "none", "glow", "particles", "aurora" })
        {
            prefs.ui.reminderEffect = effect;
            prefs.Normalize();
            Check("合法特效保留 " + effect, prefs.ui.reminderEffect, effect);
        }
        foreach (int hold in new int[] { int.MinValue, 2, 3, 6, 20, 21, int.MaxValue })
        {
            prefs.ui.reminderHoldSeconds = hold;
            prefs.Normalize();
            Check("停留钳制 " + hold, prefs.ui.reminderHoldSeconds.ToString(), Math.Max(3, Math.Min(20, hold)).ToString());
        }
        foreach (bool reduced in new bool[] { true, false })
        {
            prefs.ui.reminderEffect = "particles";
            prefs.ui.reducedMotion = reduced;
            prefs.ui.reminderHoldSeconds = 12;
            var restored = serializer.Deserialize<Settings>(serializer.Serialize(prefs));
            restored.Normalize();
            Check("减少动态 JSON 往返 " + reduced, restored.ui.reducedMotion.ToString(), reduced.ToString());
            Check("特效 JSON 往返 " + reduced, restored.ui.reminderEffect, "particles");
            Check("停留 JSON 往返 " + reduced, restored.ui.reminderHoldSeconds.ToString(), "12");
        }
        var nullPrefs = serializer.Deserialize<Settings>("{\"lark\":null,\"ui\":null}");
        nullPrefs.Normalize();
        Check("空飞书配置可归一化", (nullPrefs.lark != null && !nullPrefs.lark.enabled).ToString(), "True");
        Check("空 UI 配置恢复默认", nullPrefs.ui.reminderEffect + "/" + nullPrefs.ui.reducedMotion + "/" + nullPrefs.ui.reminderHoldSeconds, "glow/False/6");

        // 20. 缺字段的配置与新建对象同一天：兜底不该悄悄换成另一套班次。
        Check("缺字段默认固定班次", nullPrefs.mode, "fixed");
        Check("缺字段默认每日应勤", nullPrefs.flexMinutes.ToString(), "480");

        // 21. 「达标」是到点的判决，不等于工时够了：两把尺子并排放。
        var s21 = new Settings();
        s21.lark.enabled = true;            // 有打卡源，迟到才看得出差别
        s21.lark.chatId = "oc_test";
        var snap21 = new AttendanceSnapshot();
        snap21.ok = true;
        snap21.clockIn = new DateTime(2026, 11, 3, 10, 0, 0);   // 固定班次 09:00，迟到一小时
        var st21 = StatusBuilder.Build(s21, snap21, new DateTime(2026, 11, 3, 10, 0, 0), Themes.Get("midnight"));
        Check("迟到时进度按钟表走", st21.progress.ToString("0.000"), "0.111");
        Check("迟到时在岗时长照实", st21.extra, "已工作 0s · 应勤 9h00m");
        var st21b = StatusBuilder.Build(s21, snap21, new DateTime(2026, 11, 3, 18, 1, 0), Themes.Get("midnight"));
        Check("到点即达标", st21b.state.ToString(), "Overtime");
        Check("达标同时给出工时对照", st21b.extra, "工时 8h01m / 应勤 9h00m · 下班后发「2」记录");

        // 22. 大数字按内容判定角色（ADR 0005）：六种状态逐一断言其大数字用的角色与字号。
        var flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        var widgetType = typeof(WidgetForm);
        var isReading = widgetType.GetMethod("IsPureReading", BindingFlags.Static | BindingFlags.NonPublic);
        Func<string, bool> reading = text => (bool)isReading.Invoke(null, new object[] { text });
        var wf = new WidgetForm(new SettingsStore(), new Settings());
        var roleType = widgetType.GetNestedType("FontRole", BindingFlags.Public | BindingFlags.NonPublic);
        Func<bool, Font> RoleFont = timer => (Font)widgetType.GetField(timer ? "fontBig" : "fontBigText", flags).GetValue(wf);
        Action<string, CardStatus, bool, float> CheckBigRole = (name, card, expectTimer, expectSize) =>
        {
            Check(name + " 读数判定", reading(card.big).ToString(), expectTimer.ToString());
            var f = RoleFont(reading(card.big));
            var expectFamily = (string)widgetType.GetMethod("ResolveRoleFamily", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { Enum.Parse(roleType, expectTimer ? "Timer" : "Body") });
            // FontFamily.Name 可能返回本地化族名（如「等距更纱黑体 SC Semibold」），用同族名构造的字体对照。
            using (var probe = new Font(expectFamily, 10))
                Check(name + " 角色(" + card.state + " 「" + card.big + "」)",
                    f.FontFamily.Name + "/" + f.Size.ToString("0.#"),
                    probe.FontFamily.Name + "/" + expectSize.ToString("0.#"));
        };

        Func<string, CardStatus> BuildState = when =>
        {
            var day = when == "rest" ? new DateTime(2026, 11, 7) : new DateTime(2026, 11, 3); // 周六 / 周二
            var cs = new Settings();
            cs.mode = "flex"; cs.flexMinutes = 630; cs.startTime = "09:00"; cs.lunchDeduct = false;
            cs.lark.enabled = true; cs.lark.chatId = "oc_test";
            AttendanceSnapshot sn = null;                     // 未打卡 / 上班前 / 休息日：无打卡源
            if (when == "working" || when == "overtime")
            {
                sn = new AttendanceSnapshot();
                sn.ok = true;
                sn.clockIn = new DateTime(2026, 11, 3, 9, 27, 0);
            }
            if (when == "done")
            {
                sn = new AttendanceSnapshot();
                sn.ok = true;
                sn.clockIn = new DateTime(2026, 11, 3, 9, 27, 0);
                sn.clockOut = new DateTime(2026, 11, 3, 20, 9, 0);
            }
            var timeOfDay = when == "rest" ? new TimeSpan(10, 0, 0)
                : when == "before" ? new TimeSpan(8, 30, 0)
                : when == "notclock" ? new TimeSpan(9, 40, 0)
                : when == "working" ? new TimeSpan(10, 0, 0)
                : when == "overtime" ? new TimeSpan(19, 57, 0)
                : new TimeSpan(20, 20, 0);
            return StatusBuilder.Build(cs, sn, day.Date + timeOfDay, Themes.Get("midnight"));
        };

        CheckBigRole("Rest 大数字", BuildState("rest"), false, 17f);        // 今天休息
        CheckBigRole("BeforeWork 大数字", BuildState("before"), true, 28f); // 00:30:00
        CheckBigRole("NotClockedIn 大数字", BuildState("notclock"), true, 28f); // 00:40:00
        CheckBigRole("Working 大数字", BuildState("working"), true, 28f);   // 09:57:00
        CheckBigRole("Overtime 大数字", BuildState("overtime"), true, 28f); // 00:00:00
        CheckBigRole("Done 大数字", BuildState("done"), true, 28f);         // 10h42m
        // 23. 内置字体子集与三级回退（ADR 0005）：一级内嵌加载、二三级回退不崩且不落 Microsoft Sans Serif。
        var staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
        widgetType.GetMethod("EnsureEmbeddedFonts", staticFlags).Invoke(null, null);
        var embeddedTimer = (string)widgetType.GetField("embeddedTimerFamily", staticFlags).GetValue(null);
        var embeddedBody = (string)widgetType.GetField("embeddedBodyFamily", staticFlags).GetValue(null);
        Check("内置子集加载 Timer", (embeddedTimer != null).ToString(), "True");
        Check("内置子集加载 Body", (embeddedBody != null).ToString(), "True");
        Check("Timer 子集族名", embeddedTimer ?? "(null)", "Maple Mono NF CN ExtraBold");
        Check("Body 子集族名", embeddedBody ?? "(null)", "Sarasa Mono SC SemiBold");

        // 强制内置子集失效（模拟一级失败），应走二级系统族或三级回退，绝不返回 Microsoft Sans Serif。
        widgetType.GetField("embeddedTimerFamily", staticFlags).SetValue(null, null);
        widgetType.GetField("embeddedBodyFamily", staticFlags).SetValue(null, null);
        var resolve = widgetType.GetMethod("ResolveRoleFamily", staticFlags);
        var fbTimer = (string)resolve.Invoke(null, new object[] { Enum.Parse(roleType, "Timer") });
        var fbBody = (string)resolve.Invoke(null, new object[] { Enum.Parse(roleType, "Body") });
        Check("Timer 回退合法", (fbTimer == "Consolas" || fbTimer.IndexOf("Maple Mono", StringComparison.OrdinalIgnoreCase) >= 0).ToString(), "True");
        Check("Body 回退合法", (fbBody == "Microsoft YaHei UI" || fbBody.IndexOf("Sarasa Mono", StringComparison.OrdinalIgnoreCase) >= 0).ToString(), "True");
        Check("回退不落默认替换字体", (fbTimer != "Microsoft Sans Serif" && fbBody != "Microsoft Sans Serif").ToString(), "True");

        // 提醒层时间段走计时角色（fontRemindTime = Timer，12pt，自带字重不加粗）。
        var remindTimeFont = (Font)widgetType.GetField("fontRemindTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(wf);
        Check("提醒时间段归 Timer", remindTimeFont.FontFamily.Name, embeddedTimer ?? "Maple Mono NF CN ExtraBold");
        Check("提醒时间段 12pt", remindTimeFont.Size.ToString("0.#"), "12");
        Check("时间段区间算读数", reading("15:30–16:00").ToString(), "True");

        // 24. 终盘（ADR 0006）：目标前十分钟切告警态；两个目标都在窗口里时更早的那个说了算。
        var sa = new Settings();
        sa.mode = "fixed";
        sa.startTime = "09:00";
        sa.endTime = "18:00";
        sa.lunchStart = "11:50";
        sa.lunchEnd = "13:30";
        sa.ui.showLunch = true;
        sa.ui.finalAlert = true;
        sa.lark.enabled = false;

        Func<int, int, int, CardStatus> At = (hh, mm, ss) =>
            StatusBuilder.Build(sa, null, new DateTime(2026, 11, 3, hh, mm, ss), Themes.Get("midnight"));

        Check("终盘 18:00 前 11 分钟没进", At(17, 49, 0).alert.ToString(), "False");
        Check("终盘 18:00 前 10 分钟进入", At(17, 50, 0).alert.ToString(), "True");
        Check("终盘 18:00 前 10 分钟文案", At(17, 50, 0).alertLabel, "距离下班");
        Check("终盘 18:00 前 10 分钟目标", At(17, 50, 0).alertTargetText, "18:00");
        Check("终盘下班不是午休", At(17, 50, 0).alertLunch.ToString(), "False");
        Check("终盘英文标签", At(17, 50, 0).alertEn, "SHIFT END");

        Check("午休终盘 11:40 进入", At(11, 40, 0).alert.ToString(), "True");
        Check("午休终盘文案", At(11, 40, 0).alertLabel, "距离午休");
        Check("午休终盘目标", At(11, 40, 0).alertTargetText, "11:50");
        Check("午休终盘标记为午休", At(11, 40, 0).alertLunch.ToString(), "True");
        Check("午休终盘英文标签", At(11, 40, 0).alertEn, "LUNCH BREAK");
        Check("午休终盘 11:39 没进", At(11, 39, 0).alert.ToString(), "False");

        // 两个目标都落在十分钟内：更早的那个说了算（午休 11:50 先于下班 12:00）
        var sb = new Settings();
        sb.mode = "fixed"; sb.startTime = "09:00"; sb.endTime = "12:00";
        sb.lunchStart = "11:50"; sb.lunchEnd = "13:30"; sb.ui.showLunch = true; sb.lark.enabled = false;
        var both = StatusBuilder.Build(sb, null, new DateTime(2026, 11, 3, 11, 45, 0), Themes.Get("midnight"));
        Check("两个目标都在窗口里取更早的", both.alertLabel, "距离午休");

        // 到点之后、已下班、休息日都不该进终盘
        Check("到点后不进终盘", At(18, 0, 0).alert.ToString(), "False");
        Check("已达标后不进终盘", At(18, 30, 0).alert.ToString(), "False");
        var saturday = StatusBuilder.Build(sa, null, new DateTime(2026, 11, 7, 17, 55, 0), Themes.Get("midnight"));
        Check("休息日不进终盘", saturday.alert.ToString(), "False");

        // 开关：关掉终盘告警，或关掉午休倒计时，都不进
        sa.ui.finalAlert = false;
        Check("关掉终盘开关就没终盘", At(17, 55, 0).alert.ToString(), "False");
        sa.ui.finalAlert = true;
        sa.ui.showLunch = false;
        Check("关掉午休倒计时就没有午休终盘", At(11, 40, 0).alert.ToString(), "False");
        sa.ui.showLunch = true;

        // 老配置缺字段时默认开启；告警色非法值回落到跟随主题
        var sLegacy = new Settings();
        sLegacy.ui.finalAlert = null;
        sLegacy.ui.alertAccent = "明天再说";
        sLegacy.Normalize();
        Check("老配置缺终盘字段默认开", sLegacy.ui.finalAlert.ToString(), "True");
        Check("告警色非法回落", sLegacy.ui.alertAccent, "theme");
        sLegacy.ui.alertAccent = "red";
        sLegacy.Normalize();
        Check("告警色合法保留", sLegacy.ui.alertAccent, "red");

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "ALL PASS" : failures + " FAILURES");
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
