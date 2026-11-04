param(
    [Parameter(Mandatory = $true)][string]$Out,
    [switch]$Expand
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# 本进程也要 DPI 感知，否则 GetWindowRect 返回虚拟坐标，截图会错位或缩糊
try { Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class DpiFix { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }' } catch { }
[DpiFix]::SetProcessDPIAware() | Out-Null

$code = @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class Win {
  [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
'@
Add-Type -TypeDefinition $code

$procId = (Get-Process WorkCountdown -ErrorAction Stop | Select-Object -First 1).Id
$target = [IntPtr]::Zero
[Win]::EnumWindows({ param($h, $l)
    $p = 0
    [Win]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
    if ($p -eq $procId -and [Win]::IsWindowVisible($h)) {
        $sb = New-Object System.Text.StringBuilder 256
        [Win]::GetWindowText($h, $sb, 256) | Out-Null
        if ($sb.ToString() -eq '下班倒计时') { $script:target = $h }
    }
    return $true
}, [IntPtr]::Zero) | Out-Null

if ($target -eq [IntPtr]::Zero) { throw '没有找到悬浮窗' }

$r = New-Object Win+RECT
[Win]::GetWindowRect($target, [ref]$r) | Out-Null
$w = $r.R - $r.L
if ($Expand -and $w -lt 380) {
    $lp = [IntPtr](((132 / 2) -shl 16) -bor ($w / 2))
    [Win]::PostMessage($target, 0x0201, [IntPtr]1, $lp) | Out-Null
    Start-Sleep -Milliseconds 60
    [Win]::PostMessage($target, 0x0202, [IntPtr]0, $lp) | Out-Null
    Start-Sleep -Milliseconds 700
}

[Win]::GetWindowRect($target, [ref]$r) | Out-Null
$w = $r.R - $r.L; $h2 = $r.B - $r.T
$bmp = New-Object System.Drawing.Bitmap($w, $h2)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[Win]::PrintWindow($target, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"captured ${w}x${h2} at ($($r.L),$($r.T)) -> $Out"
