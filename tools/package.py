"""Makes the downloads a player gets, in dist/package/:

    Zerg-<version>-win-x64.zip   one self-contained, single-file Zerg.exe (no
                                 .NET to install, the native libraries packed
                                 inside it and unpacked on first run), and
                                 README.txt from packaging/README.txt with the
                                 version filled in
    Zerg-<version>-setup.exe     the same two files in an installer
                                 (packaging/Zerg.iss), made with Inno Setup 6:
                                 winget install JRSoftware.InnoSetup

The version is the one in Directory.Build.props; cut it first (CLAUDE.md,
"Versions").

    python tools/package.py

Prints what it made, with SHA-256s. Close any Zerg that was built here
first: a running one locks its build output.
"""

import hashlib
import os
import re
import shutil
import subprocess
import sys
import time
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def version():
    text = (ROOT / 'Directory.Build.props').read_text(encoding='utf-8')
    m = re.search(r'<Version>([^<]+)</Version>', text)
    if not m:
        sys.exit('no <Version> in Directory.Build.props')
    return m.group(1).strip()


def remove(path):
    """OneDrive holds this repository, and a delete can fail on a file it is
    syncing: tried again rather than forced."""
    for attempt in range(5):
        if not path.exists():
            return
        try:
            shutil.rmtree(path) if path.is_dir() else path.unlink()
            return
        except OSError:
            time.sleep(1 + attempt)
    sys.exit(f'could not remove {path}')


def iscc():
    """Inno Setup's compiler: on PATH, or where its installer puts it (for
    the user alone, or for everyone)."""
    found = shutil.which('iscc')
    if found:
        return Path(found)
    for base in ('LOCALAPPDATA', 'ProgramFiles(x86)', 'ProgramFiles'):
        if os.environ.get(base):
            sub = 'Programs/Inno Setup 6' if base == 'LOCALAPPDATA' else 'Inno Setup 6'
            exe = Path(os.environ[base]) / sub / 'ISCC.exe'
            if exe.exists():
                return exe
    sys.exit('no Inno Setup 6 (ISCC.exe) found for the installer: winget install JRSoftware.InnoSetup')


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    v = version()
    name = f'Zerg-{v}'
    out = ROOT / 'dist' / 'package'
    folder = out / name
    archive = out / f'{name}-win-x64.zip'
    setup = out / f'{name}-setup.exe'
    compiler = iscc()
    remove(folder)
    remove(archive)
    remove(setup)
    folder.mkdir(parents=True)

    env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1')
    publish = [
        'dotnet', 'publish', str(ROOT / 'src' / 'Zerg' / 'Zerg.csproj'),
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        # A player has no use for the symbols; a bug report has the log.
        '-p:DebugType=none', '-p:DebugSymbols=false',
        '-o', str(folder),
    ]
    try:
        subprocess.run(publish, check=True, env=env)
    finally:
        # A lingering build node has held directory locks here.
        subprocess.run(['dotnet', 'build-server', 'shutdown'], env=env, stdout=subprocess.DEVNULL)

    files = sorted(p.name for p in folder.iterdir())
    if files != ['Zerg.exe']:
        sys.exit(f'expected Zerg.exe alone in {folder}, found {files}')

    readme = (ROOT / 'packaging' / 'README.txt').read_text(encoding='utf-8')
    readme = readme.replace('{version}', v)
    # Notepad and every other Windows reader take CRLF without a second look.
    (folder / 'README.txt').write_bytes(readme.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8'))

    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for f in sorted(folder.iterdir()):
            z.write(f, f'{name}/{f.name}')

    subprocess.run([str(compiler), '/Q', f'/DAppVersion={v}', f'/DSourceDir={folder}',
                    f'/O{out}', str(ROOT / 'packaging' / 'Zerg.iss')], check=True)
    if not setup.exists():
        sys.exit(f'Inno Setup made no {setup}')

    exe = folder / 'Zerg.exe'
    print(f'{exe}  {exe.stat().st_size / 1e6:.1f} MB')
    for made in (archive, setup):
        print(f'{made}  {made.stat().st_size / 1e6:.1f} MB')
        print(f'  SHA-256  {sha256(made)}')


if __name__ == '__main__':
    main()
