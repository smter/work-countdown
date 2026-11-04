param(
    [string]$Version = '1.1'
)

# 打包成一份可以直接发给别人的压缩包：dist\下班倒计时-v1.0.zip
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'
$pack = Join-Path $dist 'pack'

& (Join-Path $root 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw '编译失败' }

Remove-Item $pack -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $pack | Out-Null

Copy-Item (Join-Path $dist 'WorkCountdown.exe') (Join-Path $pack 'WorkCountdown.exe')

function Write-Text([string]$path, [string]$text, [switch]$Bom) {
    $enc = New-Object System.Text.UTF8Encoding($Bom.IsPresent)
    [System.IO.File]::WriteAllText($path, $text, $enc)
}

# ---------- 安装 / 卸载脚本 ----------
$install = @'
$ErrorActionPreference = 'Stop'
$src = Split-Path -Parent $MyInvocation.MyCommand.Path
$dst = Join-Path $env:LOCALAPPDATA 'Programs\WorkCountdown'

Write-Host "安装到：$dst"
Get-Process WorkCountdown -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
New-Item -ItemType Directory -Force -Path $dst | Out-Null
Copy-Item (Join-Path $src 'WorkCountdown.exe') (Join-Path $dst 'WorkCountdown.exe') -Force

$exe = Join-Path $dst 'WorkCountdown.exe'
$lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) '下班倒计时.lnk'
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $dst
$sc.IconLocation = "$exe,0"
$sc.Description = '下班倒计时悬浮窗'
$sc.Save()

Write-Host "桌面快捷方式：$lnk"
Start-Process -FilePath $exe
Write-Host ''
Write-Host '装好了。第一次打开会让你填上班时间和午休，之后右键卡片可以改。'
'@
Write-Text (Join-Path $pack 'install.ps1') $install -Bom

$uninstall = @'
$ErrorActionPreference = 'Stop'
$dst = Join-Path $env:LOCALAPPDATA 'Programs\WorkCountdown'

Get-Process WorkCountdown -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400

$lnk = Join-Path ([Environment]::GetFolderPath('Desktop')) '下班倒计时.lnk'
if (Test-Path $lnk) { Remove-Item $lnk -Force; Write-Host "已删除快捷方式" }

try {
    $run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    if (Get-ItemProperty -Path $run -Name WorkCountdown -ErrorAction SilentlyContinue) {
        Remove-ItemProperty -Path $run -Name WorkCountdown -Force
        Write-Host '已取消开机自启'
    }
} catch { }

if (Test-Path $dst) { Remove-Item $dst -Recurse -Force; Write-Host "已删除程序目录：$dst" }

$cfg = Join-Path $env:APPDATA 'WorkCountdown'
if (Test-Path $cfg) {
    Write-Host ''
    Write-Host "配置和日志还留着：$cfg"
    Write-Host '不想要了就手动删掉这个文件夹。'
}
'@
Write-Text (Join-Path $pack 'uninstall.ps1') $uninstall -Bom

# cmd 外壳保持纯 ASCII，避免代码页问题
$cmdInstall = @'
@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 echo.
echo Press any key to close...
pause >nul
'@
Write-Text (Join-Path $pack '安装.cmd') ($cmdInstall -replace "`r?`n", "`r`n")

$cmdUninstall = @'
@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0uninstall.ps1"
if errorlevel 1 echo.
echo Press any key to close...
pause >nul
'@
Write-Text (Join-Path $pack '卸载.cmd') ($cmdUninstall -replace "`r?`n", "`r`n")

# ---------- 使用说明 ----------
$readme = @"
下班倒计时 $Version —— 桌面悬浮窗，显示距离下班还有多久

安装
  1. 双击「安装.cmd」
  2. 程序会装到 %LOCALAPPDATA%\Programs\WorkCountdown，并在桌面放一个快捷方式
  3. 首次打开会问你：上班时间 / 弹性工时 / 午休 —— 填完点「开始用」

不用装运行时
  只需要 Windows 自带的 .NET Framework 4.x（Win10 / Win11 都有）。

日常用
  - 双击桌面图标启动；卡片默认贴在屏幕右边，只露一条倒计时
  - 单击收起的小横条展开成卡片，右上角箭头收回；20 秒没动会自动收回
  - 右键卡片 = 菜单：设置 / 提醒预览 / 开机自启 / 退出
  - 外观页新增柔光、粒子、极光和低动效开关；提醒停留 3–20 秒可调
  - 提醒文字独立淡入淡出，色块先后进入，不裁切字形；悬停可查看最近提醒全文
  - 设置分三页：考勤（班次/工作日/节假日）、提醒（飞书/日程，会议与日程的提前量各自独立）、外观（39 套配色）

接飞书打卡（可选）
  不接也能用，只是按班次时间倒计时。
  想读飞书里的真实打卡：
    1. 装 Node.js，然后 npm i -g lark-cli
    2. 在命令行执行 lark-cli auth login 完成授权
    3. 右键 → 设置 → 飞书对话 → 点「自动找」，找到你和考勤机器人的会话
  找不到就看看命令行里 lark-cli 有没有报错。

卸载
  双击「卸载.cmd」：删程序、删快捷方式、关掉开机自启。
  配置和日志在 %APPDATA%\WorkCountdown，不想要了自己删。

便携版
  把 WorkCountdown.exe 拷到任意文件夹，在同目录建一个空文件 portable.txt，
  配置就会存到同目录的 data 文件夹里（U 盘、无权限的电脑都能用）。

出问题
  看 %APPDATA%\WorkCountdown\widget.log。
"@
Write-Text (Join-Path $pack '使用说明.txt') ($readme -replace "`r?`n", "`r`n") -Bom

# ---------- 压缩 ----------
$zip = Join-Path $dist ("下班倒计时-v$Version.zip")
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($pack, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

Write-Host ''
Write-Host "pack  -> $pack"
Write-Host "zip   -> $zip  ($([math]::Round((Get-Item $zip).Length / 1KB, 1)) KB)"
Get-ChildItem $pack | ForEach-Object { Write-Host ("  " + $_.Name + "  " + $_.Length + " B") }
