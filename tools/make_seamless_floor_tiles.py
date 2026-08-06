#!/usr/bin/env python3
"""Convert image-generation floor concepts into Unity-ready repeat tiles."""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageOps


TARGET_SIZE = 512
EDGE_BLEND_PIXELS = 48


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--room-source", type=Path, required=True)
    parser.add_argument("--corridor-source", type=Path, required=True)
    parser.add_argument("--output-directory", type=Path, required=True)
    parser.add_argument("--preview", type=Path, required=True)
    return parser.parse_args()


def crop_square(image: Image.Image) -> Image.Image:
    side = min(image.size)
    left = (image.width - side) // 2
    top = (image.height - side) // 2
    return image.crop((left, top, left + side, top + side))


def normalize_grayscale(image: Image.Image) -> Image.Image:
    grayscale = ImageOps.grayscale(image)
    grayscale = ImageOps.autocontrast(grayscale, cutoff=0.5)
    values = np.asarray(grayscale, dtype=np.float32) / 255.0

    # The runtime multiplies room colors over this texture. Keep the material
    # mostly light while retaining dark seams and bolts.
    values = 0.40 + np.power(values, 0.88) * 0.58
    return Image.fromarray(
        np.clip(np.rint(values * 255.0), 0, 255).astype(np.uint8),
        mode="L",
    ).convert("RGB")


def make_edges_periodic(image: Image.Image) -> Image.Image:
    pixels = np.asarray(image, dtype=np.float32).copy()
    height, width, _ = pixels.shape
    margin = min(EDGE_BLEND_PIXELS, width // 4, height // 4)

    source = pixels.copy()
    for distance in range(margin):
        blend = distance / (margin - 1)
        left = source[:, distance, :]
        right = source[:, width - 1 - distance, :]
        average = (left + right) * 0.5
        pixels[:, distance, :] = average * (1.0 - blend) + left * blend
        pixels[:, width - 1 - distance, :] = (
            average * (1.0 - blend) + right * blend
        )

    source = pixels.copy()
    for distance in range(margin):
        blend = distance / (margin - 1)
        bottom = source[distance, :, :]
        top = source[height - 1 - distance, :, :]
        average = (bottom + top) * 0.5
        pixels[distance, :, :] = average * (1.0 - blend) + bottom * blend
        pixels[height - 1 - distance, :, :] = (
            average * (1.0 - blend) + top * blend
        )

    result = Image.fromarray(
        np.clip(np.rint(pixels), 0, 255).astype(np.uint8),
        mode="RGB",
    )
    result_pixels = np.asarray(result)
    if not np.array_equal(result_pixels[:, 0], result_pixels[:, -1]):
        raise RuntimeError("Left and right tile edges do not match.")
    if not np.array_equal(result_pixels[0, :], result_pixels[-1, :]):
        raise RuntimeError("Top and bottom tile edges do not match.")
    return result


def build_tile(source_path: Path, output_path: Path) -> Image.Image:
    with Image.open(source_path) as source:
        square = crop_square(source.convert("RGB"))
        resized = square.resize(
            (TARGET_SIZE, TARGET_SIZE),
            Image.Resampling.LANCZOS,
        )
    tile = make_edges_periodic(normalize_grayscale(resized))
    output_path.parent.mkdir(parents=True, exist_ok=True)
    tile.save(output_path, format="PNG", optimize=True)
    return tile


def make_preview(room: Image.Image, corridor: Image.Image, path: Path) -> None:
    preview_tile_size = 256
    preview = Image.new("RGB", (preview_tile_size * 6, preview_tile_size * 3))
    for tile_index, tile in enumerate((room, corridor)):
        resized = tile.resize(
            (preview_tile_size, preview_tile_size),
            Image.Resampling.LANCZOS,
        )
        x_offset = tile_index * preview_tile_size * 3
        for row in range(3):
            for column in range(3):
                preview.paste(
                    resized,
                    (
                        x_offset + column * preview_tile_size,
                        row * preview_tile_size,
                    ),
                )
    path.parent.mkdir(parents=True, exist_ok=True)
    preview.save(path, format="PNG", optimize=True)


def main() -> None:
    args = parse_args()
    room = build_tile(
        args.room_source,
        args.output_directory / "T_FloorTile_Room.png",
    )
    corridor = build_tile(
        args.corridor_source,
        args.output_directory / "T_FloorTile_Corridor.png",
    )
    make_preview(room, corridor, args.preview)


if __name__ == "__main__":
    main()
