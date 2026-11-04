param(
    [string]$Theme = 'midnight',
    [string]$Mode = 'flex',
    [int]$X = 220,
    [int]$Y = 220,
    [switch]$Lunch,
    [switch]$Collapsed,
    [switch]$Expand,
    [switch]$Remind,
    [switch]$Frames,
    [int]$WaitSeconds = 2,
    [string]$Out = 'preview.png'
)

# 用一个独立的便携目录跑一份配置固定的实例，方便截图对比（不动用户自己的配置）
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dir = Join-Path $root 'preview-run'
$exe = Join-Path $root 'dist\WorkCountdown.exe'

Get-Process WorkCountdown -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path (Join-Path $dir 'data') -Force | Out-Null
Copy-Item $exe $dir
Set-Content -Path (Join-Path $dir 'portable.txt') -Value 'portable' -Encoding ASCII

$lunchFlag = if ($Lunch) { 'true' } else { 'false' }
$cfg = @"
{
  "mode": "$Mode",
  "startTime": "09:00",
  "endTime": "18:00",
  "flexMinutes": 630,
  "lunchDeduct": false,
  "lunchStart": "11:50",
  "lunchEnd": "13:30",
  "workdays": [false, true, true, true, true, true, false],
  "lark": { "enabled": false, "chatId": "", "botName": "考勤", "pollMinutes": 5, "command": "lark-cli" },
  "ui": { "x": $X, "y": $Y, "edge": "free", "theme": "$Theme", "opacity": 100,
          "autoCollapseSeconds": 0, "showLunch": $lunchFlag, "alwaysOnTop": true },
  "calendarEnabled": false,
  "events": []
}
"@
[System.IO.File]::WriteAllText((Join-Path $dir 'data\settings.json'), $cfg, (New-Object System.Text.UTF8Encoding($false)))

Add-Type -AssemblyName System.Drawing
try { Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class DpiP { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }' } catch { }
[DpiP]::SetProcessDPIAware() | Out-Null

function Start-App { Start-Process -FilePath (Join-Path $dir 'WorkCountdown.exe') }

function Get-Widget {
    $code = @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public class PW {
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
    try { Add-Type -TypeDefinition $code } catch { }
    $procId = (Get-Process WorkCountdown -ErrorAction Stop | Select-Object -First 1).Id
    $script:target = [IntPtr]::Zero
    [PW]::EnumWindows({ param($h, $l)
        $p = 0
        [PW]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
        if ($p -eq $procId -and [PW]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [PW]::GetWindowText($h, $sb, 256) | Out-Null
            if ($sb.ToString() -eq '下班倒计时') { $script:target = $h }
        }
        return $true
    }, [IntPtr]::Zero) | Out-Null
    return $script:target
}

function Save-Shot([IntPtr]$h, [string]$path) {
    $r = New-Object PW+RECT
    [PW]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.R - $r.L; $h2 = $r.B - $r.T
    $bmp = New-Object System.Drawing.Bitmap($w, $h2)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [PW]::PrintWindow($h, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc)
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
}

function Click-At([IntPtr]$h, [int]$cx, [int]$cy) {
    $lp = [IntPtr](($cy -shl 16) -bor $cx)
    [PW]::PostMessage($h, 0x0201, [IntPtr]1, $lp) | Out-Null
    Start-Sleep -Milliseconds 60
    [PW]::PostMessage($h, 0x0202, [IntPtr]0, $lp) | Out-Null
}

$when = (Get-Date).AddMinutes(2).ToString('yyyy-MM-dd HH:mm')
$whenEnd = (Get-Date).AddMinutes(32).ToString('yyyy-MM-dd HH:mm')
$themeId = $Theme
if ($Theme -eq 'meeting') { $themeId = 'midnight' }

if ($Remind) {
    $event = '{ "id": "local:preview", "title": "需求对齐会", "start": "' + $when + '", "end": "' + $whenEnd + '", "place": "会议室 A · 12 层", "detail": "过一遍本周排期和联调计划", "source": "local", "leadMinutes": 5 }'
    $meeting = '{ "id": "lark:preview", "title": "产品部周会", "start": "' + $when + '", "end": "' + $whenEnd + '", "place": "线上会议", "detail": "本周排期与联调计划", "source": "lark", "leadMinutes": 5 }'
    $use = if ($Theme -eq 'meeting') { $meeting } else { $event }
    $cfg2 = $cfg -replace '"theme": "[^"]*"', ('"theme": "' + $themeId + '"') -replace '"events": \[\]', ('"events": [ ' + $use + ' ]')
    [System.IO.File]::WriteAllText((Join-Path $dir 'data\settings.json'), $cfg2, (New-Object System.Text.UTF8Encoding($false)))
}

Start-App
Start-Sleep -Seconds $WaitSeconds
$h = Get-Widget
if ($h -eq [IntPtr]::Zero) { throw '没有找到悬浮窗' }

if ($Collapsed -and -not $Remind) {
    Click-At $h 343 31
    Start-Sleep -Milliseconds 400
}

if ($Frames) {
    $fd = Join-Path $root 'frames-preview'
    Remove-Item $fd -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $fd | Out-Null
    $i = 0
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt 9000) {
        Save-Shot $h (Join-Path $fd ("f{0:d3}.png" -f $i))
        $i++
        Start-Sleep -Milliseconds 60
    }
    "frames=$i"
} else {
    Save-Shot $h (Join-Path $root $Out)
    $r = New-Object PW+RECT
    [PW]::GetWindowRect($h, [ref]$r) | Out-Null
    "captured $($r.R-$r.L)x$($r.B-$r.T) at ($($r.L),$($r.T)) -> $Out"
}
