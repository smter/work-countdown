# -*- coding: utf-8 -*-
"""字体子集流水线：从 tmp-res 的完整 ttf 裁出计时/正文两款子集。

用法: python tools/make-font-subsets.py
前提: pip install fonttools
"""
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TMP = ROOT / "tmp-res"
FONTS = ROOT / "fonts"

MAPLE = TMP / "MapleMono-NF-CN-ExtraBold.ttf"
SARASA = TMP / "SarasaMonoSC-SemiBold.ttf"

# 计时子集：ASCII 全集 + 读数符号（数字、冒号、h/m/s、各种短横/en dash、箭头）
TIMER_TEXT = (
    "".join(chr(c) for c in range(0x20, 0x7F))
    + "-\u2010\u2011\u2012\u2013\u2014"  # 各种短横/en dash
    + "\u2192"                              # →
)

# 正文子集额外符号（源码/界面符号；含任务指定的 ↻ ⚙ › ● ▔ 等）
EXTRA_TEXT = "\u00b7\u2192\u21bb\u2699\u203a\u25cf\u2580\u2594\u2013\u2026\u00d7"              "\u3001\u3002\u300c\u300d\uff01\uff08\uff09\uff0c\uff1a\uff1b\uff1f\uff5e"              "０１２３４５６７８９"

ASCII_TEXT = "".join(chr(c) for c in range(0x20, 0x7F))

# GB2312 常用汉字（一二级全部汉字）
def gb2312_chars():
    out = []
    for hi in range(0xB0, 0xF8):
        for lo in range(0xA1, 0xFF):
            try:
                ch = bytes([hi, lo]).decode("gb2312")
            except UnicodeDecodeError:
                continue
            out.append(ch)
    return "".join(out)


def scan_source_non_ascii():
    """扫描 src/ 全部源码字符串里的非 ASCII 字符。"""
    chars = set()
    for f in sorted((ROOT / "src").glob("*.cs")):
        text = f.read_text(encoding="utf-8-sig", errors="replace")
        chars.update(c for c in text if ord(c) > 0x7F and c not in "\r\n\t")
    return "".join(sorted(chars))


def subset(src: Path, dst: Path, text: str, drop_hinting: bool):
    FONTS.mkdir(exist_ok=True)
    cmd = [
        sys.executable, "-m", "fontTools.subset", str(src),
        f"--text={text}",
        f"--output-file={str(dst)}",
        "--name-IDs=*", "--name-legacy",
        "--layout-features=*",
        "--glyph-names",
        "--notdef-outline",
        "--recommended-glyphs",
    ]
    if drop_hinting:
        cmd.append("--no-hinting")
    subprocess.run(cmd, check=True)


def main():
    src_extra = scan_source_non_ascii()
    body_text = ASCII_TEXT + EXTRA_TEXT + src_extra + gb2312_chars()

    timer_out = FONTS / "MapleMono-NF-CN-ExtraBold-subset.ttf"
    body_out = FONTS / "SarasaMonoSC-SemiBold-subset.ttf"

    subset(MAPLE, timer_out, TIMER_TEXT, drop_hinting=False)
    subset(SARASA, body_out, body_text, drop_hinting=True)

    for p in (timer_out, body_out):
        print(f"{p.relative_to(ROOT)}: {p.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
