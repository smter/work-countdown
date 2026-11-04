from pathlib import Path
from PIL import Image
import shutil
root=Path(__file__).resolve().parents[1]
frames=[Image.open(p).convert('RGB') for p in sorted((root/'test/visual').glob('demo-*.png'))]
frames[0].save(root/'design-preview.gif',save_all=True,append_images=frames[1:],duration=50,loop=0,optimize=False)
for f in frames:f.close()
for src,dst in [('compact.png','final-collapsed.png'),('expanded.png','final-expanded.png'),('glow-event-1200.png','reminder.png'),('glow-meeting-1200.png','reminder-meeting.png'),('appearance.png','settings-effects.png')]:
    shutil.copyfile(root/'test/visual'/src,root/dst)
p=root/'README.md';t=p.read_text(encoding='utf-8-sig')
t=t.replace('收起成窄条时，时/分下方也会多一行 `休 1h30m`。','收起成横条时，右上角显示 `休 1h30m`。')
t=t.replace('程序带 DPI 感知清单，按你的实际缩放比例绘制，**不会被系统二次拉伸**——你是 150%，所以卡片实际以 381×258 物理像素渲染（不含午休行时 381×225），字号、圆角、图标、间距一起放大，文字保持清晰。','程序使用系统 DPI 感知清单。150% 系统缩放下，展开卡片为 420×315 物理像素（不含午休行时 420×279），横条为 336×102；点单位字体与布局分别按 DPI 渲染，避免重复放大。测试覆盖 100% / 150% / 200% 的离屏渲染；混合 DPI 显示器间移动仍采用系统 DPI，可能由 Windows 缩放，并未宣称逐显示器适配。')
old=next(line for line in t.splitlines() if line.startswith('- **怎么响**'))
t=t.replace(old,'- **怎么响**：旧文字整体淡出，第一块色块完成后第二块再进入；背景就位后，新文字独立淡入，不跟着色块被裁切。默认完整展示 6 秒（可调 3–20 秒）；文字先淡出，第二块再退场、第一块随后退场，最后正常卡片文字完整淡入。默认总时长约 8 秒。会议用深色文字配橙黄底；日程用浅色文字配主题的深色双层底。')
t=t.replace('（71 条，全绿）','（97 条，全绿；另有 600 个离屏渲染组合）')
insert='''## 视觉与动效（v1.1）

- 中文使用 Microsoft YaHei UI；倒计时使用 Consolas 等宽数字。主数字 28pt，展开提醒标题 17pt，辅助文字 8.5–10pt。
- 展开卡片侧边距 18 DIP，横条 14 DIP；数字和进度条分区，色块分界线不穿过字形。
- 长标题固定可读字号，超出显示省略号。最近提醒的完整标题、时间、地点和备注可通过悬停查看。
- 设置 → 外观：无特效 / 柔光 / 粒子 / 极光。粒子只在左右边缘运行；柔光和极光位于文字背后。平时不为装饰运行动画计时器。
- 「减少动态效果」关闭扫入、粒子和移动光晕，保留静态提醒与停留时间。Windows 高对比度模式也会停止这些动效（不等于完整的系统高对比度配色适配）。
- 提醒期间暂停收起/展开，避免动画过程中改变文本布局。

![完整提醒时序](design-preview.gif)

![动效设置](settings-effects.png)

验证：97 条逻辑断言；600 个离屏渲染组合（3 种 DPI、4 种特效、收起/展开、会议/日程、进退场边界、长文本与低动效）。截图是程序真实绘制输出，但本轮模型不支持图片输入，未声称做过人工视觉验收。

'''
t=t.replace('## 显示与缩放',insert+'## 显示与缩放')
p.write_text(t,encoding='utf-8')
p=root/'package.ps1';t=p.read_text(encoding='utf-8-sig').replace("[string]$Version = '1.0'","[string]$Version = '1.1'").replace('右上角 x 收回','右上角箭头收回').replace('  - 右键卡片 = 菜单：设置 / 提醒预览 / 开机自启 / 退出','  - 右键卡片 = 菜单：设置 / 提醒预览 / 开机自启 / 退出\n  - 外观页新增柔光、粒子、极光和低动效开关；提醒停留 3–20 秒可调\n  - 提醒文字独立淡入淡出，色块先后进入，不裁切字形；悬停可查看最近提醒全文')
p.write_text(t,encoding='utf-8-sig')
print('Preview GIF, documentation and packaging guide updated.')
