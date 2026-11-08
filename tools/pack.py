#!/usr/bin/env python3
"""Build both Jellyfin ABIs and refresh manifest.json.

Jellyfin installs the highest version whose targetAbi is at or below the server.
The Jellyfin 12 package is therefore one revision above the 10.11 package.
"""

import argparse
import hashlib
import json
import subprocess
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "Jellyfin.Plugin.WatchSignal"
DIST = ROOT / "dist"
MANIFEST = ROOT / "manifest.json"
REPO = "Myxelium/WatchSignal"
GUID = "79801685-27ac-495f-a917-11298fe58dce"

OVERVIEW = "Shows when the next episode or season airs on the series page."
DESCRIPTION = (
    "Adds one line to the series page with the next episode air date. "
    "When that release is the premiere of a season, the line says when the season premieres. "
    "Dates come from Sonarr when the show is in your Sonarr library, and from TVMaze otherwise."
)
CHANGELOG = (
    "Show the next episode on the series page. "
    "When the next release is a new season, say when that season premieres."
)


def md5(path: Path) -> str:
    digest = hashlib.md5()
    digest.update(path.read_bytes())
    return digest.hexdigest()


def package_shell(versions: list) -> list:
    return [
        {
            "guid": GUID,
            "name": "WatchSignal",
            "overview": OVERVIEW,
            "description": DESCRIPTION,
            "owner": "Myxelium",
            "category": "General",
            "versions": versions,
        }
    ]


def read_manifest() -> list:
    if not MANIFEST.exists():
        return package_shell([])
    data = json.loads(MANIFEST.read_text())
    if not isinstance(data, list) or not data:
        return package_shell([])
    data[0].setdefault("versions", [])
    return data


def merge_versions(existing: list, fresh: list) -> list:
    fresh_keys = {(item["version"], item["targetAbi"]) for item in fresh}
    kept = [item for item in existing if (item.get("version"), item.get("targetAbi")) not in fresh_keys]
    return fresh + kept


def write_manifest(versions: list) -> None:
    current = read_manifest()
    current[0]["versions"] = merge_versions(current[0].get("versions", []), versions)
    MANIFEST.write_text(json.dumps(current, indent=2) + "\n")


def builds(release_version: str | None) -> list[tuple[str, str, str]]:
    if release_version:
        parts = release_version.split(".")
        if len(parts) != 3 or not all(part.isdigit() for part in parts):
            raise SystemExit("--release-version must look like 1.2.3")
        return [
            ("net10.0", f"{release_version}.1", "12.0.0.0"),
            ("net9.0", f"{release_version}.0", "10.11.0.0"),
        ]
    return [
        ("net10.0", "1.0.1.0", "12.0.0.0"),
        ("net9.0", "1.0.0.0", "10.11.0.0"),
    ]


def source_url(filename: str, tag: str | None) -> str:
    if tag:
        return f"https://github.com/{REPO}/releases/download/{tag}/{filename}"
    return f"https://github.com/{REPO}/raw/main/dist/{filename}"


def pack_zip(dll: Path, version: str, abi: str) -> Path:
    DIST.mkdir(exist_ok=True)
    archive = DIST / f"WatchSignal_{version}_{abi}.zip"
    meta = {
        "guid": GUID,
        "name": "WatchSignal",
        "overview": OVERVIEW,
        "description": DESCRIPTION,
        "owner": "Myxelium",
        "category": "General",
        "targetAbi": abi,
        "version": version,
        "changelog": CHANGELOG,
        "timestamp": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    }
    if archive.exists():
        archive.unlink()
    with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as bundle:
        bundle.write(dll, "Jellyfin.Plugin.WatchSignal.dll")
        bundle.writestr("meta.json", json.dumps(meta, indent=2) + "\n")
    return archive


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--release-version", help="Three-part version, such as 1.0.4")
    parser.add_argument("--tag", help="GitHub release tag, such as v1.0.4")
    parser.add_argument("--skip-test", action="store_true")
    parser.add_argument("--merge-catalog", type=Path, help="Merge a versions JSON file into manifest.json and exit")
    args = parser.parse_args()

    if args.merge_catalog:
        fresh = json.loads(args.merge_catalog.read_text())
        write_manifest(fresh)
        print(f"Merged {len(fresh)} versions into manifest.json")
        return

    selected = builds(args.release_version)
    dotnet = ROOT / ".dotnet" / "dotnet"
    command = [str(dotnet) if dotnet.exists() else "dotnet", "build" if args.skip_test else "test"]
    command += [str(ROOT / "Jellyfin.Plugin.WatchSignal.sln"), "-c", "Release", "--nologo"]
    if args.release_version:
        command.append(f"-p:ReleaseVersion={args.release_version}")
    subprocess.run(command, cwd=ROOT, check=True)

    timestamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    versions_json = []
    for framework, version, abi in selected:
        dll = PROJECT / "bin" / "Release" / framework / "Jellyfin.Plugin.WatchSignal.dll"
        if not dll.exists():
            raise SystemExit(f"Missing build output {dll}")
        archive = pack_zip(dll, version, abi)
        versions_json.append(
            {
                "version": version,
                "changelog": CHANGELOG,
                "targetAbi": abi,
                "sourceUrl": source_url(archive.name, args.tag),
                "checksum": md5(archive),
                "timestamp": timestamp,
            }
        )

    DIST.mkdir(exist_ok=True)
    (DIST / "catalog-versions.json").write_text(json.dumps(versions_json, indent=2) + "\n")
    write_manifest(versions_json)
    print(f"Wrote manifest.json and {len(versions_json)} zip files in dist/")


if __name__ == "__main__":
    main()
