import os
from pathlib import Path

PROJECT_DIR = Path(__file__).parent.resolve()

def os_command(command: str) -> None:
    os.system(command)

def update_repo(url: str) -> Path:
    repo_name = url.split('/')[-1].split('.')[0]
    repo_dir = PROJECT_DIR / "SDL3" / f".{repo_name}"
    
    if repo_dir.is_dir():
        os_command(f"git -C {repo_dir} pull")
    else:
        os_command(f"git clone {url} {repo_dir}")
        
    return repo_dir

def build_repo(repo_path: Path, extra_flags: str = "") -> None:
    build_dir = repo_path / "build"
    
    os_command(f"cmake -S {repo_path} -B {build_dir} -G Ninja -DCMAKE_BUILD_TYPE=Release {extra_flags}")
    os_command(f"cmake --build {build_dir}")

def main() -> None:
    print("PULLING GITHUB UPDATES...")
    sdl_path         = update_repo("https://github.com/libsdl-org/SDL.git")
    shadercross_path = update_repo("https://github.com/libsdl-org/SDL_shadercross.git")

    print("UPDATING SHADERCROSS SUBMODULES...")
    os_command(f"git -C {shadercross_path} submodule update --init --recursive")

    print("BUILDING NATIVE BINARIES...")
    build_repo(sdl_path)
    
    # Configure and build SDL_shadercross using paths derived directly from Path objects
    sdl3_build_dir = sdl_path / "build"
    build_repo(shadercross_path, extra_flags=f"-DSDLSHADERCROSS_VENDORED=ON -DSDL3_DIR={sdl3_build_dir}")

if __name__ == "__main__":
    main()
