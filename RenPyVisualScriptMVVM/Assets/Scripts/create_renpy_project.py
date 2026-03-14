import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

def detect_renpy_runner(sdk: Path):
    # Windows: prefer renpy.exe if present
    renpy_exe = sdk / "renpy.exe"
    if renpy_exe.exists():
        return [str(renpy_exe)]

    # Windows bundled python
    renpy_py = sdk / "renpy.py"
    win_py = sdk / "lib" / "py3-windows-x86_64" / "python.exe"
    if win_py.exists() and renpy_py.exists():
        return [str(win_py), str(renpy_py)]

    # Linux/macOS
    renpy_sh = sdk / "renpy.sh"
    if renpy_sh.exists():
        return [str(renpy_sh)]

    return ["renpy"]

def copy_template_project(sdk: Path, project_dir: Path):
    template_dir = sdk / "gui"
    if not template_dir.exists():
        raise FileNotFoundError(f"Не найден шаблон {template_dir}")

    if project_dir.exists():
        if any(project_dir.iterdir()):
            raise FileExistsError(f"Папка проекта уже существует и не пуста: {project_dir}")
    else:
        project_dir.mkdir(parents=True, exist_ok=True)

    for item in template_dir.iterdir():
        dst = project_dir / item.name
        if item.is_dir():
            shutil.copytree(item, dst)
        else:
            shutil.copy2(item, dst)

def run_generate_gui(sdk: Path, project_dir: Path, width: int, height: int,
                     accent: str, boring: str, light: bool, language: str | None):
    runner = detect_renpy_runner(sdk)

    cmd = runner + [
        "launcher",
        "generate_gui",
        str(project_dir),
        "--start",
        "--width", str(width),
        "--height", str(height),
        "--accent", accent,
        "--boring", boring,
        "--template", str(sdk / "gui"),
    ]

    if light:
        cmd.append("--light")
    if language:
        cmd += ["--language", language]

    print("RUN:", " ".join(cmd))
    result = subprocess.run(cmd, cwd=str(sdk), capture_output=True, text=True)
    if result.returncode != 0:
        print(result.stdout)
        print(result.stderr, file=sys.stderr)
        raise RuntimeError(f"Ren'Py generate_gui завершился с кодом {result.returncode}")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sdk", required=True, help="Путь к папке Ren'Py SDK (там где renpy.sh/renpy.py)")
    ap.add_argument("--name", required=True, help="Имя проекта (папка проекта)")
    ap.add_argument("--out", required=True, help="Папка, где создать проект")
    ap.add_argument("--width", type=int, default=1280)
    ap.add_argument("--height", type=int, default=720)
    ap.add_argument("--accent", default="#00b8c3")
    ap.add_argument("--boring", default="#000000")
    ap.add_argument("--light", action="store_true")
    ap.add_argument("--language", default=None, help="Например: russian, german, french; либо не задавать")

    args = ap.parse_args()

    sdk = Path(args.sdk).resolve()
    out_dir = Path(args.out).resolve()
    # Если out уже указывает на папку проекта (например .../Project/MyGame),
    # не создаём вложенную папку MyGame/MyGame.
    if out_dir.name == args.name:
        project_dir = out_dir
    else:
        project_dir = out_dir / args.name

    copy_template_project(sdk, project_dir)
    run_generate_gui(
        sdk=sdk,
        project_dir=project_dir,
        width=args.width,
        height=args.height,
        accent=args.accent,
        boring=args.boring,
        light=args.light,
        language=args.language,
    )

    print(f"OK: {project_dir}")

if __name__ == "__main__":
    main()