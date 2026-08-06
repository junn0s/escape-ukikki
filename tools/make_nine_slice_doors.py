#!/usr/bin/env python3
"""Convert automatic-door concepts into Unity nine-slice sprites and QA art."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw


TARGET_SIZE = 256
BORDER_PIXELS = 64


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--panel-source", type=Path, required=True)
    parser.add_argument("--frame-source", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    return parser.parse_args()


def crop_square(image: Image.Image) -> Image.Image:
    side = min(image.size)
    left = (image.width - side) // 2
    top = (image.height - side) // 2
    return image.crop((left, top, left + side, top + side))


def build_sprite(source_path: Path, output_path: Path) -> Image.Image:
    with Image.open(source_path) as source:
        sprite = crop_square(source.convert("RGB")).resize(
            (TARGET_SIZE, TARGET_SIZE),
            Image.Resampling.LANCZOS,
        )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sprite.save(output_path, format="PNG", optimize=True)
    return sprite


def resize_nine_slice(
    source: Image.Image,
    output_size: tuple[int, int],
) -> Image.Image:
    output_width, output_height = output_size
    border = BORDER_PIXELS
    if output_width < border * 2 or output_height < border * 2:
        raise ValueError("Nine-slice output must fit both fixed borders.")

    source_stops = (0, border, TARGET_SIZE - border, TARGET_SIZE)
    target_stops_x = (0, border, output_width - border, output_width)
    target_stops_y = (0, border, output_height - border, output_height)
    result = Image.new("RGB", output_size)
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


def draw_horizontal_door(
    canvas: Image.Image,
    panel: Image.Image,
    frame: Image.Image,
    origin: tuple[int, int],
) -> None:
    x, y = origin
    frame_post = resize_nine_slice(frame, (156, 282))
    panel_half = resize_nine_slice(panel, (500, 164))
    canvas.paste(panel_half, (x + 132, y + 59))
    canvas.paste(panel_half, (x + 632, y + 59))
    canvas.paste(frame_post, (x, y))
    canvas.paste(frame_post, (x + 1108, y))
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle(
        (x + 64, y + 87, x + 92, y + 195),
        radius=12,
        fill=(52, 198, 200),
    )
    draw.rounded_rectangle(
        (x + 1172, y + 87, x + 1200, y + 195),
        radius=12,
        fill=(52, 198, 200),
    )


def draw_vertical_door(
    canvas: Image.Image,
    panel: Image.Image,
    frame: Image.Image,
    origin: tuple[int, int],
) -> None:
    x, y = origin
    frame_post = resize_nine_slice(frame, (282, 156))
    panel_half = resize_nine_slice(panel, (164, 300))
    canvas.paste(panel_half, (x + 59, y + 132))
    canvas.paste(panel_half, (x + 59, y + 432))
    canvas.paste(frame_post, (x, y))
    canvas.paste(frame_post, (x, y + 708))
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle(
        (x + 87, y + 64, x + 195, y + 92),
        radius=12,
        fill=(52, 198, 200),
    )
    draw.rounded_rectangle(
        (x + 87, y + 772, x + 195, y + 800),
        radius=12,
        fill=(52, 198, 200),
    )


def make_preview(
    panel: Image.Image,
    frame: Image.Image,
    output_path: Path,
) -> None:
    canvas = Image.new("RGB", (1600, 1024), (25, 35, 43))
    draw_horizontal_door(canvas, panel, frame, (276, 96))
    draw_vertical_door(canvas, panel, frame, (112, 410))

    # Open-state sample: both leaves slide away from the center seam.
    frame_post = resize_nine_slice(frame, (132, 238))
    panel_half = resize_nine_slice(panel, (310, 142))
    canvas.paste(frame_post, (520, 596))
    canvas.paste(panel_half, (652, 644))
    canvas.paste(panel_half, (1172, 644))
    canvas.paste(frame_post, (1482, 596))
    ImageDraw.Draw(canvas).rectangle(
        (962, 644, 1172, 786),
        fill=(12, 18, 24),
    )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, format="PNG", optimize=True)


def main() -> None:
    args = parse_args()
    panel = build_sprite(
        args.panel_source,
        args.output_directory / "T_DoorPanel.png",
    )
    frame = build_sprite(
        args.frame_source,
        args.output_directory / "T_DoorFrame.png",
    )
    make_preview(panel, frame, args.preview)


if __name__ == "__main__":
    main()
