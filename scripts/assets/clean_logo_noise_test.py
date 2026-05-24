from __future__ import annotations

import importlib.util
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("clean-logo-noise.py")
SPEC = importlib.util.spec_from_file_location("clean_logo_noise", SCRIPT)
assert SPEC is not None
clean_logo_noise = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(clean_logo_noise)


class ClearOutsideLogoBackgroundTests(unittest.TestCase):
    def test_clears_edge_connected_black_and_keeps_enclosed_black(self) -> None:
        image = clean_logo_noise.PngImage(
            width=7,
            height=7,
            rgba=bytearray([0, 0, 0, 255] * 49),
            chunks_before_idat=[],
            chunks_after_idat=[],
        )

        gold = (220, 160, 70, 255)
        for x in range(2, 5):
            set_pixel(image, x, 2, gold)
            set_pixel(image, x, 4, gold)
        for y in range(2, 5):
            set_pixel(image, 2, y, gold)
            set_pixel(image, 4, y, gold)

        cleared = clean_logo_noise.clear_outside_logo_background(
            image,
            edge_luma_min=80,
            edge_gradient_min=25,
            barrier_radius=0,
            zero_rgb=True,
        )

        self.assertEqual(cleared, 40)
        self.assertEqual(pixel(image, 0, 0), (0, 0, 0, 0))
        self.assertEqual(pixel(image, 1, 3), (0, 0, 0, 0))
        self.assertEqual(pixel(image, 3, 3), (0, 0, 0, 255))
        self.assertEqual(pixel(image, 2, 2), gold)

    def test_applies_alpha_from_matching_mask_without_changing_color(self) -> None:
        image = clean_logo_noise.PngImage(
            width=2,
            height=1,
            rgba=bytearray([10, 20, 30, 255, 200, 120, 50, 255]),
            chunks_before_idat=[],
            chunks_after_idat=[],
        )
        mask = clean_logo_noise.PngImage(
            width=2,
            height=1,
            rgba=bytearray([0, 0, 0, 0, 255, 255, 255, 255]),
            chunks_before_idat=[],
            chunks_after_idat=[],
        )

        changed = clean_logo_noise.apply_alpha_mask(image, mask, zero_rgb=True)

        self.assertEqual(changed, 1)
        self.assertEqual(pixel(image, 0, 0), (0, 0, 0, 0))
        self.assertEqual(pixel(image, 1, 0), (200, 120, 50, 255))


def set_pixel(image, x: int, y: int, rgba: tuple[int, int, int, int]) -> None:
    offset = (y * image.width + x) * 4
    image.rgba[offset : offset + 4] = bytes(rgba)


def pixel(image, x: int, y: int) -> tuple[int, int, int, int]:
    offset = (y * image.width + x) * 4
    return tuple(image.rgba[offset : offset + 4])


if __name__ == "__main__":
    unittest.main()
