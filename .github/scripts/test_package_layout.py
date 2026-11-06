from pathlib import Path
import tempfile
import unittest
from package_layout import PACKAGE_FILES
from stage_package import stage


class PackageLayoutTests(unittest.TestCase):
    def test_runtime_backups_and_logs_do_not_enter_distribution(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source, output = root / 'Publish', root / 'Package'
            for name in PACKAGE_FILES | {'Backups/auto-fixture.zip', 'Data/history.json', 'Logs/output.txt'}:
                path = source / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(name.encode())
            stage(source, output)
            self.assertEqual({str(p.relative_to(output)).replace('\\', '/') for p in output.rglob('*') if p.is_file()}, PACKAGE_FILES)
            for name in PACKAGE_FILES:
                self.assertEqual((source / name).read_bytes(), (output / name).read_bytes())

    def test_incomplete_build_is_rejected_before_copying(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'Publish').mkdir()
            with self.assertRaises(ValueError):
                stage(root / 'Publish', root / 'Package')
            self.assertFalse((root / 'Package').exists())
