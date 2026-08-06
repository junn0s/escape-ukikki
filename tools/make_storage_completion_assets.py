#!/usr/bin/env python3
"""Prepare storage completion and vaccine-room props with a QA sheet."""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw


ASSET_SPECS = {
    "S_ColdShelf.png": ((256, 512), (14, 18)),
    "S_InsulatedPallet.png": ((448, 352), (18, 16)),
    "S_FrostDrain.png": ((512, 192), (12, 18)),
    "S_VialRack.png": ((256, 576), (14, 18)),
    "S_SterileBench.png": ((768, 256), (20, 14)),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cold-shelf-source", type=Path, required=True)
    parser.add_argument("--pallet-source", type=Path, required=True)
    parser.add_argument("--drain-source", type=Path, required=True)
    parser.add_argument("--vial-rack-source", type=Path, required=True)
    parser.add_argument("--sterile-bench-source", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    parser.add_argument("--floor-tile", type=Path)
    parser.add_argument("--wall-tile", type=Path)
    return parser.parse_args()


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").point(
        lambda value: 255 if value >= 8 else 0
    ).getbbox()
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
        (
            (target_size[0] - resized.width) // 2,
            (target_size[1] - resized.height) // 2,
        ),
    )
    return result


def build_background(
    floor_tile_path: Path | None,
    wall_tile_path: Path | None,
) -> Image.Image:
    canvas = Image.new("RGBA", (1800, 1000), (17, 24, 32, 255))
    if floor_tile_path is not None and floor_tile_path.is_file():
        with Image.open(floor_tile_path) as source:
            tile = source.convert("RGB").resize(
                (256, 256), Image.Resampling.LANCZOS
            )
        for y in range(330, canvas.height, tile.height):
            for x in range(0, canvas.width, tile.width):
                canvas.paste(tile, (x, y))

    if wall_tile_path is not None and wall_tile_path.is_file():
        with Image.open(wall_tile_path) as source:
            wall = source.convert("RGB").resize(
                (512, 330), Image.Resampling.LANCZOS
            )
        for x in range(0, canvas.width, wall.width):
            canvas.paste(wall, (x, 0))

    canvas.alpha_composite(Image.new("RGBA", canvas.size, (2, 28, 42, 45)))
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
        fill=(0, 0, 0, 68),
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
        "S_ColdShelf.png": ((65, 80), (256, 512)),
        "S_InsulatedPallet.png": ((370, 310), (448, 352)),
        "S_FrostDrain.png": ((280, 735), (512, 192)),
        "S_VialRack.png": ((930, 55), (256, 576)),
        "S_SterileBench.png": ((980, 680), (768, 256)),
    }
    shadow_boxes = (
        ((35, 50, 350, 625), 44),
        ((335, 275, 850, 700), 52),
        ((245, 700, 825, 965), 40),
        ((895, 25, 1220, 665), 44),
        ((945, 645, 1780, 970), 48),
    )
    for box, radius in shadow_boxes:
        draw_shadow(canvas, box, radius)
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
        "S_ColdShelf.png": args.cold_shelf_source,
        "S_InsulatedPallet.png": args.pallet_source,
        "S_FrostDrain.png": args.drain_source,
        "S_VialRack.png": args.vial_rack_source,
        "S_SterileBench.png": args.sterile_bench_source,
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
