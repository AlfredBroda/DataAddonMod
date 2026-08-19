#!/usr/bin/env python3
"""Build a DataAddonMod Workshop package in the Ostranauts Mods directory."""

from __future__ import annotations

import argparse
import json
import shutil
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_SOURCE = PROJECT_ROOT / "bin" / "Release" / "DataAddonMod.dll"
DEFAULT_PROPS = PROJECT_ROOT / "Config.Build.user.props"
DEFAULT_METADATA = PROJECT_ROOT / "mod_info.json"
DEFAULT_PREVIEW = PROJECT_ROOT / "preview.png"


def read_property(props_path: Path, property_name: str) -> str:
    try:
        root = ET.parse(props_path).getroot()
    except (ET.ParseError, OSError) as error:
        raise RuntimeError(f"Could not read {props_path}: {error}") from error

    for element in root.iter():
        if element.tag.rsplit("}", 1)[-1] == property_name:
            value = (element.text or "").strip()
            if value:
                return value

    raise RuntimeError(f"{property_name} was not found in {props_path}")


def validate_metadata(metadata_path: Path) -> None:
    try:
        with metadata_path.open(encoding="utf-8") as metadata_file:
            metadata = json.load(metadata_file)
    except (OSError, json.JSONDecodeError) as error:
        raise RuntimeError(f"Could not read metadata {metadata_path}: {error}") from error

    if not isinstance(metadata, list) or not metadata:
        raise RuntimeError(f"Metadata must be a non-empty JSON array: {metadata_path}")


def deploy(
    source: Path,
    props_path: Path,
    metadata_path: Path,
    preview_path: Path,
    dry_run: bool,
) -> Path:
    if not source.is_file():
        raise RuntimeError(
            f"Release DLL not found: {source}\n"
            "Build it first with: dotnet build --configuration Release"
        )
    if not metadata_path.is_file():
        raise RuntimeError(f"Metadata file not found: {metadata_path}")
    validate_metadata(metadata_path)
    if not preview_path.is_file():
        raise RuntimeError(
            f"Preview image not found: {preview_path}\n"
            "Add preview.png to the project or pass --preview <path>."
        )

    bepinex_dir = Path(read_property(props_path, "BepInExDir"))
    if not bepinex_dir.is_dir():
        raise RuntimeError(f"BepInExDir does not exist: {bepinex_dir}")

    game_dir = bepinex_dir.parent.parent
    package_dir = game_dir / "Ostranauts_Data" / "Mods" / "DataAddonMod"
    plugins_dir = package_dir / "plugins"
    destination = plugins_dir / source.name

    if dry_run:
        print(f"Would create package: {package_dir}")
        print(f"Would copy {metadata_path.name} -> {package_dir / 'mod_info.json'}")
        print(f"Would copy {preview_path.name} -> {package_dir / 'preview.png'}")
        print(f"Would copy {source.name} -> {destination}")
        return package_dir

    plugins_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(metadata_path, package_dir / "mod_info.json")
    shutil.copy2(preview_path, package_dir / "preview.png")
    shutil.copy2(source, destination)
    print(f"Created Workshop package at {package_dir}")
    return package_dir


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--source",
        type=Path,
        default=DEFAULT_SOURCE,
        help="Release DLL to deploy (default: bin/Release/net48/DataAddonMod.dll)",
    )
    parser.add_argument(
        "--props",
        type=Path,
        default=DEFAULT_PROPS,
        help="MSBuild props file containing BepInExDir (default: Config.Build.user.props)",
    )
    parser.add_argument(
        "--metadata",
        type=Path,
        default=DEFAULT_METADATA,
        help="Metadata source copied as mod_info.json (default: mod_info.json)",
    )
    parser.add_argument(
        "--preview",
        type=Path,
        default=DEFAULT_PREVIEW,
        help="Workshop preview image copied as preview.png (default: preview.png)",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Print the deployment path without copying the DLL",
    )
    arguments = parser.parse_args()

    try:
        deploy(
            arguments.source.resolve(),
            arguments.props.resolve(),
            arguments.metadata.resolve(),
            arguments.preview.resolve(),
            arguments.dry_run,
        )
    except RuntimeError as error:
        print(f"Error: {error}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
