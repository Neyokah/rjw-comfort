"""Run integration fixtures under this Mac's installed RimWorld compatibility runtime.

The test harness controls map/health notifications, baseline eligibility and the
completion body. Production patches, native Submit actions, timer components and
Scribe run as real methods.
No game configuration or saves are opened or modified.
"""
from pathlib import Path
import os
import json
import hashlib
import shutil
import subprocess
import tempfile

mod = Path(__file__).resolve().parents[1]
game = Path(os.environ['RIMWORLD_MANAGED_DIR'])
with tempfile.TemporaryDirectory(prefix='ComfortTests-') as tmp:
    tmp = Path(tmp)
    build = tmp / 'build'
    runtime = tmp / 'runtime'
    runtime.mkdir()
    subprocess.run(['dotnet', 'build', str(mod / 'Tests/Comfort.Tests.csproj'), '-c', 'Release', '-o', str(build), '--nologo', '--ignore-failed-sources', '-p:UseSharedCompilation=false', '-warnaserror'], check=True)
    packaged = mod / '1.7/Assemblies/Comfort.dll'
    if hashlib.sha256(packaged.read_bytes()).digest() != hashlib.sha256((build / packaged.name).read_bytes()).digest():
        raise RuntimeError('Packaged DLL differs from the tested source build. Rebuild Source/Comfort.csproj first.')
    print('Packaged DLL matches the tested source build.', flush=True)
    for p in game.iterdir():
        if p.suffix in ('.dll', '.dylib'):
            (runtime / p.name).symlink_to(p)
    for p in build.iterdir():
        if p.is_file() and not p.name.startswith(('Assembly-CSharp', 'UnityStubs')):
            dest = runtime / p.name
            if dest.is_symlink():
                dest.unlink()
            shutil.copy2(p, dest)
    shutil.copy2(game / 'RimWorld.runtimeconfig.json', runtime / 'Comfort.Tests.runtimeconfig.json')
    deps = json.loads((game / 'RimWorld.deps.json').read_text())
    extra = json.loads((build / 'Comfort.Tests.deps.json').read_text())
    target = deps['targets'][deps['runtimeTarget']['name']]
    for name, value in next(iter(extra['targets'].values())).items():
        if name.split('/')[0] not in {n.split('/')[0] for n in target}:
            target[name] = value
            deps['libraries'][name] = extra['libraries'][name]
    (runtime / 'Comfort.Tests.deps.json').write_text(json.dumps(deps))
    subprocess.run([str(runtime / 'Comfort.Tests')], cwd=mod, check=True)
