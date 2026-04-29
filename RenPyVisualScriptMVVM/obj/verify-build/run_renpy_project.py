#!/usr/bin/env python3
import argparse
import os
import platform
import subprocess
import sys
from pathlib import Path


def resolve_runner(sdk: Path):
    system = platform.system()
    renpy_py = sdk / "renpy.py"

    if system == "Windows":
        renpy_exe = sdk / "renpy.exe"
        if renpy_exe.exists():
            return [str(renpy_exe)]

        return [sys.executable, str(renpy_py)]

    if system == "Linux":
        renpy_sh = sdk / "renpy.sh"
        if renpy_sh.exists():
            return [str(renpy_sh)]

        return [sys.executable, str(renpy_py)]

    if system == "Darwin":
        renpy_app = sdk / "renpy.app" / "Contents" / "MacOS" / "renpy"
        if renpy_app.exists():
            return [str(renpy_app)]

        return [sys.executable, str(renpy_py)]

    return [sys.executable, str(renpy_py)]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sdk", required=True, help="Path to Ren'Py SDK directory")
    ap.add_argument("--project", required=True, help="Path to project base directory")
    args = ap.parse_args()

    sdk = Path(os.path.abspath(args.sdk))
    project = Path(os.path.abspath(args.project))

    if not project.is_dir():
        print(f"ERROR: project dir not found: {project}", file=sys.stderr)
        return 3

    runner = resolve_runner(sdk)
    cmd = runner + [str(project)]

    try:
        subprocess.Popen(cmd, cwd=str(project))
    except Exception as e:
        print(f"ERROR: failed to start Ren'Py: {e}", file=sys.stderr)
        return 4

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
