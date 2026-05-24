#!/usr/bin/env python3
"""Clean Prismedia logo PNG backgrounds.

The script is intentionally dependency-free so it can run anywhere the repo can
run Python. It writes cleaned copies beside the source images by default and
never overwrites the originals unless --overwrite is passed.
"""

from __future__ import annotations

import argparse
import os
import struct
import sys
import zlib
from collections import deque
from pathlib import Path


PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
DEFAULT_INPUTS = [
    Path("apps/web-svelte/static/brand/prismedia-logo.png"),
    Path("apps/web-svelte/static/brand/prismedia-logo-nsfw.png"),
]
MODE_GRAY_FRINGE = "gray-fringe"
MODE_OUTSIDE_BLACK = "outside-black"


class PngImage:
    def __init__(
        self,
        width: int,
        height: int,
        rgba: bytearray,
        chunks_before_idat: list[tuple[bytes, bytes]],
        chunks_after_idat: list[tuple[bytes, bytes]],
    ) -> None:
        self.width = width
        self.height = height
        self.rgba = rgba
        self.chunks_before_idat = chunks_before_idat
        self.chunks_after_idat = chunks_after_idat


def parse_png(path: Path) -> PngImage:
    data = path.read_bytes()
    if not data.startswith(PNG_SIGNATURE):
        raise ValueError(f"{path} is not a PNG file")

    pos = len(PNG_SIGNATURE)
    width = height = bit_depth = color_type = None
    idat_parts: list[bytes] = []
    chunks_before_idat: list[tuple[bytes, bytes]] = []
    chunks_after_idat: list[tuple[bytes, bytes]] = []
    seen_idat = False

    while pos < len(data):
        length = struct.unpack(">I", data[pos : pos + 4])[0]
        chunk_type = data[pos + 4 : pos + 8]
        chunk_data = data[pos + 8 : pos + 8 + length]
        pos += length + 12

        if chunk_type == b"IHDR":
            (
                width,
                height,
                bit_depth,
                color_type,
                compression,
                filter_method,
                interlace,
            ) = struct.unpack(">IIBBBBB", chunk_data)
            if bit_depth != 8:
                raise ValueError(f"{path} uses bit depth {bit_depth}; only 8-bit PNGs are supported")
            if color_type not in (2, 6):
                raise ValueError(f"{path} uses color type {color_type}; only RGB/RGBA PNGs are supported")
            if compression != 0 or filter_method != 0 or interlace != 0:
                raise ValueError(f"{path} uses unsupported PNG compression/filter/interlace settings")
            continue

        if chunk_type == b"IDAT":
            seen_idat = True
            idat_parts.append(chunk_data)
            continue

        if chunk_type == b"IEND":
            break

        if seen_idat:
            chunks_after_idat.append((chunk_type, chunk_data))
        else:
            chunks_before_idat.append((chunk_type, chunk_data))

    if width is None or height is None or color_type is None:
        raise ValueError(f"{path} is missing IHDR")

    channels = 4 if color_type == 6 else 3
    raw = zlib.decompress(b"".join(idat_parts))
    stride = width * channels
    expected = height * (stride + 1)
    if len(raw) != expected:
        raise ValueError(f"{path} has unexpected decoded length {len(raw)}; expected {expected}")

    rgba = bytearray(width * height * 4)
    previous = bytearray(stride)
    offset = 0
    out_offset = 0

    for _ in range(height):
        filter_type = raw[offset]
        offset += 1
        scanline = raw[offset : offset + stride]
        offset += stride
        unfiltered = unfilter_scanline(filter_type, scanline, previous, channels)
        previous = unfiltered

        for x in range(width):
            source = x * channels
            rgba[out_offset] = unfiltered[source]
            rgba[out_offset + 1] = unfiltered[source + 1]
            rgba[out_offset + 2] = unfiltered[source + 2]
            rgba[out_offset + 3] = unfiltered[source + 3] if channels == 4 else 255
            out_offset += 4

    return PngImage(width, height, rgba, chunks_before_idat, chunks_after_idat)


def unfilter_scanline(filter_type: int, scanline: bytes, previous: bytearray, bpp: int) -> bytearray:
    result = bytearray(len(scanline))
    for i, value in enumerate(scanline):
        left = result[i - bpp] if i >= bpp else 0
        above = previous[i]
        upper_left = previous[i - bpp] if i >= bpp else 0

        if filter_type == 0:
            filtered = value
        elif filter_type == 1:
            filtered = value + left
        elif filter_type == 2:
            filtered = value + above
        elif filter_type == 3:
            filtered = value + ((left + above) // 2)
        elif filter_type == 4:
            filtered = value + paeth(left, above, upper_left)
        else:
            raise ValueError(f"Unsupported PNG filter type {filter_type}")

        result[i] = filtered & 0xFF

    return result


def paeth(left: int, above: int, upper_left: int) -> int:
    estimate = left + above - upper_left
    distance_left = abs(estimate - left)
    distance_above = abs(estimate - above)
    distance_upper_left = abs(estimate - upper_left)
    if distance_left <= distance_above and distance_left <= distance_upper_left:
        return left
    if distance_above <= distance_upper_left:
        return above
    return upper_left


def write_png(path: Path, image: PngImage) -> None:
    width = image.width
    height = image.height
    rows = bytearray()

    for y in range(height):
        rows.append(0)
        row_offset = y * width * 4
        rows.extend(image.rgba[row_offset : row_offset + width * 4])

    ihdr = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    compressed = zlib.compress(bytes(rows), level=9)

    with path.open("wb") as output:
        output.write(PNG_SIGNATURE)
        write_chunk(output, b"IHDR", ihdr)
        for chunk_type, chunk_data in image.chunks_before_idat:
            write_chunk(output, chunk_type, chunk_data)
        for start in range(0, len(compressed), 8192):
            write_chunk(output, b"IDAT", compressed[start : start + 8192])
        for chunk_type, chunk_data in image.chunks_after_idat:
            write_chunk(output, chunk_type, chunk_data)
        write_chunk(output, b"IEND", b"")


def write_chunk(output, chunk_type: bytes, chunk_data: bytes) -> None:
    output.write(struct.pack(">I", len(chunk_data)))
    output.write(chunk_type)
    output.write(chunk_data)
    output.write(struct.pack(">I", zlib.crc32(chunk_type + chunk_data) & 0xFFFFFFFF))


def clean_logo_noise(
    image: PngImage,
    gray_chroma: int,
    gray_luma_min: int,
    gray_luma_max: int,
    protect_radius: int,
    zero_rgb: bool,
) -> tuple[int, int]:
    width = image.width
    height = image.height
    total = width * height
    rgba = image.rgba
    gray_candidate = bytearray(total)
    foreground_seed = bytearray(total)

    for index in range(total):
        pixel = index * 4
        red = rgba[pixel]
        green = rgba[pixel + 1]
        blue = rgba[pixel + 2]
        alpha = rgba[pixel + 3]
        if alpha == 0:
            continue

        chroma = max(red, green, blue) - min(red, green, blue)
        luma = (77 * red + 150 * green + 29 * blue) >> 8
        is_gray = chroma <= gray_chroma and gray_luma_min <= luma <= gray_luma_max

        if is_gray:
            gray_candidate[index] = 1
        else:
            foreground_seed[index] = 1

    protected = flood_fill_protected_area(foreground_seed, width, height)
    if protect_radius > 0:
        protected = dilate(protected, width, height, protect_radius)

    removed = 0
    for index in range(total):
        if not gray_candidate[index] or protected[index]:
            continue

        pixel = index * 4
        rgba[pixel + 3] = 0
        if zero_rgb:
            rgba[pixel] = 0
            rgba[pixel + 1] = 0
            rgba[pixel + 2] = 0
        removed += 1

    return removed, sum(gray_candidate)


def clear_outside_logo_background(
    image: PngImage,
    edge_luma_min: int,
    edge_gradient_min: int,
    barrier_radius: int,
    zero_rgb: bool,
) -> int:
    """Clear only canvas-connected background outside detected logo edges."""

    width = image.width
    height = image.height
    rgba = image.rgba
    total = width * height
    barrier = detect_logo_edge_barrier(image, edge_luma_min, edge_gradient_min)
    if barrier_radius > 0:
        barrier = dilate(barrier, width, height, barrier_radius)

    outside = flood_fill_outside_background(barrier, width, height)
    removed = 0
    for index in range(total):
        if not outside[index]:
            continue

        pixel = index * 4
        if rgba[pixel + 3] == 0:
            continue

        rgba[pixel + 3] = 0
        if zero_rgb:
            rgba[pixel] = 0
            rgba[pixel + 1] = 0
            rgba[pixel + 2] = 0
        removed += 1

    return removed


def apply_alpha_mask(image: PngImage, mask: PngImage, zero_rgb: bool) -> int:
    """Copy alpha from a same-size mask image onto another logo variant."""

    if image.width != mask.width or image.height != mask.height:
        raise ValueError(
            "alpha mask dimensions do not match source "
            f"({mask.width}x{mask.height} mask for {image.width}x{image.height} image)"
        )

    changed = 0
    total = image.width * image.height
    for index in range(total):
        pixel = index * 4
        mask_alpha = mask.rgba[pixel + 3]
        if image.rgba[pixel + 3] != mask_alpha:
            changed += 1

        image.rgba[pixel + 3] = mask_alpha
        if zero_rgb and mask_alpha == 0:
            image.rgba[pixel] = 0
            image.rgba[pixel + 1] = 0
            image.rgba[pixel + 2] = 0

    return changed


def detect_logo_edge_barrier(
    image: PngImage,
    edge_luma_min: int,
    edge_gradient_min: int,
) -> bytearray:
    width = image.width
    height = image.height
    total = width * height
    lumas = bytearray(total)
    visible = bytearray(total)

    for index in range(total):
        pixel = index * 4
        alpha = image.rgba[pixel + 3]
        if alpha == 0:
            continue

        visible[index] = 1
        lumas[index] = pixel_luma(image.rgba[pixel], image.rgba[pixel + 1], image.rgba[pixel + 2])

    barrier = bytearray(total)
    for y in range(height):
        row = y * width
        for x in range(width):
            index = row + x
            if not visible[index]:
                continue

            center_luma = lumas[index]
            if center_luma >= edge_luma_min:
                barrier[index] = 1
                continue

            highest_neighbor = center_luma
            lowest_neighbor = center_luma
            for dy in (-1, 0, 1):
                ny = y + dy
                if ny < 0 or ny >= height:
                    continue
                for dx in (-1, 0, 1):
                    if dx == 0 and dy == 0:
                        continue
                    nx = x + dx
                    if nx < 0 or nx >= width:
                        continue
                    neighbor = ny * width + nx
                    if not visible[neighbor]:
                        continue
                    neighbor_luma = lumas[neighbor]
                    highest_neighbor = max(highest_neighbor, neighbor_luma)
                    lowest_neighbor = min(lowest_neighbor, neighbor_luma)

            gradient_floor = max(1, edge_luma_min - edge_gradient_min)
            if (
                center_luma >= gradient_floor
                and highest_neighbor >= edge_luma_min
                and highest_neighbor - lowest_neighbor >= edge_gradient_min
            ):
                barrier[index] = 1

    return barrier


def pixel_luma(red: int, green: int, blue: int) -> int:
    return (77 * red + 150 * green + 29 * blue) >> 8


def flood_fill_outside_background(barrier: bytearray, width: int, height: int) -> bytearray:
    total = width * height
    outside = bytearray(total)
    queue: deque[int] = deque()

    def enqueue(index: int) -> None:
        if barrier[index] or outside[index]:
            return
        outside[index] = 1
        queue.append(index)

    last_row = (height - 1) * width
    for x in range(width):
        enqueue(x)
        enqueue(last_row + x)
    for y in range(height):
        row = y * width
        enqueue(row)
        enqueue(row + width - 1)

    while queue:
        index = queue.popleft()
        x = index % width
        y = index // width

        if x > 0:
            enqueue(index - 1)
        if x < width - 1:
            enqueue(index + 1)
        if y > 0:
            enqueue(index - width)
        if y < height - 1:
            enqueue(index + width)

    return outside


def flood_fill_protected_area(seed: bytearray, width: int, height: int) -> bytearray:
    total = width * height
    outside = bytearray(total)
    queue: deque[int] = deque()

    def enqueue(index: int) -> None:
        if seed[index] or outside[index]:
            return
        outside[index] = 1
        queue.append(index)

    last_row = (height - 1) * width
    for x in range(width):
        enqueue(x)
        enqueue(last_row + x)
    for y in range(height):
        row = y * width
        enqueue(row)
        enqueue(row + width - 1)

    while queue:
        index = queue.popleft()
        x = index % width
        y = index // width

        if x > 0:
            enqueue(index - 1)
        if x < width - 1:
            enqueue(index + 1)
        if y > 0:
            enqueue(index - width)
        if y < height - 1:
            enqueue(index + width)

    protected = bytearray(total)
    for index in range(total):
        if not outside[index]:
            protected[index] = 1

    return protected


def dilate(mask: bytearray, width: int, height: int, radius: int) -> bytearray:
    result = bytearray(mask)
    offsets = [
        (dx, dy)
        for dy in range(-radius, radius + 1)
        for dx in range(-radius, radius + 1)
        if dx * dx + dy * dy <= radius * radius
    ]

    for y in range(height):
        row = y * width
        for x in range(width):
            index = row + x
            if not mask[index]:
                continue
            for dx, dy in offsets:
                nx = x + dx
                ny = y + dy
                if 0 <= nx < width and 0 <= ny < height:
                    result[ny * width + nx] = 1

    return result


def destination_for(source: Path, suffix: str, overwrite: bool) -> Path:
    if overwrite:
        return source
    return source.with_name(f"{source.stem}{suffix}{source.suffix}")


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", nargs="*", type=Path, default=DEFAULT_INPUTS)
    parser.add_argument(
        "--mode",
        choices=(MODE_GRAY_FRINGE, MODE_OUTSIDE_BLACK),
        default=MODE_GRAY_FRINGE,
        help="cleanup mode to apply",
    )
    parser.add_argument("--suffix", default="-cleaned", help="suffix for cleaned copies")
    parser.add_argument("--gray-chroma", type=int, default=60, help="max RGB channel spread treated as gray")
    parser.add_argument("--gray-luma-min", type=int, default=35, help="minimum luma treated as removable gray")
    parser.add_argument("--gray-luma-max", type=int, default=235, help="maximum luma treated as removable gray")
    parser.add_argument("--protect-radius", type=int, default=0, help="pixels to preserve around the foreground mask")
    parser.add_argument("--edge-luma-min", type=int, default=26, help="minimum luma treated as a logo edge")
    parser.add_argument("--edge-gradient-min", type=int, default=10, help="local luma contrast treated as an edge")
    parser.add_argument("--barrier-radius", type=int, default=2, help="pixels to preserve around detected edges")
    parser.add_argument("--alpha-mask", type=Path, help="copy alpha from a same-size PNG instead of detecting edges")
    parser.add_argument("--overwrite", action="store_true", help="replace sources instead of creating copies")
    parser.add_argument("--keep-rgb", action="store_true", help="leave RGB values under cleared alpha")
    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    alpha_mask = parse_png(args.alpha_mask) if args.alpha_mask else None
    for source in args.inputs:
        image = parse_png(source)
        if alpha_mask is not None:
            changed = apply_alpha_mask(image, alpha_mask, zero_rgb=not args.keep_rgb)
            summary = f"copied alpha mask; changed {changed:,} pixels"
        elif args.mode == MODE_OUTSIDE_BLACK:
            removed = clear_outside_logo_background(
                image,
                edge_luma_min=args.edge_luma_min,
                edge_gradient_min=args.edge_gradient_min,
                barrier_radius=args.barrier_radius,
                zero_rgb=not args.keep_rgb,
            )
            summary = f"cleared {removed:,} outside-background pixels"
        else:
            removed, candidates = clean_logo_noise(
                image,
                gray_chroma=args.gray_chroma,
                gray_luma_min=args.gray_luma_min,
                gray_luma_max=args.gray_luma_max,
                protect_radius=args.protect_radius,
                zero_rgb=not args.keep_rgb,
            )
            summary = f"removed {removed:,}/{candidates:,} gray-edge pixels"

        destination = destination_for(source, args.suffix, args.overwrite)
        write_png(destination, image)
        print(f"{source} -> {destination} {summary}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
