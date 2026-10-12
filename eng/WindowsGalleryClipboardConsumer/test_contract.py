"""Offline admission controls only; importing these modules does not open a desktop."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parent


def load(name, file):
    spec = importlib.util.spec_from_file_location(name, ROOT / file)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


prepare = load("gallery_prepare", "prepare.py")
probe = load("gallery_probe", "run-windows.py")
prepare_ci = load("gallery_prepare_ci", "prepare-ci.py")


class AdmissionTests(unittest.TestCase):
    def test_manual_clipboard_replacement_still_requires_explicit_selection(self):
        self.assertEqual("--allow-replace-clipboard", probe.clipboard_mode(True, False, {}))
        for allow, empty in ((False, False), (True, True)):
            with self.subTest(allow=allow, empty=empty), self.assertRaises(RuntimeError):
                probe.clipboard_mode(allow, empty, {})

    def test_empty_ci_mode_rejects_local_self_hosted_and_foreign_runners(self):
        environment = {"GITHUB_ACTIONS": "true", "RUNNER_ENVIRONMENT": "github-hosted",
                       "RUNNER_OS": "Windows", "GITHUB_REPOSITORY": "wieslawsoltes/LibreWPF"}
        self.assertEqual("--require-empty-ci-clipboard", probe.clipboard_mode(False, True, environment))
        for key in environment:
            changed = dict(environment); changed.pop(key)
            with self.subTest(missing=key), self.assertRaises(RuntimeError):
                probe.clipboard_mode(False, True, changed)
        for key, value in (("RUNNER_ENVIRONMENT", "self-hosted"), ("RUNNER_OS", "Linux"),
                           ("GITHUB_REPOSITORY", "another/repository")):
            changed = dict(environment); changed[key] = value
            with self.subTest(key=key, value=value), self.assertRaises(RuntimeError):
                probe.clipboard_mode(False, True, changed)

    def snapshot(self):
        return {"pid": 12, "frame": 2, "client": [-500, 20, 400, 300], "image": [-480, 60, 198, 198]}

    def test_exact_native_client_and_negative_origin(self):
        self.assertEqual((-480, 60, -282, 258), probe.admitted_crop(self.snapshot(), 12, (-500, 20, 400, 300)))

    def test_foreign_process(self):
        with self.assertRaisesRegex(RuntimeError, "PID"):
            probe.admitted_crop(self.snapshot(), 13, (-500, 20, 400, 300))

    def test_no_frame(self):
        value = self.snapshot(); value["frame"] = 0
        with self.assertRaisesRegex(RuntimeError, "frame"):
            probe.admitted_crop(value, 12, (-500, 20, 400, 300))

    def test_no_dpi_ratio_repair(self):
        with self.assertRaisesRegex(RuntimeError, "mismatch"):
            probe.admitted_crop(self.snapshot(), 12, (-1000, 40, 800, 600))

    def test_partial_image_is_not_cropped_into_success(self):
        value = self.snapshot(); value["image"][0] = -501
        with self.assertRaisesRegex(RuntimeError, "entire"):
            probe.admitted_crop(value, 12, (-500, 20, 400, 300))

    def test_nonfinite_image(self):
        value = self.snapshot(); value["image"][0] = float("nan")
        with self.assertRaisesRegex(RuntimeError, "nonfinite"):
            probe.admitted_crop(value, 12, (-500, 20, 400, 300))

    def test_empty_image(self):
        value = self.snapshot(); value["image"][2] = 0
        with self.assertRaisesRegex(RuntimeError, "entire"):
            probe.admitted_crop(value, 12, (-500, 20, 400, 300))

    def test_physical_monitor_coverage_handles_negative_origin_and_adjoining_displays(self):
        probe.require_monitor_coverage((-480, 60, -282, 258), [(-500, 0, 0, 300)])
        probe.require_monitor_coverage((-100, 20, 100, 220), [(-500, 0, 0, 300), (0, 0, 500, 300)])

    def test_monitor_bounding_box_cannot_hide_a_gap_or_offscreen_pixels(self):
        for crop, monitors in (
            ((-100, 20, 100, 220), [(-500, 0, 0, 300), (1, 0, 500, 300)]),
            ((0, -1, 100, 220), [(0, 0, 500, 300)]),
            ((400, 20, 501, 220), [(0, 0, 500, 300)]),
        ):
            with self.subTest(crop=crop, monitors=monitors), self.assertRaisesRegex(RuntimeError, "entire image capture"):
                probe.require_monitor_coverage(crop, monitors)

    def test_empty_excessive_or_degenerate_monitor_inventory_rejected(self):
        for monitors in ([], [(0, 0, 100, 100)] * 17, [(0, 0, 0, 100)]):
            with self.subTest(monitors=monitors), self.assertRaises(RuntimeError):
                probe.require_monitor_coverage((0, 0, 50, 50), monitors)

    def test_original_manifest_includes_source_asset_and_license(self):
        value = json.loads((ROOT / "source-manifest.json").read_text())
        self.assertEqual("811d01e95c8c929e68539d698d0a0609e94fd185", value["commit"])
        files = {entry["path"]: entry for entry in value["files"]}
        self.assertEqual(12, len(files))
        self.assertIn("LICENSE", files)
        self.assertEqual("f1c9a6e709e0f7fcaf048f06074dbd7d2addcdc7576e82efe618632f433ee799",
                         files["Assets/ControlImages/Clipboard.png"]["sha256"])
        self.assertIn("Views/System/ClipboardPage.xaml.cs", files)


class ProducerTests(unittest.TestCase):
    def test_private_build_disables_unrelated_first_run_side_effects(self):
        self.assertEqual({"DOTNET_GENERATE_ASPNET_CERTIFICATE": "false",
                          "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH": "false",
                          "DOTNET_CLI_TELEMETRY_OPTOUT": "1"}, prepare.BUILD_ENVIRONMENT)

    def test_explicit_rid_output_layout_matches_asset_verifier(self):
        for architecture in ("arm64", "x64"):
            command = prepare.build_command(None, Path("case.csproj"), architecture, Path("NuGet.config"), Path("packages"))
            self.assertEqual("win-" + architecture, command[command.index("-r") + 1])
            self.assertIn("-p:AppendRuntimeIdentifierToOutputPath=false", command)
            self.assertEqual(Path("bin/Release/net10.0-windows"), prepare.OUTPUT_RELATIVE)

    def receipts(self):
        head = "1" * 40
        success = {"status": "completed", "conclusion": "success"}
        build = dict(success, databaseId=1, name="LibreWPF Build", headSha=head,
                     jobs=[dict(success, name=name, steps=[dict(success, name="Upload CI package bundle")])
                           for name in sorted(prepare.BUILD_JOBS)])
        docs = dict(success, databaseId=2, name="LibreWPF Docs", headSha=head,
                    jobs=[dict(success, name="Verify release documentation")])
        artifact = {"name": "librewpf-ci-packages-" + head, "expired": False,
                    "workflow_run": {"id": 1, "head_sha": head}, "digest": "sha256:" + "2" * 64}
        return build, docs, artifact

    def validate(self, receipts):
        prepare.validate_producer(*receipts, "1" * 40, 1, 2)

    def test_whole_exact_producer_required(self):
        self.validate(self.receipts())

    def test_ci_requires_every_current_bootstrap_job_beside_original_inventory(self):
        receipts = self.receipts()
        with self.assertRaisesRegex(ValueError, "three current SDK bootstrap"):
            prepare_ci.validate_ci_producer(*receipts, "1" * 40, 1, 2)
        bootstrap = [dict(name=name, status="completed", conclusion="success")
                     for name in sorted(prepare_ci.BOOTSTRAP_JOBS)]
        receipts[0]["jobs"].extend(bootstrap)
        prepare_ci.validate_ci_producer(*receipts, "1" * 40, 1, 2)
        for name in prepare_ci.BOOTSTRAP_JOBS:
            incomplete = copy.deepcopy(receipts)
            incomplete[0]["jobs"] = [job for job in incomplete[0]["jobs"] if job["name"] != name]
            with self.subTest(missing=name), self.assertRaisesRegex(ValueError, "three current SDK bootstrap"):
                prepare_ci.validate_ci_producer(*incomplete, "1" * 40, 1, 2)

    def test_additional_jobs_must_also_succeed(self):
        receipts = self.receipts()
        receipts[0]["jobs"].append({"name": "Additional required gate", "status": "completed", "conclusion": "success"})
        self.validate(receipts)
        receipts[0]["jobs"][-1]["conclusion"] = "failure"
        with self.assertRaises(ValueError):
            self.validate(receipts)

    def test_green_producer_job_does_not_waive_failed_or_canceled_build(self):
        for conclusion in ("failure", "cancelled", "skipped", None):
            receipts = self.receipts(); receipts[0]["conclusion"] = conclusion
            with self.subTest(conclusion=conclusion), self.assertRaises(ValueError):
                self.validate(receipts)

    def test_missing_failed_and_duplicate_jobs_rejected(self):
        original = self.receipts()
        for mode in ("missing", "failed", "duplicate"):
            receipts = copy.deepcopy(original)
            if mode == "missing": receipts[0]["jobs"].pop()
            elif mode == "failed": receipts[0]["jobs"][0]["conclusion"] = "failure"
            else: receipts[0]["jobs"][0] = receipts[0]["jobs"][1]
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                self.validate(receipts)

    def test_live_docs_or_wrong_head_rejected(self):
        for field, value in (("status", "in_progress"), ("headSha", "3" * 40)):
            receipts = self.receipts(); receipts[1][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.validate(receipts)

    def test_other_producer_or_expired_artifact_rejected(self):
        for mode in ("other-run", "expired", "no-digest"):
            receipts = self.receipts()
            if mode == "other-run": receipts[2]["workflow_run"]["id"] = 3
            elif mode == "expired": receipts[2]["expired"] = True
            else: receipts[2].pop("digest")
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                self.validate(receipts)

    def test_upload_failure_rejected(self):
        receipts = self.receipts()
        producer = next(j for j in receipts[0]["jobs"] if j["name"] == "SDK package and no-source-change smoke")
        producer["steps"][0]["conclusion"] = "failure"
        with self.assertRaises(ValueError):
            self.validate(receipts)


if __name__ == "__main__":
    unittest.main()
