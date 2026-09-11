# -*- coding: utf-8 -*-
"""Генератор растрового шрифта для WorldBox.

Берёт системный TTF, растрирует нужные символы в 1-битные глифы высотой CELL
и печатает готовые данные для C# (base64). Бинарных ассетов в репозитории нет —
в коде лежит только компактная строка, шрифт собирается в текстуру при старте.

Запуск: python3 tools/gen_font.py [номер_варианта]
В репозитории лежит вариант 0 (9 пикселей, порог 110). Варианты 10 и 11 пикселей
проверку глазами не прошли: у них теряются штрихи у Б, Г, Ц.
Нужен Pillow и fc-match. Результат — строки CHARS и BLOB для
`src/WorldBox.Render/Text/PixelFontData.cs`.
"""
import base64
import os
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont

CELL = 9        # строк в глифе
BASELINE = 7    # базовая линия (снизу 2 строки под выносные элементы)
MAXW = 8        # максимальная ширина глифа в пикселях (1 байт на строку)
OUT_DIR = os.environ.get("WB_FONT_OUT", "/tmp/wb_font")

DIGITS = "0123456789"
LATIN = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
CYR_U = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ"
CYR_L = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя"
PUNCT = " !\"#%&'()*+,-./:;<=>?@[\\]^_{|}~°×№—"
CHARS = DIGITS + LATIN + CYR_U + CYR_L + PUNCT


def find_font():
    wanted = ["DejaVu Sans", "Liberation Sans", "Noto Sans", "Verdana", "Arial"]
    for name in wanted:
        try:
            path = subprocess.run(
                ["fc-match", "-f", "%{file}", name],
                capture_output=True, text=True, timeout=10,
            ).stdout.strip()
        except Exception:
            continue
        if path and os.path.exists(path):
            return name, path
    raise SystemExit("шрифт не найден")


def render(font, ch, threshold):
    img = Image.new("L", (MAXW + 6, CELL + 4), 0)
    d = ImageDraw.Draw(img)
    d.text((2, BASELINE + 1), ch, font=font, fill=255, anchor="ls")
    px = img.load()
    rows = []
    for y in range(CELL):
        rows.append([1 if px[x + 2, y + 1] >= threshold else 0 for x in range(MAXW + 2)])
    return rows


def trim(rows):
    cols = [x for x in range(len(rows[0])) if any(r[x] for r in rows)]
    if not cols:
        return [[0] * MAXW for _ in rows], 3
    left, right = min(cols), max(cols)
    out = []
    for r in rows:
        cut = r[left:right + 1][:MAXW]
        out.append(cut + [0] * (MAXW - len(cut)))
    return out, min(MAXW, right - left + 2)


def build(size, threshold, path):
    font = ImageFont.truetype(path, size)
    glyphs = {}
    for ch in CHARS:
        rows, w = trim(render(font, ch, threshold))
        if ch == " ":
            rows = [[0] * MAXW for _ in range(CELL)]
            w = 3
        glyphs[ch] = (rows, w)
    return glyphs


def pack(glyphs):
    blob = bytearray()
    for ch in CHARS:
        rows, w = glyphs[ch]
        blob.append(w)
        for r in rows:
            b = 0
            for x in range(MAXW):
                if r[x]:
                    b |= 1 << (7 - x)
            blob.append(b)
    return base64.b64encode(bytes(blob)).decode("ascii")


def draw_text(img, glyphs, x, y, text, scale, color):
    d = ImageDraw.Draw(img)
    cx = x
    for ch in text:
        if ch not in glyphs:
            cx += 4 * scale
            continue
        rows, w = glyphs[ch]
        for gy in range(CELL):
            for gx in range(MAXW):
                if rows[gy][gx]:
                    d.rectangle(
                        [cx + gx * scale, y + gy * scale,
                         cx + gx * scale + scale - 1, y + gy * scale + scale - 1],
                        fill=color,
                    )
        cx += (w + 1) * scale
    return cx


SAMPLES = [
    "ФПС 60  кадр 3.1 мс  тик 20 Гц",
    "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ",
    "абвгдеёжзийклмнопрстуфхцчшщъыьэюя",
    "Новый мир  Пауза  x1 x4 x16 x64  0123456789",
    "Глубокий океан  Тайга  Селитра  12 480 — 3%",
]


def preview(variants, out):
    pad, line = 14, 40
    height = pad * 2 + line * len(SAMPLES) * len(variants) + 30 * len(variants)
    img = Image.new("RGB", (1180, height), (25, 25, 25))
    d = ImageDraw.Draw(img)
    y = pad
    for title, glyphs in variants:
        d.text((pad, y), title, fill=(120, 200, 255))
        y += 22
        for s in SAMPLES:
            draw_text(img, glyphs, pad, y, s, 3, (235, 235, 235))
            y += line
        y += 8
    img.save(out)


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    name, path = find_font()
    print("шрифт:", name, path)
    variants = []
    for size, thr in ((9, 110), (10, 110), (11, 120)):
        variants.append(("%s %dpx thr%d" % (name, size, thr), build(size, thr, path)))
    preview(variants, os.path.join(OUT_DIR, "font_preview.png"))
    pick = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    blob = pack(variants[pick][1])
    with open(os.path.join(OUT_DIR, "font_b64.txt"), "w") as f:
        f.write(blob)
    print("вариант:", variants[pick][0])
    print("символов:", len(CHARS), "байт:", len(base64.b64decode(blob)), "base64:", len(blob))
    print("CHARS=" + CHARS)
    print("BLOB=" + blob)
