param(
    [string]$Out = 'dlg.png',
    [int]$Tab = 0,
    [string]$Title = '下班倒计时 · 设置'
)

# 打开设置对话框并把它打印成图片（不做真实截屏，窗口被遮挡也没关系）
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Add-Type -AssemblyName System.Drawing
try { Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class DpiQ { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }' } catch { }
[DpiQ]::SetProcessDPIAware() | Out-Null

$code = @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class DW {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
'@
Add-Type -TypeDefinition $code

$procId = (Get-Process WorkCountdown -ErrorAction Stop | Select-Object -First 1).Id

function Find-Window([string]$want) {
    $script:found = [IntPtr]::Zero
    [DW]::EnumWindows({ param($h, $l)
        $p = 0
        [DW]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
        if ($p -eq $procId -and [DW]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [DW]::GetWindowText($h, $sb, 256) | Out-Null
            if ($sb.ToString() -eq $want) { $script:found = $h }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
    return $script:found
}

$widget = Find-Window '下班倒计时'
if ($widget -eq [IntPtr]::Zero) { throw '没有找到悬浮窗' }

# 点右上角齿轮（未展开先点一下卡片展开）
$r = New-Object DW+RECT
[DW]::GetWindowRect($widget, [ref]$r) | Out-Null
if (($r.R - $r.L) -lt 380) {
    $lp = [IntPtr](((40) -shl 16) -bor (($r.R - $r.L) / 2))
    [DW]::PostMessage($widget, 0x0201, [IntPtr]1, $lp) | Out-Null
    Start-Sleep -Milliseconds 80
    [DW]::PostMessage($widget, 0x0202, [IntPtr]0, $lp) | Out-Null
    Start-Sleep -Milliseconds 900
    [DW]::GetWindowRect($widget, [ref]$r) | Out-Null
}
$gear = [IntPtr](((28 -shl 16) -bor (($r.R - $r.L) - 87)))
[DW]::PostMessage($widget, 0x0201, [IntPtr]1, $gear) | Out-Null
Start-Sleep -Milliseconds 80
[DW]::PostMessage($widget, 0x0202, [IntPtr]0, $gear) | Out-Null
Start-Sleep -Milliseconds 1200

$dlg = Find-Window $Title
if ($dlg -eq [IntPtr]::Zero) { throw "没有找到窗口：$Title" }

if ($Tab -gt 0) {
    # 点标签页：(8 + 112*i + 56, 8 + 17) 设计坐标 ×1.5
    $x = 12 + 112 * $Tab + 56
    $y = 29
    $lp = [IntPtr](($y -shl 16) -bor $x)
    [DW]::SetForegroundWindow($dlg) | Out-Null
    Start-Sleep -Milliseconds 300
    $pt = New-Object DW+POINT
    $pt.X = $x; $pt.Y = $y
    [DW]::ClientToScreen($dlg, [ref]$pt) | Out-Null
    [DW]::SetCursorPos($pt.X, $pt.Y) | Out-Null
    Start-Sleep -Milliseconds 120
    for ($k = 0; $k -lt 2; $k++) {
        [DW]::SetCursorPos($pt.X, $pt.Y) | Out-Null
        [DW]::mouse_event(0x0002, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 60
        [DW]::mouse_event(0x0004, 0, 0, 0, [IntPtr]::Zero)
        Start-Sleep -Milliseconds 600
    }
}

[DW]::GetWindowRect($dlg, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[DW]::PrintWindow($dlg, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"captured dialog ${w}x${h} -> $Out"
