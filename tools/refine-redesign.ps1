$p='D:\DSH\RWZ0\work-countdown\src\Widget.cs'
$t=[IO.File]::ReadAllText($p)
$t=$t.Replace('if (reminding) return;', 'if (reminding || animating) return;')
$t=$t.Replace('            if (collapsed)'+"`n"+'            {'+'', '            if (collapsed)'+"`n"+'            {')
# Central guard prevents menu and mouse resizing a live reminder.
$t=$t.Replace('            if (collapsed == value && !animating) return;', '            if (reminding) return;' + "`r`n" + '            if (collapsed == value && !animating) return;')
$t=$t.Replace('            remindEvent = e;', '            if (animating) { animT.Stop(); animating = false; SetBounds(animToX, Top, animToW, animToH); collapsed = animToW == S(DesignCollapsedW); ApplyRegion(); }' + "`r`n" + '            remindEvent = e;' + "`r`n" + '            AccessibleDescription = e.title + " · " + Reminders.TimeRange(e) + " · " + e.place + " · " + e.detail;')
$t=$t.Replace('int seam = collapsed ? S(37) : S(90);','int seam = collapsed ? S(37) : S(98);')
$t=$t.Replace('new RectangleF(pad,S(39),w,S(35))','new RectangleF(pad,S(35),w,S(35))').Replace('new RectangleF(pad,S(97),w,S(24))','new RectangleF(pad,S(104),w,S(24))').Replace('new RectangleF(pad,S(123),w,S(21))','new RectangleF(pad,S(130),w,S(21))')
$t=$t.Replace('            if (Height >= S(200))','            if (!string.IsNullOrWhiteSpace(remindEvent.detail))').Replace('new RectangleF(pad,S(147),w,S(20))','new RectangleF(pad,S(74),w,S(18))')
$t=$t.Replace('double oldAlpha = settings.ui.reducedMotion ? 0 : 1-Clamp01(elapsed/130);','double oldAlpha = (settings.ui.reducedMotion || SystemInformation.HighContrast) ? 0 : Math.Max(1-Clamp01(elapsed/130), Clamp01((elapsed-RemindInMs-RemindHoldMs-780)/160));')
$t=$t.Replace('remindT.Interval = settings.ui.reducedMotion ? 100 : 33;', 'remindT.Interval = (settings.ui.reducedMotion || SystemInformation.HighContrast) ? 100 : 33;')
[IO.File]::WriteAllText($p,$t,(New-Object Text.UTF8Encoding($false)))
