"""Exact source ownership controls; these create no managed or native payloads."""

import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


SPEC = importlib.util.spec_from_file_location(
    "framework_package_source", Path(__file__).resolve().parents[1] / "progpu-framework-package-source.py")
SOURCE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SOURCE)


class FrameworkPackageSourceTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory(prefix="framework-package-source-")
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.producer = self.root / "external/ProGPU"
        self.forms = self.root / "external/LibreWinForms"
        for path in (self.root, self.producer, self.forms):
            path.mkdir(parents=True, exist_ok=True)
            SOURCE.git(path, "init", "--quiet")
            SOURCE.git(path, "config", "user.name", "Package provenance controls")
            SOURCE.git(path, "config", "user.email", "package-provenance@example.invalid")
            SOURCE.git(path, "commit", "--quiet", "--allow-empty", "-m", f"Source checkpoint {path.name}")
        self.wpf_commit = SOURCE.git(self.root, "rev-parse", "HEAD")
        self.forms_commit = SOURCE.git(self.forms, "rev-parse", "HEAD")

    def manifest(self, value):
        (self.producer / "eng").mkdir(exist_ok=True)
        (self.producer / "eng/framework-sources.json").write_text(json.dumps(value))
        SOURCE.git(self.producer, "add", "eng/framework-sources.json")
        SOURCE.git(self.producer, "commit", "--quiet", "-m", "Pin source graph")
        return SOURCE.git(self.producer, "rev-parse", "HEAD")

    def test_rebuilt_frameworks_identify_actual_owners(self):
        for package, owner, revision in (
            ("LibreWPF.Interop", "LibreWPF", self.wpf_commit),
            ("ProGPU.System.Drawing.Common", "LibreWinForms", self.forms_commit),
        ):
            self.assertEqual(SOURCE.identity(package, self.root, "unused"),
                             (f"https://github.com/wieslawsoltes/{owner}", revision))

    def test_producer_packages_use_exact_historical_manifest_not_framework_heads(self):
        revisions = {"LibreWPF": "1" * 40, "LibreWinForms": "2" * 40}
        pinned = self.manifest({"schemaVersion": 1, "repositories": revisions})
        self.manifest({"schemaVersion": 1, "repositories": {"LibreWPF": "3" * 40, "LibreWinForms": "4" * 40}})
        for package, owner in (("LibreWPF.Interop", "LibreWPF"), ("ProGPU.System.Drawing.Common", "LibreWinForms")):
            self.assertEqual(SOURCE.identity(package, self.root, pinned, True),
                             (f"https://github.com/wieslawsoltes/{owner}", revisions[owner]))

    def test_old_package_provenance_requires_actual_legacy_source(self):
        for _, _, project in SOURCE.OWNERS.values():
            path = self.producer / project
            path.parent.mkdir(parents=True)
            path.write_text("<Project />")
        SOURCE.git(self.producer, "add", "src")
        SOURCE.git(self.producer, "commit", "--quiet", "-m", "Original source layout")
        pinned = SOURCE.git(self.producer, "rev-parse", "HEAD")
        for package in SOURCE.OWNERS:
            self.assertEqual(SOURCE.identity(package, self.root, pinned, True),
                             ("https://github.com/wieslawsoltes/ProGPU", pinned))

    def test_missing_manifest_and_source_fail_instead_of_inventing_old_owner(self):
        pinned = SOURCE.git(self.producer, "rev-parse", "HEAD")
        with self.assertRaises(subprocess.CalledProcessError):
            SOURCE.identity("LibreWPF.Interop", self.root, pinned, True)

    def test_branch_names_and_incomplete_manifest_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "exact source commit"):
            SOURCE.identity("LibreWPF.Interop", self.root, "main", True)
        pinned = self.manifest({"schemaVersion": 1, "repositories": {"LibreWPF": "1" * 40}})
        with self.assertRaisesRegex(ValueError, "Unsupported"):
            SOURCE.identity("LibreWPF.Interop", self.root, pinned, True)

    def test_invalid_other_owner_cannot_partially_admit_manifest(self):
        pinned = self.manifest({"schemaVersion": 1, "repositories": {"LibreWPF": "1" * 40, "LibreWinForms": "main"}})
        with self.assertRaisesRegex(ValueError, "exact source commit"):
            SOURCE.identity("LibreWPF.Interop", self.root, pinned, True)

    def test_actual_shell_audit_preserves_source_mode_after_snapshot(self):
        eng = Path(__file__).resolve().parents[1]
        script = (eng / "progpu-preview-package-audit.sh").read_text()
        loop = script.split('for package_id in "${runtime_packages[@]}"; do', 1)[1]
        selection = '  case "${package_id}" in' + loop.split('  case "${package_id}" in', 1)[1].split('\n  esac', 1)[0] + '\n  esac\n'
        (self.root / "eng").mkdir()
        (self.root / "eng/progpu-framework-package-source.py").write_bytes((eng / "progpu-framework-package-source.py").read_bytes())
        revisions = {"LibreWPF": "1" * 40, "LibreWinForms": "2" * 40}
        pinned = self.manifest({"schemaVersion": 1, "repositories": revisions})
        for package in SOURCE.OWNERS:
            for staged, rebuilt in (("", "1"), ("snapshot", "1"), ("producer", "0")):
                expected = SOURCE.identity(package, self.root, pinned, bool(staged) and rebuilt == "0")
                environment = dict(os.environ, repo_root=str(self.root), package_id=package,
                                   progpu_package_commit=pinned, PROGPU_WPF_PREPACKAGED_PROGPU_DIR=staged,
                                   PROGPU_WPF_REPACKED_FRAMEWORK_PACKAGES=rebuilt)
                result = subprocess.run(["bash", "-eu", "-c",
                    'require_nuspec_repository() { printf "%s|%s|%s\\n" "$1" "$2" "$3"; }\n' + selection],
                    env=environment, check=True, text=True, capture_output=True)
                self.assertEqual(result.stdout.strip(), "|".join((package, *expected)))


if __name__ == "__main__":
    unittest.main()
