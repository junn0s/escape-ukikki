#!/usr/bin/env python3
"""Prepare transparent RX-9 common fixtures and a visual QA sheet."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw


ASSET_SPECS = {
    "S_WallMonitor.png": ((512, 320), (20, 18)),
    "S_FireExtinguisher.png": ((256, 384), (18, 20)),
    "S_TrashBin.png": ((256, 256), (16, 16)),
    "S_EmergencyPhone.png": ((256, 384), (18, 20)),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--wall-monitor-source", type=Path, required=True)
    parser.add_argument("--fire-extinguisher-source", type=Path, required=True)
    parser.add_argument("--trash-bin-source", type=Path, required=True)
    parser.add_argument("--emergency-phone-source", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    parser.add_argument("--floor-tile", type=Path)
    parser.add_argument("--wall-tile", type=Path)
    return parser.parse_args()


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A")
    mask = alpha.point(lambda value: 255 if value >= 8 else 0)
    bbox = mask.getbbox()
    if bbox is None:
        raise ValueError("Source contains no visible pixels.")
    return bbox


def fit_sprite(
    source_path: Path,
    target_size: tuple[int, int],
    padding: tuple[int, int],
) -> Image.Image:
    with Image.open(source_path) as source:
        rgba = source.convert("RGBA")
        subject = rgba.crop(alpha_bbox(rgba))

    maximum = (
        target_size[0] - padding[0] * 2,
        target_size[1] - padding[1] * 2,
    )
    scale = min(maximum[0] / subject.width, maximum[1] / subject.height)
    resized = subject.resize(
        (
            max(1, round(subject.width * scale)),
            max(1, round(subject.height * scale)),
        ),
        Image.Resampling.LANCZOS,
    )
    result = Image.new("RGBA", target_size, (0, 0, 0, 0))
    result.alpha_composite(
        resized,
        ((target_size[0] - resized.width) // 2, (target_size[1] - resized.height) // 2),
    )
    return result


def build_background(
    floor_tile_path: Path | None,
    wall_tile_path: Path | None,
) -> Image.Image:
    canvas = Image.new("RGBA", (1536, 900), (17, 24, 32, 255))
    if floor_tile_path is not None and floor_tile_path.is_file():
        with Image.open(floor_tile_path) as source:
            tile = source.convert("RGB").resize((256, 256), Image.Resampling.LANCZOS)
        for y in range(350, canvas.height, tile.height):
            for x in range(0, canvas.width, tile.width):
                canvas.paste(tile, (x, y))

    if wall_tile_path is not None and wall_tile_path.is_file():
        with Image.open(wall_tile_path) as source:
            wall = source.convert("RGB").resize((512, 350), Image.Resampling.LANCZOS)
        for x in range(0, canvas.width, wall.width):
            canvas.paste(wall, (x, 0))

    veil = Image.new("RGBA", canvas.size, (5, 12, 18, 70))
    canvas.alpha_composite(veil)
    return canvas


def draw_mount_shadow(
    canvas: Image.Image,
    box: tuple[int, int, int, int],
    radius: int,
) -> None:
    overlay = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(overlay).rounded_rectangle(
        box,
        radius=radius,
        fill=(0, 0, 0, 70),
    )
    canvas.alpha_composite(overlay)


def make_preview(
    sprites: dict[str, Image.Image],
    output_path: Path,
    floor_tile_path: Path | None,
    wall_tile_path: Path | None,
) -> None:
    canvas = build_background(floor_tile_path, wall_tile_path)
    placements = {
        "S_WallMonitor.png": ((90, 80), (512, 320)),
        "S_FireExtinguisher.png": ((680, 58), (256, 384)),
        "S_EmergencyPhone.png": ((1050, 58), (256, 384)),
        "S_TrashBin.png": ((640, 535), (320, 320)),
    }
    draw_mount_shadow(canvas, (70, 70, 630, 430), 45)
    draw_mount_shadow(canvas, (660, 50, 956, 460), 45)
    draw_mount_shadow(canvas, (1030, 50, 1326, 460), 45)
    draw_mount_shadow(canvas, (610, 515, 990, 875), 75)

    for name, (position, target_size) in placements.items():
        preview_sprite = sprites[name].resize(target_size, Image.Resampling.LANCZOS)
        canvas.alpha_composite(preview_sprite, position)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.convert("RGB").save(output_path, format="PNG", optimize=True)


def validate_sprite(name: str, image: Image.Image) -> None:
    if image.mode != "RGBA":
        raise ValueError(f"{name} must be RGBA.")
    corners = (
        image.getpixel((0, 0))[3],
        image.getpixel((image.width - 1, 0))[3],
        image.getpixel((0, image.height - 1))[3],
        image.getpixel((image.width - 1, image.height - 1))[3],
    )
    if any(corners):
        raise ValueError(f"{name} must have transparent corners: {corners}")
    if image.getchannel("A").getbbox() is None:
        raise ValueError(f"{name} contains no visible pixels.")


def main() -> None:
    args = parse_args()
    sources = {
        "S_WallMonitor.png": args.wall_monitor_source,
        "S_FireExtinguisher.png": args.fire_extinguisher_source,
        "S_TrashBin.png": args.trash_bin_source,
        "S_EmergencyPhone.png": args.emergency_phone_source,
    }
    sprites = {
        name: fit_sprite(sources[name], *ASSET_SPECS[name])
        for name in ASSET_SPECS
    }
    args.output_directory.mkdir(parents=True, exist_ok=True)
    for name, sprite in sprites.items():
        validate_sprite(name, sprite)
        sprite.save(args.output_directory / name, format="PNG", optimize=True)

    make_preview(
        sprites,
        args.preview,
        args.floor_tile,
        args.wall_tile,
    )


if __name__ == "__main__":
    main()
