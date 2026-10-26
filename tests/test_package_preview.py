"""Offline checks for the preview packager's path and NSIS escaping boundaries."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("package_preview", ROOT / "desktop/scripts/Package-Preview.py")
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


class PackagePreviewTests(unittest.TestCase):
    def test_root_and_outside_paths_are_rejected(self):
        for path in (ROOT, ROOT.parent, ROOT / ".." / "elsewhere"):
            with self.assertRaises(ValueError):
                package.inside(path)

    def test_descendant_path_is_allowed(self):
        self.assertEqual(package.inside(ROOT / "artifacts" / "fixture"), ROOT / "artifacts" / "fixture")

    def test_symlink_is_rejected_even_inside_repository(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "artifacts") as temporary:
            folder = Path(temporary)
            target = folder / "payload"
            target.mkdir()
            link = folder / "linked"
            link.symlink_to(target, target_is_directory=True)
            with self.assertRaises(ValueError):
                package.inside(link)

    def test_nsis_variables_are_escaped(self):
        self.assertEqual(package.nsis_string("$INSTDIR"), "$$INSTDIR")

    def test_newlines_are_never_injected_into_uninstall_script(self):
        for name in ("bad\nDelete", "bad\rDelete"):
            with self.assertRaises(ValueError):
                package.nsis_string(name)


if __name__ == "__main__":
    unittest.main()
