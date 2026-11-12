#!/usr/bin/env python3
"""Generates the GIF and TIFF test images of ImageFormatTests.

The images are written by Pillow and ImageMagick, i.e. by encoders that are independent of PDFsharp.
For each image, <file>.expected.gz contains the expected pixels with 8 bits per sample: RGBA, or CMYK for CMYK images.
manifest.json lists the images with size, expected PDFsharp image format, and expected pixels.

Requires Python 3 with Pillow (with libtiff) and ImageMagick 6 (convert). Run in this folder:
    python3 generate.py
"""
import gzip
import json
import os
import subprocess

from PIL import Image

W, H = 37, 23  # Odd sizes, so that rows need padding.
manifest = []


def rgb_image(w=W, h=H):
    img = Image.new('RGB', (w, h))
    img.putdata([((x * 7 + y * 3) % 256, (y * 11) % 256, (x * y * 5) % 256) for y in range(h) for x in range(w)])
    return img


def rgba_image():
    img = rgb_image().convert('RGBA')
    img.putalpha(Image.frombytes('L', (W, H), bytes(
        0 if (x + y) % 9 == 0 else 255 if (x + y) % 5 == 0 else (x * 13 + y * 29) % 256
        for y in range(H) for x in range(W))))
    return img


def gray_image(step=1):
    img = Image.new('L', (W, H))
    img.putdata([((x * 7 + y * 5) % 256) // step * step for y in range(H) for x in range(W)])
    return img


def bilevel_image(w=50, h=21):
    img = Image.new('1', (w, h))
    img.putdata([255 if ((x // 3 + y // 2) % 2 == 0) != ((x - 25) ** 2 + (y - 10) ** 2 < 64) else 0
                 for y in range(h) for x in range(w)])
    return img


def write_expected(file, img, mode='RGBA'):
    data = img.convert(mode).tobytes()
    with gzip.GzipFile(file + '.expected.gz', 'wb', mtime=0) as f:
        f.write(data)


def add(file, expected_img, pdfsharp_format, mode='RGBA'):
    """Registers an image. expected_img is what the file must decode to."""
    write_expected(file, expected_img, mode)
    manifest.append({'file': file, 'width': expected_img.width, 'height': expected_img.height,
                     'format': pdfsharp_format, 'mode': mode})


def magick(source, target, *options):
    subprocess.run(['convert', source, *options, target], check=True)


def tag(file, number):
    """Returns a TIFF tag; a tuple with one value is returned as that value."""
    value = Image.open(file).tag_v2.get(number)
    return value[0] if isinstance(value, tuple) and len(value) == 1 else value


def gif_image_flags(file):
    """Returns the flags of the first image descriptor of a GIF file."""
    with open(file, 'rb') as f:
        data = f.read()
    pos = 13 + (3 << ((data[10] & 7) + 1) if data[10] & 0x80 else 0)
    while data[pos] == 0x21:  # Skip extensions.
        pos += 2
        while data[pos]:
            pos += data[pos] + 1
        pos += 1
    assert data[pos] == 0x2C
    return data[pos + 9]


# Sources for ImageMagick.
rgb_image().save('src-rgb.png')
rgba_image().save('src-rgba.png')
gray_image().save('src-gray.png')
gray_image(17).save('src-gray4.png')  # Values that 4 bits represent exactly.
bilevel_image().convert('L').save('src-bilevel.png')
rgb_image(40, 35).save('src-rgb-40x35.png')

# GIF.
p256 = rgb_image().quantize(256, dither=Image.Dither.NONE)
p256.save('gif-palette256.gif', interlace=False)
add('gif-palette256.gif', p256, 'Indexed8')

p256.save('gif-interlaced.gif', interlace=True)
assert gif_image_flags('gif-interlaced.gif') & 0x40, 'GIF is not interlaced'
add('gif-interlaced.gif', p256, 'Indexed8')

p16 = rgb_image().quantize(16, dither=Image.Dither.NONE)
p16.save('gif-palette16.gif')
add('gif-palette16.gif', Image.open('gif-palette16.gif'), 'Indexed8')

bilevel_image().save('gif-bilevel.gif')
add('gif-bilevel.gif', Image.open('gif-bilevel.gif'), 'Indexed8')

p16.save('gif-transparent.gif', transparency=3)
add('gif-transparent.gif', Image.open('gif-transparent.gif'), 'Indexed8')

second = rgb_image().transpose(Image.Transpose.FLIP_LEFT_RIGHT).quantize(16, dither=Image.Dither.NONE)
p16.save('gif-animated.gif', save_all=True, append_images=[second], duration=100, loop=0)
add('gif-animated.gif', p16, 'Indexed8')

# An image smaller than the logical screen: the rest of the screen is transparent.
subprocess.run(['convert', '-size', '20x10', 'xc:#ff0000', '-page', '40x30+5+7', 'gif-offset.gif'], check=True)
offset = Image.new('RGBA', (40, 30), (0, 0, 0, 0))
offset.paste((255, 0, 0, 255), (5, 7, 25, 17))
add('gif-offset.gif', offset, 'Indexed8')

# TIFF written by Pillow (libtiff).
for compression, suffix in [(None, 'none'), ('tiff_lzw', 'lzw'), ('tiff_adobe_deflate', 'deflate'), ('packbits', 'packbits')]:
    rgb_image().save(f'tiff-rgb-{suffix}.tif', compression=compression)
    add(f'tiff-rgb-{suffix}.tif', rgb_image(), 'Bgr32')

rgba_image().save('tiff-rgba.tif', compression='tiff_lzw')
assert tag('tiff-rgba.tif', 338) == 2, 'expected unassociated alpha'
add('tiff-rgba.tif', rgba_image(), 'Bgra32')

gray_image().save('tiff-gray8.tif', compression='tiff_lzw')
add('tiff-gray8.tif', gray_image(), 'Gray8')

bilevel_image().save('tiff-bilevel.tif')
add('tiff-bilevel.tif', bilevel_image(), 'Indexed1')

p256.save('tiff-palette8.tif', compression='tiff_lzw')
add('tiff-palette8.tif', p256, 'Indexed8')

cmyk = rgb_image().convert('CMYK')
cmyk.save('tiff-cmyk.tif', compression='tiff_lzw')
add('tiff-cmyk.tif', cmyk, 'Cmyk32', 'CMYK')

rgb_image().save('tiff-multipage.tif', save_all=True, append_images=[gray_image()], compression='tiff_lzw')
add('tiff-multipage.tif', rgb_image(), 'Bgr32')

# CCITT: written to PDF without decoding.
for compression, suffix in [('group4', 'g4'), ('group3', 'g3'), ('tiff_ccitt', 'mh')]:
    bilevel_image().save(f'tiff-{suffix}.tif', compression=compression)
    add(f'tiff-{suffix}.tif', bilevel_image(), 'CcittFax')

# TIFF written by ImageMagick.
magick('src-rgb.png', 'tiff-rgb-lzw-predictor.tif', '-compress', 'LZW', '-define', 'tiff:predictor=2')
add('tiff-rgb-lzw-predictor.tif', rgb_image(), 'Bgr32')

magick('src-rgb.png', 'tiff-rgb16-deflate-predictor.tif', '-depth', '16', '-compress', 'Zip', '-define', 'tiff:predictor=2')
assert tag('tiff-rgb16-deflate-predictor.tif', 258) == (16, 16, 16)
add('tiff-rgb16-deflate-predictor.tif', rgb_image(), 'Bgr32')

magick('src-gray.png', 'tiff-gray16.tif', '-depth', '16', '-compress', 'LZW')
add('tiff-gray16.tif', gray_image(), 'Gray8')

magick('src-gray4.png', 'tiff-gray4.tif', '-depth', '4', '-compress', 'None')
assert tag('tiff-gray4.tif', 258) in (4, (4,))
add('tiff-gray4.tif', gray_image(17), 'Gray8')

magick('src-rgb.png', 'tiff-palette4.tif', '+dither', '-colors', '16', '-type', 'Palette', '-depth', '4', '-compress', 'None')
assert tag('tiff-palette4.tif', 262) == 3 and tag('tiff-palette4.tif', 258) in (4, (4,))
add('tiff-palette4.tif', Image.open('tiff-palette4.tif'), 'Indexed4')

magick('src-rgb-40x35.png', 'tiff-tiled.tif', '-compress', 'LZW', '-define', 'tiff:tile-geometry=16x16')
assert tag('tiff-tiled.tif', 322) == 16
add('tiff-tiled.tif', rgb_image(40, 35), 'Bgr32')

magick('src-rgb.png', 'tiff-planar.tif', '-interlace', 'plane', '-compress', 'LZW')
assert tag('tiff-planar.tif', 284) == 2
add('tiff-planar.tif', rgb_image(), 'Bgr32')

magick('src-rgb.png', 'tiff-bigendian.tif', '-define', 'tiff:endian=msb', '-compress', 'LZW')
with open('tiff-bigendian.tif', 'rb') as f:
    assert f.read(2) == b'MM'
add('tiff-bigendian.tif', rgb_image(), 'Bgr32')

magick('src-rgba.png', 'tiff-rgba-associated.tif', '-define', 'tiff:alpha=associated', '-compress', 'LZW')
assert tag('tiff-rgba-associated.tif', 338) in (1, (1,))
add('tiff-rgba-associated.tif', rgba_image(), 'Bgra32')

magick('src-bilevel.png', 'tiff-bilevel-miniswhite.tif', '-type', 'bilevel', '-depth', '1', '-define', 'tiff:photometric=min-is-white', '-compress', 'None')
assert tag('tiff-bilevel-miniswhite.tif', 262) == 0 and tag('tiff-bilevel-miniswhite.tif', 258) == 1
add('tiff-bilevel-miniswhite.tif', bilevel_image(), 'Indexed1')

magick('src-bilevel.png', 'tiff-g4-lsb.tif', '-type', 'bilevel', '-compress', 'Group4', '-define', 'tiff:fill-order=lsb')
assert tag('tiff-g4-lsb.tif', 266) == 2
add('tiff-g4-lsb.tif', bilevel_image(), 'CcittFax')

for source in ['src-rgb.png', 'src-rgba.png', 'src-gray.png', 'src-gray4.png', 'src-bilevel.png', 'src-rgb-40x35.png']:
    os.remove(source)

with open('manifest.json', 'w') as f:
    json.dump(manifest, f, indent=2)
print(f'{len(manifest)} images')
