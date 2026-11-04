$ErrorActionPreference='Stop'
$p='D:\DSH\RWZ0\work-countdown\src\Widget.cs'
$t=[IO.File]::ReadAllText($p)
$t=$t.Replace('DesignExpandedW = 254','DesignExpandedW = 280').Replace('DesignExpandedH = 150','DesignExpandedH = 186').Replace('DesignExpandedHLunch = 172','DesignExpandedHLunch = 210').Replace('DesignCollapsedH = 58','DesignCollapsedH = 68')
$t=$t.Replace('private const int RemindHoldMs = 2000;', 'private int RemindHoldMs { get { return Math.Max(3, Math.Min(20, settings.ui.reminderHoldSeconds)) * 1000; } } //')
$t=$t.Replace('private const int RemindInMs = 660;', 'private const int RemindInMs = 1000;').Replace('private const int RemindOutMs = 520;', 'private const int RemindOutMs = 800;')
$t=$t.Replace('MakeFont("Consolas", 23f','MakeFont("Consolas", 28f').Replace('MakeFont("Microsoft YaHei UI", 7.5f','MakeFont("Microsoft YaHei UI", 8.5f').Replace('MakeFont("Microsoft YaHei UI", 8f','MakeFont("Microsoft YaHei UI", 9f')
$t=$t.Replace('Bounds.Contains(PointToClient(Cursor.Position))','ClientRectangle.Contains(PointToClient(Cursor.Position))')
$t=$t.Replace('if (collapsed || animating) return;', 'if (collapsed || animating || reminding) return;')
$t=$t.Replace('remindT.Start();', 'remindT.Interval = settings.ui.reducedMotion ? 100 : 33; remindT.Start();')
$a=$t.IndexOf('        /// <summary>', $t.IndexOf('private static Color MixColor'))
$b=$t.IndexOf('        /// <summary>按宽度', $a)
if($a -lt 0 -or $b -lt 0){throw 'Reminder markers missing'}
$new=@'
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
            int seam = collapsed ? S(37) : S(90);
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
            Line(g, remindEvent.title, fontRemindTitle, ink, new RectangleF(pad,S(39),w,S(35)),alpha,false);
            Line(g, when, fontRemindTime, ink, new RectangleF(pad,S(97),w,S(24)),alpha,false);
            string place = string.IsNullOrWhiteSpace(remindEvent.place) ? "未设置地点" : remindEvent.place;
            Line(g, place, fontRemindMeta, ink, new RectangleF(pad,S(123),w,S(21)),alpha,false);
            // Content keeps a fixed position; optional rows never cause title jumps.
            if (Height >= S(200))
                Line(g, remindEvent.detail, fontSmall, ink, new RectangleF(pad,S(147),w,S(20)),alpha,false);
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

'@
$t=$t.Substring(0,$a)+$new+"`r`n"+$t.Substring($b)
$a=$t.IndexOf('        protected override void OnPaint(PaintEventArgs e)')
$b=$t.IndexOf('        private void DrawGlyph(', $a)
if($a -lt 0 -or $b -lt 0){throw 'Paint markers missing'}
$new=@'
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
                    if (collapsed) PaintCollapsed(g); else PaintExpanded(g);
                }
                else
                {
                    double elapsed = (DateTime.Now-remindAt).TotalMilliseconds;
                    // Render old content into an isolated layer: whole words fade, never get wiped.
                    double oldAlpha = settings.ui.reducedMotion ? 0 : 1-Clamp01(elapsed/130);
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
            Font basis=status.bigIsClock?fontBig:fontBigText;
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
            Line(g,status.big,status.bigIsClock?fontCollapsedNum:fontCollapsedText,theme.textMain,
                new RectangleF(pad-S(1),S(27),w,S(27)),1,false);
            DrawProgress(g,pad,S(59),Width-pad*2,S(3));
            if(lastFetchFailed) Line(g,"!",fontStatus,Color.FromArgb(242,179,76),new RectangleF(Width-pad-S(14),S(32),S(14),S(20)),1,true);
        }

'@
$t=$t.Substring(0,$a)+$new+"`r`n"+$t.Substring($b)
# A reminder owns the visual surface; do not let invisible normal glyph hit targets fire.
$t=$t.Replace('            if (rectGear.Contains(e.Location))', '            if (reminding) return;' + "`r`n" + '            if (rectGear.Contains(e.Location))')
[IO.File]::WriteAllText($p,$t,(New-Object Text.UTF8Encoding($false)))
Write-Output 'Widget redesign applied'
