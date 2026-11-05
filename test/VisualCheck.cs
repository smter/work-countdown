using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using WorkCountdown;
class VisualCheck {
 static BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Set(object o,string name,object value){o.GetType().GetField(name,F).SetValue(o,value);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,F).Invoke(o,args);}
 [STAThread] static void Main(string[] args){
  Application.EnableVisualStyles();
  // args[0]=输出目录（默认 visual）；args[1]="all" 时全量保存每个组合（回归基线模式）。
  string dir=args.Length>0?args[0]:Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"visual");Directory.CreateDirectory(dir);
  bool full=args.Length>1&&args[1]=="all";
  int cases=0;
  foreach(float dpi in new float[]{96,144,192})
  foreach(string effect in new string[]{"none","glow","particles","aurora"})
  foreach(bool collapsed in new bool[]{false,true}){
   Settings s=new Settings();s.lark.enabled=false;s.calendarEnabled=false;s.ui.edge="free";s.ui.autoCollapseSeconds=0;s.ui.reminderEffect=effect;s.ui.theme="midnight";
   using(var form=new WidgetForm(new SettingsStore(),s)){
    Set(form,"scale",dpi/96f);Set(form,"collapsed",collapsed);
    form.MinimumSize=Size.Empty;form.Size=new Size((int)((collapsed?224:280)*dpi/96),(int)((collapsed?68:210)*dpi/96));
    Call(form,"Recompute");
    int width=form.Width,height=form.Height;
    foreach(bool meeting in new bool[]{false,true}){
     Set(form,"remindEvent",new ReminderEvent { title="产品需求评审 · 项目进度与下一阶段规划 🗓",detail="明确负责人、验收时间及联调计划\n换行不影响布局",place="12F · 南侧会议室 A",start=Reminders.FormatStart(DateTime.Now.AddMinutes(5)),end=Reminders.FormatStart(DateTime.Now.AddMinutes(35)),source=meeting?"lark":"local"});
     Set(form,"reminding",true);
     int[] moments={0,200,420,600,800,1200,4200,7100,7400,7760,7850,7950};
     foreach(int moment in moments){
      Set(form,"remindAt",DateTime.Now.AddMilliseconds(-moment));
      using(var bmp=new Bitmap(width,height)){
       bmp.SetResolution(dpi,dpi);
       using(var g=Graphics.FromImage(bmp)) Call(form,"OnPaint",new PaintEventArgs(g,new Rectangle(0,0,width,height)));
       if(full) bmp.Save(Path.Combine(dir,dpi+"-"+effect+"-"+(collapsed?"col":"exp")+"-"+(meeting?"mtg":"evt")+"-"+moment+".png"));
       else if(dpi==144 && !collapsed && (effect=="glow" || effect=="particles" || effect=="aurora")) bmp.Save(Path.Combine(dir,effect+"-"+(meeting?"meeting":"event")+"-"+moment+".png"));
      }cases++;
     }
    }
    Set(form,"reminding",false);
    using(var bmp=new Bitmap(width,height)) {bmp.SetResolution(dpi,dpi);using(var g=Graphics.FromImage(bmp)) Call(form,"OnPaint",new PaintEventArgs(g,new Rectangle(0,0,width,height)));if(full) bmp.Save(Path.Combine(dir,dpi+"-"+effect+"-"+(collapsed?"col":"exp")+"-idle.png")); else if(dpi==144 && effect=="glow") bmp.Save(Path.Combine(dir,collapsed?"compact.png":"expanded.png"));}
    s.ui.reducedMotion=true;Set(form,"reminding",true);Set(form,"remindAt",DateTime.Now.AddSeconds(-2));
    using(var bmp=new Bitmap(width,height)) {bmp.SetResolution(dpi,dpi);using(var g=Graphics.FromImage(bmp)) Call(form,"OnPaint",new PaintEventArgs(g,new Rectangle(0,0,width,height)));if(full) bmp.Save(Path.Combine(dir,dpi+"-"+effect+"-"+(collapsed?"col":"exp")+"-quiet.png"));}cases++;
    Call(form,"OnFormClosing",new FormClosingEventArgs(CloseReason.None,false));
   }
  }
  // 终盘告警态（ADR 0006）：3 种 DPI × 展开/横条 × 下班/午休。
  foreach(float adpi in new float[]{96,144,192})
  foreach(bool acollapsed in new bool[]{false,true})
  foreach(bool alunch in new bool[]{false,true}){
   var aset=new Settings();aset.lark.enabled=false;aset.calendarEnabled=false;
   aset.ui.edge="free";aset.ui.autoCollapseSeconds=0;aset.ui.theme="midnight";
   aset.ui.finalAlert=true;aset.ui.alertAccent="theme";
   aset.mode="fixed";aset.startTime="09:00";
   aset.endTime=DateTime.Now.AddMinutes(5).ToString("HH:mm");
   aset.ui.showLunch=alunch;
   if(alunch){aset.lunchStart=DateTime.Now.AddMinutes(3).ToString("HH:mm");aset.lunchEnd=DateTime.Now.AddMinutes(90).ToString("HH:mm");}
   using(var aform=new WidgetForm(new SettingsStore(),aset)){
    Set(aform,"scale",adpi/96f);Set(aform,"collapsed",acollapsed);
    aform.MinimumSize=Size.Empty;
    aform.Size=new Size((int)((acollapsed?224:280)*adpi/96),(int)((acollapsed?68:186)*adpi/96));
    Call(aform,"Recompute");
    Set(aform,"alertEnteredAt",Environment.TickCount-30000);   // 入场动画放完再看静态样子
    using(var abmp=new Bitmap(aform.Width,aform.Height)){
     abmp.SetResolution(adpi,adpi);
     using(var ag=Graphics.FromImage(abmp)) Call(aform,"OnPaint",new PaintEventArgs(ag,new Rectangle(0,0,aform.Width,aform.Height)));
     if(full) abmp.Save(Path.Combine(dir,adpi+"-alert-"+(acollapsed?"col":"exp")+"-"+(alunch?"lunch":"eod")+".png"));
     else if(adpi==144) abmp.Save(Path.Combine(dir,"alert-"+(acollapsed?"strip":"expanded")+"-"+adpi+".png"));
    }
    cases++;
    Call(aform,"OnFormClosing",new FormClosingEventArgs(CloseReason.None,false));
   }
  }

  // 终盘强度分档（ADR 0006）：最后 60 秒 / 最后 10 秒，144 DPI 展开态各一帧。
  foreach(int remain in new int[]{55000,8000}){
   var aset=new Settings();aset.lark.enabled=false;aset.calendarEnabled=false;
   aset.ui.edge="free";aset.ui.autoCollapseSeconds=0;aset.ui.theme="midnight";
   aset.ui.finalAlert=true;aset.ui.alertAccent="theme";
   aset.mode="fixed";aset.startTime="09:00";aset.endTime="17:00";
   using(var aform=new WidgetForm(new SettingsStore(),aset)){
    Set(aform,"scale",1.5f);Set(aform,"collapsed",false);
    aform.MinimumSize=Size.Empty;aform.Size=new Size(420,315);
    Call(aform,"Recompute");
    Call(aform,"StartAlertPreview",600.0);
    DateTime at=DateTime.Now.AddMilliseconds(remain);
    Set(aform,"alertTarget",at);Set(aform,"alertTargetText",at.ToString("HH:mm"));
    Set(aform,"alertEnteredAt",Environment.TickCount-30000);   // 入场已经放完
    using(var abmp=new Bitmap(420,315)){
     abmp.SetResolution(144,144);
     using(var ag=Graphics.FromImage(abmp)) Call(aform,"OnPaint",new PaintEventArgs(ag,new Rectangle(0,0,420,315)));
     string tag=remain>10000?"hot":"crit";
     abmp.Save(Path.Combine(dir,full?("144-alert-"+tag+".png"):("alert-"+tag+"-144.png")));
    }
    cases++;
    Call(aform,"OnFormClosing",new FormClosingEventArgs(CloseReason.None,false));
   }
  }

  var demo=new Settings();demo.lark.enabled=false;demo.calendarEnabled=false;demo.ui.edge="free";demo.ui.reminderEffect="particles";
  using(var form=new WidgetForm(new SettingsStore(),demo)){
   Set(form,"scale",1.5f);Set(form,"collapsed",false);form.MinimumSize=Size.Empty;form.Size=new Size(420,315);Call(form,"Recompute");
   Set(form,"remindEvent",new ReminderEvent {title="产品设计评审",detail="确认交互方案与本周排期",place="12F · 会议室 A",start=Reminders.FormatStart(DateTime.Now.AddMinutes(5)),end=Reminders.FormatStart(DateTime.Now.AddMinutes(35)),source="lark"});
   for(int moment=0;moment<9000;moment+=50){Set(form,"reminding",moment<7960);Set(form,"remindAt",DateTime.Now.AddMilliseconds(-moment));using(var bmp=new Bitmap(420,315)){bmp.SetResolution(144,144);using(var g=Graphics.FromImage(bmp))Call(form,"OnPaint",new PaintEventArgs(g,new Rectangle(0,0,420,315)));if(!full) bmp.Save(Path.Combine(dir,"demo-"+moment.ToString("D5")+".png"));}}
   Call(form,"OnFormClosing",new FormClosingEventArgs(CloseReason.None,false));
  }
  var settings=new Settings();settings.lark.enabled=false;settings.calendarEnabled=false;
  using(var dialog=new SettingsForm(new SettingsStore(),settings)){
   dialog.CreateControl();
   var tabs=(TabControl)typeof(SettingsForm).GetField("tabs",F).GetValue(dialog);
   dialog.Show();Application.DoEvents();tabs.SelectedIndex=2;Application.DoEvents();
   using(var bmp=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(dir,"appearance.png"));}
   dialog.Close();
  }
  // 附加覆盖：深浅主题 × 低动效，长文本提醒（展开态 150%）。
  if(full) foreach(string themeId in new string[]{"midnight","ivory"})
  foreach(bool quiet in new bool[]{false,true}){
   var ts=new Settings();ts.lark.enabled=false;ts.calendarEnabled=false;ts.ui.edge="free";ts.ui.reminderEffect="glow";ts.ui.theme=themeId;ts.ui.reducedMotion=quiet;
   using(var form=new WidgetForm(new SettingsStore(),ts)){
    Set(form,"scale",1.5f);Set(form,"collapsed",false);form.MinimumSize=Size.Empty;form.Size=new Size(420,315);Call(form,"Recompute");
    Set(form,"remindEvent",new ReminderEvent { title="产品需求评审 · 项目进度与下一阶段规划与验收标准讨论 🗓",detail="明确负责人、验收时间及联调计划\n换行不影响布局\n会议时间较长时 detail 会换到更多行",place="12F · 南侧会议室 A（near 洽谈区）",start=Reminders.FormatStart(DateTime.Now.AddMinutes(5)),end=Reminders.FormatStart(DateTime.Now.AddMinutes(35)),source="lark"});
    Set(form,"reminding",true);Set(form,"remindAt",DateTime.Now.AddMilliseconds(-800));
    using(var bmp=new Bitmap(form.Width,form.Height)){bmp.SetResolution(144,144);using(var g=Graphics.FromImage(bmp)) Call(form,"OnPaint",new PaintEventArgs(g,new Rectangle(0,0,form.Width,form.Height)));bmp.Save(Path.Combine(dir,"theme-"+themeId+"-"+(quiet?"quiet":"motion")+"-reminder.png"));}
    Set(form,"reminding",false);
    using(var bmp=new Bitmap(form.Width,form.Height)){bmp.SetResolution(144,144);using(var g=Graphics.FromImage(bmp)) Call(form,"OnPaint",new PaintEventArgs(g,new Rectangle(0,0,form.Width,form.Height)));bmp.Save(Path.Combine(dir,"theme-"+themeId+"-"+(quiet?"quiet":"motion")+"-idle.png"));}
    Call(form,"OnFormClosing",new FormClosingEventArgs(CloseReason.None,false));
   }cases+=2;
  }
  Console.WriteLine("PASS: "+cases+" rendering cases; out="+dir+(full?" (baseline all)":""));
 }
}
