#!/usr/bin/env python3
"""Observe the unchanged acceptance driver. This run never qualifies a release."""
import argparse
import ctypes as C
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys
import time


def capture_client(desktop, snapshot, pid, path):
    """Keep actual client pixels, including uniform failures, without source pixels."""
    hwnd = snapshot["hwnd"]
    desktop.owned_window(hwnd, pid)
    if snapshot["pid"] != pid or snapshot["frame"] <= 0:
        raise RuntimeError("Diagnostic requires the original owned presented frame")
    client = desktop.geometry(hwnd, pid)
    x, y, width, height = client
    if not (0 < width * height <= 2_000_000 and width > 0 and height > 0):
        raise RuntimeError("Diagnostic client capture exceeds the fixed bound")
    crop = (x, y, x + width, y + height)
    monitors = desktop.monitors(crop)
    before = desktop.unobstructed(hwnd, crop)
    dc = desktop.u.GetDC(0)
    if not dc:
        raise RuntimeError("Diagnostic screen DC unavailable")
    memory = bitmap = old = None
    try:
        memory = desktop.g.CreateCompatibleDC(dc)
        bitmap = desktop.g.CreateCompatibleBitmap(dc, width, height)
        if not memory or not bitmap:
            raise RuntimeError("Diagnostic capture storage unavailable")
        old = desktop.g.SelectObject(memory, bitmap)
        if not old or old == C.c_void_p(-1).value:
            old = None
            raise RuntimeError("Diagnostic bitmap selection failed")
        if not desktop.g.BitBlt(memory, 0, 0, width, height, dc, x, y, 0x00CC0020):
            raise RuntimeError("Diagnostic desktop BitBlt failed")
        if not desktop.g.SelectObject(memory, old):
            raise RuntimeError("Diagnostic bitmap deselection failed")
        old = None
        header = struct.pack("<IiiHHIIiiII", 40, width, -height, 1, 32, 0,
                             width * height * 4, 0, 0, 0, 0)
        info = C.create_string_buffer(header)
        pixels = C.create_string_buffer(width * height * 4)
        if desktop.g.GetDIBits(dc, bitmap, 0, height, pixels, info, 0) != height:
            raise RuntimeError("Diagnostic desktop pixels incomplete")
        if desktop.geometry(hwnd, pid) != client or desktop.monitors(crop) != monitors:
            raise RuntimeError("Diagnostic desktop geometry changed during capture")
        after = desktop.unobstructed(hwnd, crop)
        raw = pixels.raw
        data = struct.pack("<2sIHHI", b"BM", 54 + len(raw), 0, 0, 54) + header + raw
        with path.open("xb") as output:
            output.write(data)
        rgb = bytes(value for i, value in enumerate(raw) if i % 4 != 3)
        return {"client": client, "crop": crop, "monitors": monitors,
                "zOrderBefore": before, "zOrderAfter": after,
                "firstBgr": list(raw[:3]), "minimumRgbByte": min(rgb),
                "maximumRgbByte": max(rgb), "distinctRgbBytes": len(set(rgb)),
                "rgbSha256": hashlib.sha256(rgb).hexdigest(),
                "fileSha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)}
    finally:
        failures = []
        if old and not desktop.g.SelectObject(memory, old): failures.append("deselect")
        if bitmap and not desktop.g.DeleteObject(bitmap): failures.append("bitmap release")
        if memory and not desktop.g.DeleteDC(memory): failures.append("DC release")
        if not desktop.u.ReleaseDC(0, dc): failures.append("screen DC release")
        if failures:
            error = RuntimeError("Diagnostic cleanup: " + ", ".join(failures))
            if sys.exception() is not None: sys.exception().add_note(str(error))
            else: raise error


def observed_capture(original, observer=capture_client):
    def capture(desktop, snapshot, pid, path):
        record = {"qualified": False, "diagnosticOnly": True, "snapshot": snapshot,
                  "originalDeadlineSeconds": 60, "startedMonotonic": time.monotonic()}
        try:
            record["clientCapture"] = observer(
                desktop, snapshot, pid, path.with_name(path.stem + ".diagnostic-client.bmp"))
        except Exception as error:
            record["diagnosticError"] = str(error)
        record["elapsedSeconds"] = time.monotonic() - record["startedMonotonic"]
        try:
            with path.with_name(path.stem + ".diagnostic.json").open("x") as output:
                json.dump(record, output, indent=2)
        except Exception as error:
            print("Diagnostic receipt failure: " + str(error), file=sys.stderr)
        # Preserve the original capture, assertions, exception and return value.
        return original(desktop, snapshot, pid, path)
    return capture


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--driver", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args, original_arguments = parser.parse_known_args()
    driver = args.driver.resolve(strict=True)
    receipt = json.loads(driver.with_name("preparation.json").read_text())
    digest = hashlib.sha256(driver.read_bytes()).hexdigest()
    if driver.name != "run-windows.py" or digest != receipt["inputs"]["run-windows.py"]:
        raise RuntimeError("Original prepared driver identity mismatch")
    spec = importlib.util.spec_from_file_location("original_gallery_driver", driver)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.Desktop.capture = observed_capture(module.Desktop.capture)
    sys.argv = [str(driver), "--output", str(args.output), *original_arguments]
    record = {"qualified": False, "diagnosticOnly": True, "originalDriverSha256": digest,
              "observerSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              "producer": receipt["head"], "originalDeadlineSeconds": 60}
    print(json.dumps(record))
    try:
        code = module.main()
        record["originalExitCode"] = code
        return code
    finally:
        if args.output.is_dir():
            with (args.output / "diagnostic-run.json").open("x") as output:
                json.dump(record, output, indent=2)


if __name__ == "__main__":
    raise SystemExit(main())
