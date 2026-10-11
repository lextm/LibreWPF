#!/usr/bin/env python3
"""Package extraction/provenance controls; fixtures are not native qualification."""

import importlib.util
from pathlib import Path
import tempfile
import unittest
import zipfile


ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("release_native", ROOT / "eng/progpu-stage-release-native-packages.py")
STAGING = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(STAGING)
VERSION = "0.1.0-preview.67"
COMMIT = "a" * 40
NOTICE = "runtimes/osx-arm64/native/licenses/edit-word-icu/LICENSE"


class ReleaseNativePackagesTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="release-native-packages-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.destination = self.root / "staged"

    def package(self, sdk=False, commit=COMMIT, version=VERSION, entries=None):
        package_id = "ProGPU.Native.Sdk" if sdk else "ProGPU.Backend.Native"
        path = self.root / (package_id + ".nupkg")
        if entries is None:
            entry = "sdk/libprogpu_native_text.a" if sdk else "libprogpu_native.dylib"
            entries = [("runtimes/osx-arm64/native/" + entry, b"layout fixture"), (NOTICE, b"original notice")]
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr(package_id + ".nuspec", f'''<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata><id>{package_id}</id><version>{version}</version>
              <repository url="https://github.com/wieslawsoltes/ProGPU" commit="{commit}"/></metadata></package>''')
            for name, data in entries:
                archive.writestr(name, data)
        return path

    def stage(self, native, sdk):
        return STAGING.stage(native, sdk, self.destination, VERSION, COMMIT)

    def test_complete_pair_preserves_payload_and_shared_extensionless_notice(self):
        self.assertEqual(self.stage(self.package(), self.package(sdk=True)), 3)
        self.assertEqual((self.destination / NOTICE).read_bytes(), b"original notice")
        self.assertEqual((self.destination / "runtimes/osx-arm64/native/sdk/libprogpu_native_text.a").read_bytes(), b"layout fixture")

    def test_missing_sdk_never_publishes_partial_runtime(self):
        with self.assertRaises(FileNotFoundError):
            self.stage(self.package(), self.root / "missing.nupkg")
        self.assertFalse(self.destination.exists())

    def test_mixed_commit_or_version_never_publishes(self):
        for changed in ({"commit": "b" * 40}, {"version": "0.1.0-preview.68"}):
            with self.subTest(changed=changed), self.assertRaisesRegex(ValueError, "provenance"):
                self.stage(self.package(), self.package(sdk=True, **changed))
            self.assertFalse(self.destination.exists())

    def test_different_shared_notice_is_not_overwritten(self):
        with self.assertRaisesRegex(ValueError, "payload collision"):
            self.stage(self.package(), self.package(sdk=True, entries=[(NOTICE, b"changed notice")]))
        self.assertFalse(self.destination.exists())

    def test_unsafe_path_never_escapes_staging(self):
        for name in ("runtimes/../../escaped", "runtimes/../escaped", "runtimes/x\\escaped"):
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, "unsafe"):
                self.stage(self.package(), self.package(sdk=True, entries=[(name, b"bad")]))
            self.assertFalse(self.destination.exists())
            self.assertFalse((self.root / "escaped").exists())

    def test_existing_destination_remains_untouched(self):
        self.destination.mkdir()
        original = self.destination / "original"
        original.write_bytes(b"existing")
        with self.assertRaisesRegex(ValueError, "already exist"):
            self.stage(self.package(), self.package(sdk=True))
        self.assertEqual(original.read_bytes(), b"existing")


if __name__ == "__main__":
    unittest.main()
