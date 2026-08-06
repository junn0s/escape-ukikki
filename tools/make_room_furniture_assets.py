#!/usr/bin/env python3
"""Prepare transparent RX-9 room furniture and a visual QA sheet."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw


ASSET_SPECS = {
    "S_LabWorkbench.png": ((512, 256), (18, 16)),
    "S_StorageCabinet.png": ((256, 512), (16, 20)),
    "S_ReagentShelf.png": ((256, 512), (16, 20)),
    "S_RollingCart.png": ((384, 256), (18, 16)),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--workbench-source", type=Path, required=True)
    parser.add_argument("--cabinet-source", type=Path, required=True)
    parser.add_argument("--shelf-source", type=Path, required=True)
    parser.add_argument("--cart-source", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    parser.add_argument("--floor-tile", type=Path)
    parser.add_argument("--wall-tile", type=Path)
    return parser.parse_args()


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    alpha = image.getchannel("A")
    bbox = alpha.point(lambda value: 255 if value >= 8 else 0).getbbox()
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
        for y in range(300, canvas.height, tile.height):
            for x in range(0, canvas.width, tile.width):
                canvas.paste(tile, (x, y))

    if wall_tile_path is not None and wall_tile_path.is_file():
        with Image.open(wall_tile_path) as source:
            wall = source.convert("RGB").resize((512, 300), Image.Resampling.LANCZOS)
        for x in range(0, canvas.width, wall.width):
            canvas.paste(wall, (x, 0))

    canvas.alpha_composite(Image.new("RGBA", canvas.size, (5, 12, 18, 64)))
    return canvas


def draw_shadow(
    canvas: Image.Image,
    box: tuple[int, int, int, int],
    radius: int,
) -> None:
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).rounded_rectangle(
        box,
        radius=radius,
        fill=(0, 0, 0, 72),
    )
    canvas.alpha_composite(layer)


def make_preview(
    sprites: dict[str, Image.Image],
    output_path: Path,
    floor_tile_path: Path | None,
    wall_tile_path: Path | None,
) -> None:
    canvas = build_background(floor_tile_path, wall_tile_path)
    placements = {
        "S_LabWorkbench.png": ((70, 160), (640, 320)),
        "S_StorageCabinet.png": ((760, 70), (240, 480)),
        "S_ReagentShelf.png": ((1080, 70), (240, 480)),
        "S_RollingCart.png": ((575, 590), (384, 256)),
    }
    draw_shadow(canvas, (50, 145, 730, 500), 55)
    draw_shadow(canvas, (740, 55, 1020, 570), 55)
    draw_shadow(canvas, (1060, 55, 1340, 570), 55)
    draw_shadow(canvas, (550, 570, 985, 870), 60)
    for name, (position, target_size) in placements.items():
        sprite = sprites[name].resize(target_size, Image.Resampling.LANCZOS)
        canvas.alpha_composite(sprite, position)

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
        "S_LabWorkbench.png": args.workbench_source,
        "S_StorageCabinet.png": args.cabinet_source,
        "S_ReagentShelf.png": args.shelf_source,
        "S_RollingCart.png": args.cart_source,
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
