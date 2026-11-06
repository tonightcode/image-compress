"""Render the existing rectangle-based SVG brand into Windows ICO and PNG assets.

Uses only Python's standard library. Keep letsmall.svg as the editable source.
"""
from pathlib import Path
import struct
import zlib
import xml.etree.ElementTree as ET


ASSETS = Path(__file__).resolve().parents[1] / "src" / "LetSmall" / "Assets"


def pixels(size):
    shapes = []
    for rect in ET.parse(ASSETS / "letsmall.svg").getroot():
        color = bytes.fromhex(rect.attrib["fill"].lstrip("#")) + b"\xff"
        bounds = [float(rect.attrib[key]) for key in ("x", "y", "width", "height")]
        shapes.append((bounds, color))
    result = bytearray()
    for y in range(size):
        for x in range(size):
            samples = []
            for sy in range(4):
                for sx in range(4):
                    px, py = (x + (sx + .5) / 4) * 256 / size, (y + (sy + .5) / 4) * 256 / size
                    color = b"\x00" * 4
                    for (left, top, width, height), fill in shapes:
                        if left <= px < left + width and top <= py < top + height:
                            color = fill
                    samples.append(color)
            alpha = sum(c[3] for c in samples)
            result.extend(round(sum(c[i] * c[3] for c in samples) / alpha) if alpha else 0 for i in range(3))
            result.append(round(alpha / len(samples)))
    return bytes(result)


def dib(size, rgba):
    image = bytearray()
    mask = bytearray()
    stride = ((size + 31) // 32) * 4
    for y in reversed(range(size)):
        row_mask = bytearray(stride)
        for x in range(size):
            r, g, b, a = rgba[(y * size + x) * 4: (y * size + x + 1) * 4]
            image.extend((b, g, r, a))
            if not a:
                row_mask[x // 8] |= 0x80 >> (x % 8)
        mask.extend(row_mask)
    header = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, len(image), 0, 0, 0, 0)
    return header + image + mask


def chunk(kind, data):
    return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))


def main():
    sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
    images = [dib(size, pixels(size)) for size in sizes]
    offset = 6 + 16 * len(sizes)
    directory = bytearray(struct.pack("<HHH", 0, 1, len(sizes)))
    for size, image in zip(sizes, images):
        directory.extend(struct.pack("<BBBBHHII", size % 256, size % 256, 0, 0, 1, 32, len(image), offset))
        offset += len(image)
    (ASSETS / "letsmall.ico").write_bytes(directory + b"".join(images))
    rgba = pixels(256)
    rows = b"".join(b"\x00" + rgba[y * 1024: (y + 1) * 1024] for y in range(256))
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 256, 256, 8, 6, 0, 0, 0))
    (ASSETS / "letsmall.png").write_bytes(png + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b""))
    print("Generated letsmall.ico (9 sizes) and letsmall.png from letsmall.svg")


if __name__ == "__main__":
    main()
