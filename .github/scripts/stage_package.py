"""Stage only build products, excluding test-created backups and runtime data."""
from pathlib import Path
import shutil
import sys
from package_layout import PACKAGE_FILES


def stage(source, destination):
    source, destination = Path(source), Path(destination)
    for name in PACKAGE_FILES:
        path = source / name
        if not path.is_file() or path.is_symlink():
            raise ValueError(f'Missing or unsafe build product: {name}')
    if destination.exists() and any(destination.iterdir()):
        raise ValueError('Package destination must be empty')
    for name in sorted(PACKAGE_FILES):
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / name, target)


if __name__ == '__main__':
    stage(sys.argv[1], sys.argv[2])
