#!/usr/bin/env python3
"""Offline package metadata/selection controls; no restore or renderer execution."""

import importlib.util
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET
import zipfile


ROOT = Path(__file__).resolve().parents[2]
CHECKER = ROOT / "eng/progpu-preview-package-closure.py"
SPEC = importlib.util.spec_from_file_location("preview_package_closure", CHECKER)
CLOSURE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CLOSURE)
SOURCE_VERSION = "0.1.0-source.48a49afe"
WPF_VERSION = "0.1.0-preview.65"


class PreviewPackageClosureTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="wpf-preview-closure-")
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)

    def package(self, name, version=SOURCE_VERSION, dependencies=(), grouped=True,
                namespace="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"):
        root = ET.Element("package", {"xmlns": namespace} if namespace else {})
        metadata = ET.SubElement(root, "metadata")
        ET.SubElement(metadata, "id").text = name
        ET.SubElement(metadata, "version").text = version
        container = ET.SubElement(metadata, "dependencies")
        if grouped:
            container = ET.SubElement(container, "group", {"targetFramework": "net10.0"})
        for dependency, constraint in dependencies:
            ET.SubElement(container, "dependency", {"id": dependency, "version": constraint})
        path = self.directory / f"{name}.{version}.nupkg"
        with zipfile.ZipFile(path, "w") as package:
            package.writestr(name + ".nuspec", ET.tostring(root))
        return path

    def bridge(self, grouped=True):
        return self.package("LibreWPF.ProGPU", WPF_VERSION,
                            [("ProGPU.Scene.Native", SOURCE_VERSION)], grouped)

    def test_exact_scene_native_source_closure(self):
        paths = [self.bridge(), self.package("ProGPU.Scene.Native", dependencies=[
            ("ProGPU.Scene", SOURCE_VERSION), ("ProGPU.Backend.Native", SOURCE_VERSION)]),
            self.package("ProGPU.Scene"), self.package("ProGPU.Backend.Native")]
        self.assertEqual(CLOSURE.audit_packages(paths), 4)

    def test_original_omitted_scene_native_fails_with_dependency_identity(self):
        with self.assertRaisesRegex(ValueError, "LibreWPF.ProGPU requires ProGPU.Scene.Native.*absent"):
            CLOSURE.audit_packages([self.bridge()])

    def test_scene_native_transitive_dependency_is_required(self):
        paths = [self.bridge(), self.package("ProGPU.Scene.Native", dependencies=[
            ("ProGPU.Backend.Native", SOURCE_VERSION)])]
        with self.assertRaisesRegex(ValueError, "ProGPU.Scene.Native requires ProGPU.Backend.Native.*absent"):
            CLOSURE.audit_packages(paths)

    def test_native_sdk_transitive_dependency_is_required(self):
        native = self.package("ProGPU.Backend.Native", dependencies=[("ProGPU.Native.Sdk", SOURCE_VERSION)])
        with self.assertRaisesRegex(ValueError, "ProGPU.Native.Sdk.*absent"):
            CLOSURE.audit_packages([native])
        self.assertEqual(CLOSURE.audit_packages([native, self.package("ProGPU.Native.Sdk")]), 2)

    def test_released_preview_cannot_replace_source_commit_package(self):
        with self.assertRaisesRegex(ValueError, "requires ProGPU.Scene.Native.*contains 0.1.0-preview.65"):
            CLOSURE.audit_packages([self.bridge(), self.package("ProGPU.Scene.Native", WPF_VERSION)])

    def test_direct_nonnamespaced_dependencies_are_checked(self):
        bridge = self.package("LibreWPF.ProGPU", WPF_VERSION,
                              [("ProGPU.Scene.Native", SOURCE_VERSION)], False, "")
        with self.assertRaisesRegex(ValueError, "absent"):
            CLOSURE.audit_packages([bridge])

    def test_exact_singleton_range_and_case_insensitive_dependency_id(self):
        bridge = self.package("LibreWPF.ProGPU", WPF_VERSION,
                              [("progpu.scene.native", f"[{SOURCE_VERSION}]")])
        self.assertEqual(CLOSURE.audit_packages([bridge, self.package("ProGPU.Scene.Native")]), 2)

    def test_unbounded_internal_range_is_not_a_pinned_closure(self):
        for constraint in ("", "*", f"[{SOURCE_VERSION},)", f"(,{SOURCE_VERSION}]"):
            with self.subTest(constraint=constraint):
                bridge = self.package("LibreWPF.ProGPU", WPF_VERSION,
                                      [("ProGPU.Scene.Native", constraint)])
                with self.assertRaisesRegex(ValueError, "requires ProGPU.Scene.Native"):
                    CLOSURE.audit_packages([bridge, self.package("ProGPU.Scene.Native")])

    def test_third_party_resolution_remains_with_real_nuget_consumer(self):
        external = self.package("ProGPU.Scene.Native", dependencies=[("Silk.NET.WebGPU", "2.23.0")])
        self.assertEqual(CLOSURE.audit_packages([external]), 1)

    def test_internal_librewpf_dependency_is_required(self):
        package = self.package("LibreWPF.Sdk", WPF_VERSION,
                               [("LibreWPF.Transport", WPF_VERSION)])
        with self.assertRaisesRegex(ValueError, "LibreWPF.Transport.*absent"):
            CLOSURE.audit_packages([package])

    def test_selected_identity_mismatch_and_duplicate_are_rejected(self):
        package = self.package("ProGPU.Scene.Native")
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            CLOSURE.audit_packages([package, package])
        renamed = package.with_name("ProGPU.Scene.Native.0.1.0-preview.65.nupkg")
        package.rename(renamed)
        with self.assertRaisesRegex(ValueError, "identity"):
            CLOSURE.audit_packages([renamed])

    def test_missing_duplicate_and_oversized_nuspec_are_rejected(self):
        for entries in ({}, {"one.nuspec": "", "two.nuspec": ""},
                        {"one.nuspec": "x" * (CLOSURE.MAX_NUSPEC_BYTES + 1)}):
            package = self.directory / "ProGPU.Scene.Native.0.1.0.nupkg"
            with zipfile.ZipFile(package, "w") as archive:
                for name, data in entries.items():
                    archive.writestr(name, data)
            with self.assertRaisesRegex(ValueError, "bounded nuspec"):
                CLOSURE.audit_packages([package])

    def test_empty_feed_is_not_success(self):
        with self.assertRaisesRegex(ValueError, "No selected"):
            CLOSURE.audit_packages([])

    def test_cli_reports_original_missing_package_before_success(self):
        result = subprocess.run([sys.executable, str(CHECKER), str(self.bridge())],
                                capture_output=True, text=True, timeout=10)
        self.assertEqual(result.returncode, 1)
        self.assertIn("ProGPU.Scene.Native", result.stderr)
        self.assertNotIn("verified", result.stdout)

    def selected_packages(self):
        script = 'source "$1"; printf "%s\\n" "${progpu_preview_runtime_package_ids[@]}"'
        result = subprocess.run(["bash", "-c", script, "preview-list",
                                 str(ROOT / "eng/progpu-preview-package-list.sh")],
                                capture_output=True, text=True, check=True, timeout=10)
        return result.stdout.splitlines()

    def test_production_pack_selection_matches_central_runtime_list(self):
        selected = self.selected_packages()
        self.assertIn("ProGPU.Scene.Native", selected)
        sdk = (ROOT / "eng/progpu-wpf-sdk-ci.sh").read_text()
        stages = re.findall(r'^stage_or_pack_progpu_project "([^"]+)" "([^"]+)"$', sdk, re.MULTILINE)
        self.assertEqual([package for _, package in stages], selected)
        self.assertIn(("external/ProGPU/src/ProGPU.Scene.Native/ProGPU.Scene.Native.csproj",
                       "ProGPU.Scene.Native"), stages)
        self.assertIn('source "${repo_root}/eng/progpu-preview-package-list.sh"', sdk)

    def test_actual_snapshot_function_preserves_every_selected_package(self):
        # Exercise only the product snapshot function with owned metadata ZIPs.
        # No native library, managed assembly or fake dotnet process is created.
        selected = self.selected_packages()
        packages = [self.package(package) for package in selected]
        sdk = (ROOT / "eng/progpu-wpf-sdk-ci.sh").read_text()
        function = sdk.split("snapshot_staged_progpu_packages() {", 1)[1].split("\n}\n", 1)[0]
        snapshot = self.directory / "snapshot"
        script = 'source "$1"\nsnapshot_staged_progpu_packages() {' + function + '\n}\nsnapshot_staged_progpu_packages\n'
        environment = dict(os.environ, package_output=str(self.directory),
                           progpu_package_version=SOURCE_VERSION,
                           progpu_package_snapshot_dir=str(snapshot))
        subprocess.run(["bash", "-eu", "-c", script, "preview-snapshot",
                        str(ROOT / "eng/progpu-preview-package-list.sh")], env=environment,
                       capture_output=True, text=True, check=True, timeout=10)
        self.assertEqual(sorted(path.name for path in snapshot.iterdir()), sorted(path.name for path in packages))
        for package in packages:
            self.assertEqual(package.read_bytes(), (snapshot / package.name).read_bytes())

    def test_audit_and_bundle_verify_both_use_actual_payload_closure(self):
        for name in ("progpu-preview-package-audit.sh", "progpu-preview-release-verify.sh"):
            script = (ROOT / "eng" / name).read_text()
            self.assertIn('python3 "${repo_root}/eng/progpu-preview-package-closure.py" "${package_files[@]}"', script)
            self.assertLess(script.index("eng/progpu-preview-package-closure.py"), script.index("verification succeeded")
                            if name.endswith("release-verify.sh") else script.index("audit succeeded"))
        workflow = (ROOT / ".github/workflows/progpu-wpf-sdk.yml").read_text()
        self.assertIn("python3 eng/tests/test_preview_package_closure.py -v", workflow)


if __name__ == "__main__":
    unittest.main()
