#!/usr/bin/env python3
import argparse
import os
import subprocess
import sys


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sdk", required=True, help="Path to Ren'Py SDK directory (contains renpy.py)")
    ap.add_argument("--project", required=True, help="Path to project base directory")
    args = ap.parse_args()

    sdk = os.path.abspath(args.sdk)
    project = os.path.abspath(args.project)

    renpy_py = os.path.join(sdk, "renpy.py")
    if not os.path.isfile(renpy_py):
        print(f"ERROR: renpy.py not found: {renpy_py}", file=sys.stderr)
        return 2

    if not os.path.isdir(project):
        print(f"ERROR: project dir not found: {project}", file=sys.stderr)
        return 3

    # Use the same Python that runs this script (in your app it's SDK python on Windows).
    python = sys.executable

    # According to Ren'Py CLI docs, calling renpy.py with <base> runs commands like lint/compile.
    # When no command is given, Ren'Py starts the project.
    cmd = [python, renpy_py, project]

    try:
        subprocess.Popen(cmd, cwd=sdk)
    except Exception as e:
        print(f"ERROR: failed to start Ren'Py: {e}", file=sys.stderr)
        return 4

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
