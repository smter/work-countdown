# 打开原型（双击或在终端里跑）：只是用一个静态页面回答设计问题。
$path = Join-Path $PSScriptRoot "index.html"
if (-not (Test-Path $path)) { Write-Host "找不到 index.html" -ForegroundColor Red; exit 1 }
Start-Process $path
Write-Host "已在默认浏览器打开原型：$path"
