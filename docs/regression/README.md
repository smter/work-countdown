# 视觉回归：字体切换（Consolas / Microsoft YaHei UI → Maple Mono NF CN ExtraBold / Sarasa Mono SC SemiBold）

工单 #5，2026-11-05。基线为切换后新字体的全量离屏渲染，对照图左半为旧字体观感
（编译时去掉内嵌资源，走三级回退得到旧族），右半为新字体基线。

## 基线

- `test/baseline/`：633 张 PNG，约 6.7 MB，由 `VisualCheck.exe <outdir> all` 生成。
  覆盖 3 DPI（100/150/200%）× 4 特效（none/glow/particles/aurora）× 收起/展开 × 会议/日程 × 12 动画时刻（576 帧），
  加每组合 idle（24）与低动效 quiet（24），另深浅主题（midnight / ivory）× 低动效开关 × 提醒/空闲（8）。
  长文本提醒（三行 detail、长标题、带括号地点）在全部组合中天然覆盖。
- 命名：`<dpi>-<effect>-<col|exp>-<mtg|evt>-<moment>.png`、`...-idle.png`、`...-quiet.png`、`theme-<id>-<quiet|motion>-<reminder|idle>.png`。
- 重新生成：`csc`（带字体 `/res`）编译 `test/VisualCheck.cs` 后运行 `VisualCheck.exe test\baseline all`。

## 对照图（docs/regression/）

| 文件 | 内容 |
| --- | --- |
| reminder-expanded-150.png | 展开提醒 150%（长标题省略号 + 时间段计时字体） |
| reminder-expanded-200.png | 展开提醒 200%（字形锐度） |
| reminder-event-mid-150.png | 日程提醒动画中段（4200ms） |
| card-idle-expanded-150.png | 主卡空闲 150%（大数字 + 说明行） |
| card-idle-collapsed-150.png | 折叠条 150% |
| quiet-placeholder.png | 100% 低动效占位帧 |
| review/theme-*.png | 深浅主题 × 低动效复查件（新基线直拷） |

## 结论清单

1. **溢出 / 裁切：无。** 全部 633 组合未见文字出界或被裁；长标题仍走省略号 + FitFont 缩字号，时间段行完整。
2. **错位：无。** 图标、进度条、行基线与旧版一致，换族未引起布局漂移。
3. **糊字：无。** 200% 下 ExtraBold 大数字与 SemiBold 正文边缘锐利；150% 无锯齿堆积。
4. **换族宽度变化：可控。** 更纱等宽正文比微软雅黑略宽，但说明行（如「已工作 1h24m · 应勤 9h00m」）在三种 DPI 下均单行放得下，未触发额外换行或截断；大数字 28pt Maple Mono 宽度与旧 Consolas 接近，FitFont 缩字号路径未被触发（无触发即无回归）。
5. **低动效 / 深浅主题：无问题。** quiet 占位帧、midnight / ivory 空闲与提醒均正常，浅色底上深字对比充足。
6. **观察项（低严重度，非阻塞）：100%（96dpi）下 8.5–10pt SemiBold 说明行比旧版微软雅黑 Regular 观感略「重」，小字号密度稍高。** 无笔画粘连，可读性无损；属字体设计取舍，接受。若后续想减轻，可在工厂里为 ≤9pt 的 Body 角色单独换 Light/Regular 字重——需要新子集，超出本工单范围。
7. **层次是否糊（工单关注点）：不糊。** 正文变重后，半粗等宽在小字号下的字形仍清晰可辨（见 150%/200% 对照图右侧），且主数字 ExtraBold 与正文的层次对比反而比旧版（Consolas + 雅黑合成 Bold）更清晰。合成加粗移除后未出现发糊。
