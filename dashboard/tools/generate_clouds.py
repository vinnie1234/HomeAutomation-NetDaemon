"""Genereert de wolkenafbeeldingen voor de weer-hero op Home (www/clouds/*.webp).

Elke wolk is fractale ruis (fBm) binnen een wolkvorm, belicht van boven met zelfschaduw:
hoe meer wolk er boven een punt zit, hoe donkerder dat punt. Zo krijg je een heldere bovenkant,
een grijze onderkant en rafelige randen, zoals bij echte wolken.

Gebruik (numpy en Pillow nodig):  python generate_clouds.py <uitvoermap> [voorbeeld.png]
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

SCALE = 2  # eerst op dubbele grootte rekenen, daarna verkleinen voor zachte randen


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / (edge1 - edge0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def value_noise(height, width, cell, rng):
    """Vloeiende ruis: een grof raster van toevalsgetallen, bicubisch opgeschaald."""
    grid = rng.random((height // cell + 3, width // cell + 3)).astype(np.float32)
    image = Image.fromarray(grid, mode="F").resize((grid.shape[1] * cell, grid.shape[0] * cell), Image.BICUBIC)
    return np.asarray(image)[:height, :width]


def fbm(height, width, cell, octaves, rng, gain=0.5):
    """Fractale ruis: meerdere lagen ruis, steeds fijner en zwakker."""
    total = np.zeros((height, width), np.float32)
    amplitude, norm = 1.0, 0.0
    for _ in range(octaves):
        total += amplitude * value_noise(height, width, max(2, cell), rng)
        norm += amplitude
        amplitude *= gain
        cell //= 2
    return total / norm


def soften(values, factor):
    """Vervagen door te verkleinen en weer bicubisch te vergroten (werkt ook op kommagetallen)."""
    image = Image.fromarray(values.astype(np.float32), mode="F")
    small = image.resize((max(1, image.width // factor), max(1, image.height // factor)), Image.BILINEAR)
    return np.asarray(small.resize(image.size, Image.BICUBIC))


def light(alpha, depth_scale, ambient, billows=None):
    """Belichting van boven (iets van links):
    - zelfschaduw: hoe meer wolk erboven, hoe donkerder (optische diepte op een vervaagde dichtheid, dus zonder strepen);
    - oppervlak: kanten die naar boven wijzen zijn helder, de onderkant donkerder;
    - billows: ribbelruis voor het bloemkool-reliëf binnen in de wolk."""
    soft = soften(alpha, 16 * SCALE)
    depth = np.cumsum(soft, axis=0) / (alpha.shape[0] * depth_scale)
    depth = 0.7 * depth + 0.3 * np.roll(depth, alpha.shape[1] // 30, axis=1)
    surface = soften(alpha, 12 * SCALE)
    facing_up = np.clip(0.55 + np.gradient(surface, axis=0) * alpha.shape[0] * 0.07, 0.3, 1)
    shade = 0.62 * np.exp(-depth * 2.0) + 0.38 * facing_up
    if billows is not None:
        shade *= 0.74 + 0.42 * billows
    return np.clip(ambient + (1 - ambient) * shade, 0, 1)


def edge_window(h, w, margin=0.07):
    """Laat alles naar de randen van de afbeelding uitlopen, zodat er nooit een harde rand zichtbaar is."""
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    x /= w
    y /= h
    return (smoothstep(0, margin, x) * smoothstep(1, 1 - margin, x)
            * smoothstep(0, margin, y) * smoothstep(1, 1 - margin, y))


def render(alpha, shade, highlight, shadow):
    """Kleur uit de belichting; dunne randen worden iets lichter (zilverrand)."""
    shade = np.clip(shade + (1 - alpha) * 0.06, 0, 1)[..., None]
    rgb = np.asarray(shadow, np.float32) + (np.asarray(highlight, np.float32) - shadow) * shade
    rgba = np.dstack([rgb, alpha * 255]).clip(0, 255).astype(np.uint8)
    image = Image.fromarray(rgba, "RGBA")
    a = image.getchannel("A").filter(ImageFilter.GaussianBlur(1.2 * SCALE))
    image.putalpha(a)
    return image.resize((image.width // SCALE, image.height // SCALE), Image.LANCZOS)


def cumulus(seed, width=900, height=460):
    """Stapelwolk: een koepel van veel zachte klonten (bloemkool), vlakke onderkant, rafelige randen."""
    rng = np.random.default_rng(seed)
    w, h = width * SCALE, height * SCALE
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    x /= w
    y /= h
    # Vervormde coördinaten geven organische vormen in plaats van ronde bollen
    x = x + (fbm(h, w, 120 * SCALE, 5, rng) - 0.5) * 0.06
    y = y + (fbm(h, w, 120 * SCALE, 5, rng) - 0.5) * 0.06

    base = 0.76
    dome = 0.34 + rng.random() * 0.14
    shape = np.zeros((h, w), np.float32)
    for _ in range(34):
        cx = 0.14 + rng.random() * 0.72
        profile = max(0.0, 1 - ((cx - 0.5) / 0.38) ** 2) ** 0.7  # hoe hoog de koepel op deze plek is
        top = base - dome * profile
        cy = top + rng.random() * (base - top)
        r = 0.035 + (0.05 + 0.07 * profile) * rng.random() ** 0.6
        shape += np.exp(-(((x - cx) / (r * 0.5)) ** 2 + ((y - cy) / r) ** 2))
    shape = 1 - np.exp(-shape * 1.3)  # zachte vereniging van de klonten
    shape *= smoothstep(base + 0.1, base - 0.05, y)  # vlakke, zachte onderkant

    noise = fbm(h, w, 140 * SCALE, 7, rng)
    detail = fbm(h, w, 36 * SCALE, 5, rng)
    erosion = 1 - shape  # randen rafelen meer dan het midden
    density = shape - 0.28 + (noise - 0.5) * (0.35 + 0.5 * erosion) + (detail - 0.5) * 0.22
    alpha = smoothstep(0.0, 0.42, density) * edge_window(h, w)
    billows = (1 - np.abs(2 * fbm(h, w, 64 * SCALE, 4, rng) - 1)) ** 1.5  # ribbelruis: bolle klonten
    shade = light(alpha, 0.24, 0.34, billows)
    return render(alpha, shade, (255, 254, 250), (140, 156, 184))


def stratus(seed, width=1600, height=420):
    """Wolkendek: brede, vlakkere band voor bewolkt weer."""
    rng = np.random.default_rng(seed)
    w, h = width * SCALE, height * SCALE
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    x /= w
    y /= h
    shape = np.exp(-((y - 0.45) / 0.22) ** 2) * smoothstep(0.0, 0.18, x) * smoothstep(1.0, 0.82, x)
    noise = fbm(h, w, 260 * SCALE, 7, rng)
    detail = fbm(h, w, 60 * SCALE, 5, rng)
    density = shape * 1.15 - 0.3 + (noise - 0.5) * 0.9 + (detail - 0.5) * 0.3
    alpha = smoothstep(0.0, 0.45, density) * 0.95 * edge_window(h, w, 0.12)
    billows = 1 - np.abs(2 * fbm(h, w, 90 * SCALE, 4, rng) - 1)
    shade = light(alpha, 0.4, 0.42, billows)
    return render(alpha, shade, (246, 248, 252), (148, 160, 180))


def preview(images, path):
    """Voorbeeld: alle wolken op een blauwe lucht, om het resultaat te beoordelen."""
    sky = Image.new("RGB", (1600, 640))
    top, bottom = np.array([40, 110, 205]), np.array([160, 208, 245])
    for row in range(sky.height):
        color = tuple(int(c) for c in top + (bottom - top) * row / sky.height)
        sky.paste(color, (0, row, sky.width, row + 1))
    spots = [(-60, 260), (380, 20), (820, 230), (1180, 40), (60, -30), (700, 330), (-200, 400)]
    for (name, image), (px, py) in zip(images.items(), spots):
        sky.paste(image, (px, py), image)
    sky.save(path)


def main():
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    images = {f"cumulus_{i + 1}": cumulus(seed) for i, seed in enumerate([7, 21, 42, 77, 101])}
    images |= {f"stratus_{i + 1}": stratus(seed) for i, seed in enumerate([5, 13])}
    for name, image in images.items():
        image.save(out / f"{name}.webp", "WEBP", quality=88, method=6)
        print(f"{name}.webp  {image.size[0]}x{image.size[1]}  {(out / f'{name}.webp').stat().st_size // 1024} kB")
    if len(sys.argv) > 2:
        preview(images, sys.argv[2])


if __name__ == "__main__":
    main()
