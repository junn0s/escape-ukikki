#!/usr/bin/env python3
"""Convert wall concepts into Unity-ready nine-slice sprites and a QA preview."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw


TARGET_SIZE = 256
BORDER_PIXELS = 64


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--section-source", type=Path, required=True)
    parser.add_argument("--face-source", type=Path, required=True)
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


def make_preview(
    section: Image.Image,
    face: Image.Image,
    output_path: Path,
) -> None:
    background = (25, 35, 43)
    preview = Image.new("RGB", (1536, 1024), background)
    draw = ImageDraw.Draw(preview)

    horizontal_section = resize_nine_slice(section, (1240, 164))
    vertical_section = resize_nine_slice(section, (164, 650))
    stretched_face = resize_nine_slice(face, (1120, 486))

    preview.paste(horizontal_section, (148, 58))
    preview.paste(vertical_section, (58, 302))
    preview.paste(stretched_face, (300, 342))

    # Thin neutral ground strips make the top cap/front-face relationship clear.
    draw.rectangle((300, 828, 1420, 844), fill=(12, 18, 24))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    preview.save(output_path, format="PNG", optimize=True)


def main() -> None:
    args = parse_args()
    section = build_sprite(
        args.section_source,
        args.output_directory / "T_WallSection.png",
    )
    face = build_sprite(
        args.face_source,
        args.output_directory / "T_WallFace.png",
    )
    make_preview(section, face, args.preview)


if __name__ == "__main__":
    main()
