#!/usr/bin/env python3
"""Resolve exact framework package provenance from its selected source graph."""

import argparse
import json
from pathlib import Path
import re
import subprocess


OWNERS = {
    "LibreWPF.Interop": ("LibreWPF", Path("."), "src/ProGPU.Wpf.Interop/ProGPU.Wpf.Interop.csproj"),
    "ProGPU.System.Drawing.Common": ("LibreWinForms", Path("external/LibreWinForms"), "src/System.Drawing.Common/System.Drawing.Common.csproj"),
}


def git(root, *arguments):
    return subprocess.run(["git", "-C", str(root), *arguments],
                          capture_output=True, text=True, check=True).stdout.strip()


def require_commit(value):
    if not isinstance(value, str) or re.fullmatch(r"[0-9a-f]{40}", value) is None:
        raise ValueError("Package provenance requires a full exact source commit")
    return value


def identity(package, wpf_root, producer_commit, producer_packages=False):
    owner, relative, legacy_project = OWNERS[package]
    if not producer_packages:
        return f"https://github.com/wieslawsoltes/{owner}", require_commit(git(wpf_root / relative, "rev-parse", "HEAD"))

    producer_commit = require_commit(producer_commit)
    producer = wpf_root / "external/ProGPU"
    try:
        source = git(producer, "show", f"{producer_commit}:eng/framework-sources.json")
    except subprocess.CalledProcessError:
        # Previously published packages predate the move. Admit that layout only
        # when the exact selected producer really owns the original project.
        git(producer, "cat-file", "-e", f"{producer_commit}:{legacy_project}")
        return "https://github.com/wieslawsoltes/ProGPU", producer_commit

    manifest = json.loads(source)
    revisions = manifest.get("repositories", {})
    if manifest.get("schemaVersion") != 1 or set(revisions) != {"LibreWPF", "LibreWinForms"}:
        raise ValueError("Unsupported producer framework source manifest")
    for revision in revisions.values():
        require_commit(revision)
    return f"https://github.com/wieslawsoltes/{owner}", revisions[owner]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("package", choices=OWNERS)
    parser.add_argument("wpf_root", type=Path)
    parser.add_argument("producer_commit")
    parser.add_argument("--producer-packages", action="store_true")
    args = parser.parse_args()
    print("|".join(identity(args.package, args.wpf_root, args.producer_commit, args.producer_packages)))


if __name__ == "__main__":
    main()
