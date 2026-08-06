#!/usr/bin/env python3
"""Prepare RX-9 world signage sprites and a visual QA sheet."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


PANEL_SIZE = 256
PANEL_BORDER = 64
GUIDE_SIZE = (512, 128)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--panel-source", type=Path, required=True)
    parser.add_argument(
        "--guide-source",
        type=Path,
        required=True,
        help="RGBA guide source after chroma-key removal",
    )
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    parser.add_argument("--font", type=Path)
    return parser.parse_args()


def crop_square(image: Image.Image) -> Image.Image:
    side = min(image.size)
    left = (image.width - side) // 2
    top = (image.height - side) // 2
    return image.crop((left, top, left + side, top + side))


def build_panel(source_path: Path, output_path: Path) -> Image.Image:
    with Image.open(source_path) as source:
        panel = crop_square(source.convert("RGB")).resize(
            (PANEL_SIZE, PANEL_SIZE),
            Image.Resampling.LANCZOS,
        )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    panel.save(output_path, format="PNG", optimize=True)
    return panel


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A")
    mask = alpha.point(lambda value: 255 if value >= 8 else 0)
    bbox = mask.getbbox()
    if bbox is None:
        raise ValueError("Guide source contains no visible pixels.")
    return bbox


def build_guide(source_path: Path, output_path: Path) -> Image.Image:
    with Image.open(source_path) as source:
        subject = source.convert("RGBA").crop(alpha_bbox(source.convert("RGBA")))

    maximum = (GUIDE_SIZE[0] - 32, GUIDE_SIZE[1] - 20)
    scale = min(maximum[0] / subject.width, maximum[1] / subject.height)
    resized = subject.resize(
        (
            max(1, round(subject.width * scale)),
            max(1, round(subject.height * scale)),
        ),
        Image.Resampling.LANCZOS,
    )
    guide = Image.new("RGBA", GUIDE_SIZE, (0, 0, 0, 0))
    guide.alpha_composite(
        resized,
        ((GUIDE_SIZE[0] - resized.width) // 2, (GUIDE_SIZE[1] - resized.height) // 2),
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    guide.save(output_path, format="PNG", optimize=True)
    return guide


def resize_nine_slice(
    source: Image.Image,
    output_size: tuple[int, int],
) -> Image.Image:
    output_width, output_height = output_size
    if output_width < PANEL_BORDER * 2 or output_height < PANEL_BORDER * 2:
        raise ValueError("Nine-slice output must fit both fixed borders.")

    source_stops = (0, PANEL_BORDER, PANEL_SIZE - PANEL_BORDER, PANEL_SIZE)
    target_stops_x = (0, PANEL_BORDER, output_width - PANEL_BORDER, output_width)
    target_stops_y = (0, PANEL_BORDER, output_height - PANEL_BORDER, output_height)
    result = Image.new(source.mode, output_size)
    for row in range(3):
        for column in range(3):
            source_box = (
                source_stops[column],
                source_stops[row],
                source_stops[column + 1],
                source_stops[row + 1],
            )
            target_box = (
                target_stops_x[column],
                target_stops_y[row],
                target_stops_x[column + 1],
                target_stops_y[row + 1],
            )
            target_size = (
                target_box[2] - target_box[0],
                target_box[3] - target_box[1],
            )
            patch = source.crop(source_box)
            if patch.size != target_size:
                patch = patch.resize(target_size, Image.Resampling.BICUBIC)
            result.paste(patch, target_box[:2])
    return result


def load_font(font_path: Path | None, size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    if font_path is not None and font_path.is_file():
        return ImageFont.truetype(str(font_path), size=size)
    return ImageFont.load_default()


def draw_centered_label(
    canvas: Image.Image,
    bounds: tuple[int, int, int, int],
    text: str,
    font: ImageFont.FreeTypeFont | ImageFont.ImageFont,
) -> None:
    draw = ImageDraw.Draw(canvas)
    center_x = (bounds[0] + bounds[2]) // 2
    center_y = (bounds[1] + bounds[3]) // 2
    draw.text(
        (center_x + 3, center_y + 4),
        text,
        font=font,
        fill=(3, 8, 12),
        anchor="mm",
    )
    draw.text(
        (center_x, center_y),
        text,
        font=font,
        fill=(236, 246, 248),
        anchor="mm",
    )


def make_preview(
    panel: Image.Image,
    guide: Image.Image,
    output_path: Path,
    font_path: Path | None,
) -> None:
    canvas = Image.new("RGBA", (1536, 900), (24, 33, 41, 255))
    room_sign = resize_nine_slice(panel, (760, 190)).convert("RGBA")
    door_sign = resize_nine_slice(panel, (480, 150)).convert("RGBA")
    canvas.alpha_composite(room_sign, (90, 78))
    canvas.alpha_composite(door_sign, (930, 98))
    draw_centered_label(canvas, (90, 78, 850, 268), "중앙 보안실", load_font(font_path, 64))
    draw_centered_label(canvas, (930, 98, 1410, 248), "격리실 A", load_font(font_path, 48))

    guide_large = guide.resize((640, 160), Image.Resampling.LANCZOS)
    canvas.alpha_composite(guide_large, (120, 380))
    canvas.alpha_composite(guide_large.transpose(Image.Transpose.FLIP_LEFT_RIGHT), (776, 380))
    guide_vertical_source = guide.resize((400, 100), Image.Resampling.LANCZOS)
    guide_vertical = guide_vertical_source.rotate(
        90,
        expand=True,
        resample=Image.Resampling.BICUBIC,
    )
    canvas.alpha_composite(guide_vertical, (555, 480))
    canvas.alpha_composite(
        guide_vertical.transpose(Image.Transpose.FLIP_TOP_BOTTOM),
        (875, 480),
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output_path, format="PNG", optimize=True)


def main() -> None:
    args = parse_args()
    panel = build_panel(
        args.panel_source,
        args.output_directory / "T_RoomSignPanel.png",
    )
    guide = build_guide(
        args.guide_source,
        args.output_directory / "S_FloorGuideDecal.png",
    )
    make_preview(panel, guide, args.preview, args.font)


if __name__ == "__main__":
    main()
