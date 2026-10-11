#!/usr/bin/env python3
"""Execute the canonical workflow staging step with a recording helper, offline."""

import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import zipfile


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github/workflows/progpu-wpf-sdk.yml"


class CanonicalNativeStagingTests(unittest.TestCase):
    def staging_step(self):
        workflow = WORKFLOW.read_text()
        canonical = workflow.split("\n  canonical-winforms-integration:\n", 1)[1].split("\n  portable-source-contracts:\n", 1)[0]
        name = "      - name: Stage exact native drawing package dependency\n"
        self.assertLess(canonical.index(name), canonical.index("      - name: Build canonical WinForms and WindowsFormsIntegration\n"))
        step = canonical.split(name, 1)[1].split("\n      - name:", 1)[0]
        self.assertIn("GH_TOKEN: ${{ github.token }}", step)
        return "\n".join(line[10:] for line in step.split("        run: |\n", 1)[1].splitlines())

    def run_step(self, helper_status=0, git_status=0):
        with tempfile.TemporaryDirectory(prefix="wpf-canonical-staging-") as temporary:
            root = Path(temporary)
            (root / "eng").mkdir()
            helper = root / "eng/progpu-stage-ci-native-runtimes.sh"
            # This stub records arguments only; it cannot create native payloads
            # and is never used by package production or the real staging helper.
            helper.write_text('#!/bin/bash\nprintf "%s\\n" "$@" > "$STAGING_RECEIPT"\nexit "$STAGING_STATUS"\n')
            helper.chmod(0o755)
            receipt = root / "arguments"
            environment = dict(os.environ, GITHUB_WORKSPACE=str(root), STAGING_RECEIPT=str(receipt),
                               STAGING_STATUS=str(helper_status), GIT_STATUS=str(git_status))
            script = '''git() {
  test "$*" = "-C external/ProGPU rev-parse HEAD" || return 91
  test "$GIT_STATUS" = 0 || return "$GIT_STATUS"
  printf '%s\\n' aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
}
'''+ self.staging_step()
            result = subprocess.run(["bash", "-c", script], cwd=root, env=environment, capture_output=True, text=True)
            return result.returncode, receipt.read_text().splitlines() if receipt.exists() else [], str(root)

    def test_stages_exact_checkout_into_its_native_package_root(self):
        status, arguments, root = self.run_step()
        self.assertEqual(status, 0)
        self.assertEqual(arguments, ["a" * 40, root + "/external/ProGPU/artifacts/progpu-native/package"])

    def test_failed_producer_staging_fails_the_step(self):
        status, arguments, _ = self.run_step(helper_status=23)
        self.assertEqual(status, 23)
        self.assertEqual(arguments[0], "a" * 40)

    def test_unresolved_source_identity_never_invokes_staging(self):
        status, arguments, _ = self.run_step(git_status=27)
        self.assertEqual(status, 27)
        self.assertEqual(arguments, [])


class ReleaseNativeStagingTests(unittest.TestCase):
    workflow = ROOT / ".github/workflows/progpu-wpf-release.yml"

    def native_step(self):
        workflow = self.workflow.read_text()
        names = ["Stage exact ProGPU release packages",
                 "Stage native runtimes from exact ProGPU release package",
                 "Build canonical LibreWinForms package closure",
                 "Align canonical package feed with exact ProGPU release",
                 "Build, pack, and smoke SDK"]
        positions = [workflow.index("      - name: " + name + "\n") for name in names]
        self.assertEqual(positions, sorted(positions),
                         "canonical drawing packages require native payloads before they can be produced")
        step = workflow.split("      - name: " + names[1] + "\n", 1)[1].split("\n      - name:", 1)[0]
        self.assertNotIn("continue-on-error", step)
        return "\n".join(line[10:] for line in step.split("        run: |\n", 1)[1].splitlines())

    def test_release_production_order(self):
        self.native_step()

    def test_absent_native_payload_cannot_reach_canonical_packaging(self):
        for metadata_only_archive in (False, True):
            with self.subTest(metadata_only_archive=metadata_only_archive):
                with tempfile.TemporaryDirectory(prefix="wpf-release-native-") as temporary:
                    root = Path(temporary)
                    packages = root / "packages"
                    packages.mkdir()
                    if metadata_only_archive:
                        # Deliberately contains no runtime bytes: this is a
                        # rejection control, never a staged native substitute.
                        with zipfile.ZipFile(packages / "ProGPU.Backend.Native.0.1.0-test.nupkg", "w") as archive:
                            archive.writestr("ProGPU.Backend.Native.nuspec", "<package/>")
                    environment = dict(os.environ, GITHUB_WORKSPACE=str(root),
                                       PROGPU_WPF_PREPACKAGED_PROGPU_DIR=str(packages),
                                       PROGPU_WPF_PROGPU_PACKAGE_VERSION="0.1.0-test")
                    result = subprocess.run(["bash", "-c", self.native_step()], cwd=root,
                                            env=environment, capture_output=True, text=True)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertFalse((root / "external/ProGPU/artifacts/progpu-native/runtime").exists())


if __name__ == "__main__":
    unittest.main()
