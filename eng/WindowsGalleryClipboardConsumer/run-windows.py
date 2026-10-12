#!/usr/bin/env python3
"""Root-only actual desktop capture; never build, inject input, or restore guessed clipboard data."""
import argparse
import ctypes as C
from ctypes import wintypes as W
import hashlib
import json
import math
import os
from pathlib import Path
import struct
import subprocess
import sys
import time


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def read_json(path):
    with path.open("rb") as stream:
        data = stream.read(65537)
    require(len(data) <= 65536, "receipt size limit")
    return json.loads(data)


def admitted_crop(snapshot, pid, native_client):
    require(snapshot["pid"] == pid, "foreign snapshot PID")
    require(snapshot["frame"] > 0, "no presented native frame")
    require(snapshot["client"] == list(native_client), "source/native client mismatch; no scaling or titlebar inference")
    image = snapshot["image"]
    require(len(image) == 4 and all(math.isfinite(v) for v in image), "nonfinite image bounds")
    x, y, w, h = image
    cx, cy, cw, ch = native_client
    require(w > 0 and h > 0 and x >= cx and y >= cy and x + w <= cx + cw and y + h <= cy + ch,
            "entire pasted image must be inside actual client")
    crop = (math.floor(x), math.floor(y), math.ceil(x + w), math.ceil(y + h))
    require((crop[2] - crop[0]) * (crop[3] - crop[1]) <= 2_000_000, "capture extent exceeds bound")
    return crop


def require_monitor_coverage(crop, monitors):
    require(0 < len(monitors) <= 16, "physical monitor inventory exceeds bound or is empty")
    remaining = [crop]
    for monitor in monitors:
        require(len(monitor) == 4 and all(isinstance(v, int) for v in monitor)
                and monitor[0] < monitor[2] and monitor[1] < monitor[3], "invalid physical monitor rectangle")
        uncovered = []
        for left, top, right, bottom in remaining:
            x0, y0 = max(left, monitor[0]), max(top, monitor[1])
            x1, y1 = min(right, monitor[2]), min(bottom, monitor[3])
            if x0 >= x1 or y0 >= y1:
                uncovered.append((left, top, right, bottom))
                continue
            for piece in ((left, top, right, y0), (left, y1, right, bottom),
                          (left, y0, x0, y1), (x1, y0, right, y1)):
                if piece[0] < piece[2] and piece[1] < piece[3]:
                    uncovered.append(piece)
        remaining = uncovered
    require(not remaining, "entire image capture must be covered by physical monitors; no off-screen pixels or monitor gaps")


class Desktop:
    def __init__(self):
        require(os.name == "nt", "Windows only")
        self.u = C.WinDLL("user32", use_last_error=True)
        self.g = C.WinDLL("gdi32", use_last_error=True)
        self.callback = C.WINFUNCTYPE(W.BOOL, W.HWND, W.LPARAM)
        self.monitor_callback = C.WINFUNCTYPE(W.BOOL, W.HANDLE, W.HDC, C.POINTER(W.RECT), W.LPARAM)
        def bind(dll, name, result, *args):
            fn = getattr(dll, name); fn.restype = result; fn.argtypes = args
        for name in ("IsWindow", "IsWindowVisible", "IsIconic"):
            bind(self.u, name, W.BOOL, W.HWND)
        bind(self.u, "GetWindowThreadProcessId", W.DWORD, W.HWND, C.POINTER(W.DWORD))
        bind(self.u, "GetClientRect", W.BOOL, W.HWND, C.POINTER(W.RECT))
        bind(self.u, "GetWindowRect", W.BOOL, W.HWND, C.POINTER(W.RECT))
        bind(self.u, "ClientToScreen", W.BOOL, W.HWND, C.POINTER(W.POINT))
        bind(self.u, "GetForegroundWindow", W.HWND)
        bind(self.u, "EnumWindows", W.BOOL, self.callback, W.LPARAM)
        bind(self.u, "EnumDisplayMonitors", W.BOOL, W.HDC, C.POINTER(W.RECT), self.monitor_callback, W.LPARAM)
        bind(self.u, "GetDC", W.HDC, W.HWND)
        bind(self.u, "ReleaseDC", C.c_int, W.HWND, W.HDC)
        bind(self.u, "SetProcessDpiAwarenessContext", W.BOOL, W.HANDLE)
        bind(self.g, "CreateCompatibleDC", W.HDC, W.HDC)
        bind(self.g, "CreateCompatibleBitmap", W.HBITMAP, W.HDC, C.c_int, C.c_int)
        bind(self.g, "SelectObject", W.HANDLE, W.HDC, W.HANDLE)
        bind(self.g, "DeleteObject", W.BOOL, W.HANDLE)
        bind(self.g, "DeleteDC", W.BOOL, W.HDC)
        bind(self.g, "BitBlt", W.BOOL, W.HDC, C.c_int, C.c_int, C.c_int, C.c_int, W.HDC, C.c_int, C.c_int, W.DWORD)
        bind(self.g, "GetDIBits", C.c_int, W.HDC, W.HBITMAP, W.UINT, W.UINT, W.LPVOID, W.LPVOID, W.UINT)
        require(self.u.SetProcessDpiAwarenessContext(W.HANDLE(-4)), "capture process must admit physical per-monitor coordinates")

    def monitors(self, crop):
        result = []
        @self.monitor_callback
        def visit(_, __, rectangle, ___):
            if len(result) >= 16 or not rectangle:
                return False
            r = rectangle.contents
            result.append((r.left, r.top, r.right, r.bottom))
            return True
        require(self.u.EnumDisplayMonitors(None, None, visit, 0), "complete physical monitor enumeration required")
        result.sort()
        require_monitor_coverage(crop, result)
        return result

    def geometry(self, hwnd, pid):
        owner = W.DWORD()
        require(self.u.IsWindow(hwnd) and self.u.IsWindowVisible(hwnd) and not self.u.IsIconic(hwnd), "owned window is not live/visible")
        require(self.u.GetWindowThreadProcessId(hwnd, C.byref(owner)) and owner.value == pid, "window PID mismatch")
        require(self.u.GetForegroundWindow() == hwnd, "owned window is not foreground; no activation attempted")
        r, p = W.RECT(), W.POINT()
        require(self.u.GetClientRect(hwnd, C.byref(r)) and self.u.ClientToScreen(hwnd, C.byref(p)), "native client query failed")
        return (p.x, p.y, r.right - r.left, r.bottom - r.top)

    def unobstructed(self, hwnd, crop):
        examined, blockers, found = [], [], False
        @self.callback
        def visit(other, _):
            nonlocal found
            if other == hwnd:
                found = True; return False
            if len(examined) >= 256:
                blockers.append("window enumeration bound"); return False
            if self.u.IsWindowVisible(other):
                r = W.RECT()
                if not self.u.GetWindowRect(other, C.byref(r)):
                    blockers.append("unavailable preceding window rect"); return False
                examined.append([int(other), r.left, r.top, r.right, r.bottom])
                if max(r.left, crop[0]) < min(r.right, crop[2]) and max(r.top, crop[1]) < min(r.bottom, crop[3]):
                    blockers.append(int(other))
            return True
        self.u.EnumWindows(visit, 0)
        require(found and not blockers, f"capture obstruction or incomplete z-order: {blockers}")
        return examined

    def capture(self, snapshot, pid, path):
        hwnd = snapshot["hwnd"]
        client = self.geometry(hwnd, pid)
        crop = admitted_crop(snapshot, pid, client)
        monitors_before = self.monitors(crop)
        before = self.unobstructed(hwnd, crop)
        x, y, right, bottom = crop
        width, height = right - x, bottom - y
        dc = self.u.GetDC(0); require(dc, "screen DC missing")
        memory = bitmap = old = None
        try:
            memory = self.g.CreateCompatibleDC(dc); require(memory, "capture DC missing")
            bitmap = self.g.CreateCompatibleBitmap(dc, width, height); require(bitmap, "capture bitmap missing")
            old = self.g.SelectObject(memory, bitmap); require(old and old != C.c_void_p(-1).value, "capture selection failed")
            require(self.g.BitBlt(memory, 0, 0, width, height, dc, x, y, 0x00CC0020), "actual desktop capture failed")
            self.g.SelectObject(memory, old); old = None
            info = C.create_string_buffer(struct.pack("<IiiHHIIiiII", 40, width, -height, 1, 32, 0, width * height * 4, 0, 0, 0, 0))
            pixels = C.create_string_buffer(width * height * 4)
            require(self.g.GetDIBits(dc, bitmap, 0, height, pixels, info, 0) == height, "incomplete native pixels")
            require(self.geometry(hwnd, pid) == client, "window changed during capture")
            monitors_after = self.monitors(crop)
            require(monitors_after == monitors_before, "physical monitors changed during capture")
            after = self.unobstructed(hwnd, crop)
            # BI_RGB's unused fourth byte has no alpha semantics; compare exact RGB.
            rgb = bytes(v for i, v in enumerate(pixels.raw) if i % 4 != 3)
            require(len(set(rgb)) > 1, "uniform image-region capture is not accepted")
            data = struct.pack("<2sIHHI", b"BM", 54 + len(pixels.raw), 0, 0, 54) + info.raw[:40] + pixels.raw
            with path.open("xb") as output: output.write(data)
            return {"client": client, "crop": crop, "zOrderBefore": before, "zOrderAfter": after,
                    "physicalMonitorsBefore": monitors_before, "physicalMonitorsAfter": monitors_after,
                    "rgbSha256": hashlib.sha256(rgb).hexdigest(), "fileSha256": hashlib.sha256(data).hexdigest(),
                    "bytes": len(data), "capture": "actual desktop BitBlt; no source pixels used", "qualifiedPixelParity": False}
        finally:
            original_error = sys.exception()
            failures = []
            if old and not self.g.SelectObject(memory, old): failures.append("deselect")
            if bitmap and not self.g.DeleteObject(bitmap): failures.append("bitmap release")
            if memory and not self.g.DeleteDC(memory): failures.append("DC release")
            if not self.u.ReleaseDC(0, dc): failures.append("screen DC release")
            if failures:
                message = "native capture cleanup failed: " + ",".join(failures)
                if original_error is not None: original_error.add_note(message)
                else: raise RuntimeError(message)


def clipboard_mode(allow_replace, require_empty_ci, environment):
    require(allow_replace != require_empty_ci, "Select explicit clipboard replacement permission or the empty GitHub-hosted CI clipboard mode.")
    if require_empty_ci:
        require(environment.get("GITHUB_ACTIONS") == "true"
                and environment.get("RUNNER_ENVIRONMENT") == "github-hosted"
                and environment.get("RUNNER_OS") == "Windows"
                and environment.get("GITHUB_REPOSITORY") == "wieslawsoltes/LibreWPF",
                "Empty CI clipboard mode requires the LibreWPF GitHub-hosted Windows runner.")
        return "--require-empty-ci-clipboard"
    return "--allow-replace-clipboard"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--app", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--allow-replace-clipboard", action="store_true")
    parser.add_argument("--require-empty-ci-clipboard", action="store_true")
    args = parser.parse_args()
    mode = clipboard_mode(args.allow_replace_clipboard, args.require_empty_ci_clipboard, os.environ)
    require(args.app.is_file(), "missing apphost")
    args.output.mkdir()
    started = time.monotonic(); deadline = started + 60
    result = {"qualified": False, "status": "failed", "captures": [], "errors": [], "clipboardMode": mode, "appSha256": hashlib.sha256(args.app.read_bytes()).hexdigest()}
    process = None
    try:
        desktop = Desktop()
        with (args.output / "stdout.log").open("xb") as stdout, (args.output / "stderr.log").open("xb") as stderr:
            process = subprocess.Popen([str(args.app.resolve()), mode, str(args.output.resolve())], stdout=stdout, stderr=stderr, cwd=args.app.parent)
            result["pid"] = process.pid
            for phase in ("pasted", "retained"):
                snapshot_path = args.output / (phase + ".json")
                while not snapshot_path.exists():
                    require(process.poll() is None, "host exited before " + phase)
                    require(time.monotonic() < deadline - 2, "original60-second run budget exhausted")
                    time.sleep(0.025)
                snapshot = read_json(snapshot_path)
                require(process.poll() is None, "host lifetime ended before capture")
                capture = desktop.capture(snapshot, process.pid, args.output / (phase + ".bmp"))
                require(process.poll() is None and time.monotonic() < deadline - 2, "host lifetime/deadline changed during capture")
                capture["phase"] = phase; capture["snapshot"] = snapshot
                result["captures"].append(capture)
                if phase == "retained":
                    first = result["captures"][0]
                    require(capture["crop"] == first["crop"] and capture["rgbSha256"] == first["rgbSha256"], "actual displayed image changed after clipboard clear")
                (args.output / (phase + ".capture-accepted")).touch(exist_ok=False)
            require(process.wait(timeout=max(0.001, deadline - time.monotonic() - 1)) == 0, "host failure")
            host = read_json(args.output / "host-result.json")
            require(host["completed"] and not host["errors"], "host/clipboard cleanup failure")
            result["host"] = host; result["status"] = "completed-scoped-probe"
    except Exception as error:
        result["errors"].append(str(error))
        result["errors"].extend(getattr(error, "__notes__", []))
    finally:
        if process is not None:
            if process.poll() is None:
                process.kill()
                try: process.wait(timeout=max(0.001, deadline - time.monotonic()))
                except Exception as error: result["errors"].append("owned process cleanup: " + str(error))
            result["exitCode"] = process.poll()
            result["ownedProcessExited"] = process.poll() is not None
            if not result["ownedProcessExited"]: result["errors"].append("owned process remains live")
        result["elapsed"] = time.monotonic() - started
        if result["elapsed"] > 60: result["errors"].append("original60-second deadline exceeded")
        if result["errors"]: result["status"] = "failed"
        with (args.output / "receipt.json").open("x") as output: json.dump(result, output, indent=2)
    return 0 if result["status"] == "completed-scoped-probe" else 1


if __name__ == "__main__":
    raise SystemExit(main())
