"""Self-test of the Abyss QA tools on synthetic data (no Unity needed).

Usage:
    python -I Tools/qa/selftest.py [--keep] [--skip-encode]

Generates images, frame sequences and fake Claude Code transcripts under QA/tmp/selftest/ (gitignored), runs every
tool as a separate ``python -I`` process exactly as the lead will, and asserts the expected verdicts:
* abyss_qa.py regions - fractions are clamped, never reread as pixels ({"px": [...]} is explicit); tiny / empty /
  malformed regions are errors; CLI fractions outside 0..1 are rejected; report duplicates are found; a
  {"rects": [...]} union covers both horizon side ranges and nothing between them; the doorway shot is KS1b only.
* image_metrics.py doorway - KS1b aperture luma 0.50 passes (calibrated band 0.40-0.60, lead decision D6), 0.39 and
  0.80 fail, 1 % clipped inside it fails (the frame-level
  0.3 % still passes), the default KS1b aperture is used without a region; a doorway region on KS1 is never judged.
* image_metrics.py horizon - symmetric side-range samples skip a centred door, lamp and +2-level vignette and agree;
  a centred sky box and horizon row over the door fail.
* image_metrics.py - a clean IGN-dithered beat-2 frame passes; an undithered (banded) copy fails banding; magenta
  block, isolated white speck, black frame and size mismatch each fail the matching check; an overshooting
  fractional region and an explicit pixel region still measure the band; a tiny region and a bare pixel list fail
  with an error; the same tag at two resolutions gives two entries; single-image mode checks horizon agreement
  (pass / fail), the -nopost OUT_HORIZ row and the abyss nadir (pass / fail), and exits 2 on a pixel rectangle
  written without px:.
* contact_sheet.py - writes a sheet with pass / fail bars; tiles match metrics by file, not tag.
* sequence_metrics.py - a 1-frame pop is found by the 4-sigma rule, a noisy thin-line mask fails the temporal std,
  a static deck passes; an exposure ramp fails the luma-step and EV-rate checks.
* encode_test.py - a banded sequence fails the gated x264 CRF 20 encode; a dithered one is encoded, decoded and
  scored (verdict reported), crops are written.
* compare.py - matched by file: only the modified file shows a difference, its other resolution does not.
* ../metrics.py - exact token, dedup, gap, active-time, cost and report-section numbers on fake transcripts,
  including exclusion of a foreign-cwd file and a sibling project folder.
* no tool wrote a .pyc / __pycache__ into Tools/.
Exit code 0 when every assertion holds.
"""

from __future__ import annotations

import sys

sys.dont_write_bytecode = True   # never write __pycache__ into Tools/ (python -I does not imply -B)

import argparse  # noqa: E402
import json  # noqa: E402
import os  # noqa: E402
import shutil  # noqa: E402
import subprocess  # noqa: E402
import time  # noqa: E402
from typing import List  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import abyss_qa as qa  # noqa: E402
import numpy as np  # noqa: E402
from PIL import Image  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
TMP = os.path.join(qa.QA_DIR, "tmp", "selftest")
FAILS: List[str] = []


def check(cond: bool, msg: str) -> None:
    print(("  ok   " if cond else "  FAIL ") + msg)
    if not cond:
        FAILS.append(msg)


def run(script: str, *args: str, expect: tuple = (0, 1)) -> subprocess.CompletedProcess:
    cmd = [sys.executable, "-I", script] + list(args)
    p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if p.returncode not in expect:
        print(p.stdout[-2000:])
        print(p.stderr[-2000:])
    return p


def ign(w: int, h: int, frame: int = 0) -> np.ndarray:
    """Interleaved gradient noise (Jimenez 2014) in 0..1, the static dither pattern family the game uses."""
    y, x = np.mgrid[0:h, 0:w].astype(np.float64)
    x = x + 5.588238 * frame
    y = y + 5.588238 * frame
    return np.mod(52.9829189 * np.mod(0.06711056 * x + 0.00583715 * y, 1.0), 1.0)


def dither15(w: int, h: int, frame: int = 0) -> np.ndarray:
    """IGN dither of 1.5 LSB peak-to-peak (the spec's 1-1.5 LSB range). Measured on this gradient: IGN 1.0 LSB leaves
    13 px runs in the gentle lower 40 % (fails 12 px), IGN 1.5 LSB 3 px, white noise of any amplitude 17-24 px."""
    return 0.5 + 1.5 * (ign(w, h, frame) - 0.5)


def fog_gradient(w: int, h: int) -> np.ndarray:
    """Dark beat-2-like fog: upper 30 % 64 -> 36 levels, middle 36 -> 30, lower 40 % 30 -> 2 (float levels)."""
    t = (np.arange(h) + 0.5) / h
    v = np.where(t < 0.3, 64 - (t / 0.3) * 28, np.where(t < 0.6, 36 - ((t - 0.3) / 0.3) * 6,
                                                      30 - ((t - 0.6) / 0.4) * 28))
    base = np.repeat(v[:, None], w, axis=1)
    return np.stack([base * 0.85, base, base * 1.2], axis=2)


def quantise(levels: np.ndarray, dither: np.ndarray = None) -> np.ndarray:
    d = 0.5 if dither is None else dither[..., None] if dither.ndim == 2 else dither
    return np.clip(np.floor(levels + d), 0, 255).astype(np.uint8)


def save(a: np.ndarray, path: str) -> None:
    qa.ensure_dir(os.path.dirname(path))
    Image.fromarray(a).save(path)


# ----------------------------------------------------------------------------------------------------------------------
def test_regions() -> None:
    print("[abyss_qa regions]")
    W, H = 1920, 1080
    check(qa.rect_to_px([0.0, 0.6, 1.02, 1.0], W, H) == (0, 648, 1920, 1080),
          "fraction overshooting to 1.02 is clamped, not reread as pixels")
    check(qa.rect_to_px([-0.01, 0.0, 0.5, 0.5], W, H) == (0, 0, 960, 540), "negative fraction is clamped to 0")
    check(qa.region_mask({"px": [10, 20, 110, 220]}, W, H)[0] == (10, 20, 110, 220), "explicit {px: [...]} rectangle")
    box, m = qa.region_mask({"circle_px": [100, 100, 20]}, W, H)
    check(box == (80, 80, 120, 120) and m is not None and 1200 < int(m.sum()) < 1320,
          "explicit circle_px (box %s, %d px)" % (box, int(m.sum()) if m is not None else -1))
    check(qa.resolve_region([0, 0, 1, 1], W, H)[2] is None, "full-frame fraction region is valid")
    err = qa.resolve_region([0.5, 0.5, 0.503, 0.503], W, H)[2]
    check(err is not None and "minimum" in err, "region under 8x8 px is an error (%s)" % err)
    err = qa.resolve_region([0, 400, 600, 500], W, H)[2]
    check(err is not None and '"px"' in err, "bare pixel list clamps to an empty region: error with px hint")
    err = qa.resolve_region({"circle": [0.5, 0.5, 0.002]}, W, H)[2]
    check(err is not None, "tiny circle is an error")
    check(qa.resolve_region({"box": [0, 0, 1, 1]}, W, H)[2] is not None and
          qa.resolve_region([0, 0, 1], W, H)[2] is not None, "malformed regions are errors, not exceptions")
    check(qa.parse_rect_arg("fog=0,0,1,0.3") == ("fog", [0.0, 0.0, 1.0, 0.3]), "CLI fraction rectangle")
    check(qa.parse_rect_arg("deck=px:20,300,220,340") == ("deck", {"px": [20.0, 300.0, 220.0, 340.0]}),
          "CLI px: rectangle")
    check(qa.parse_rect_arg("circle:0.5,0.5,0.3") == ("", {"circle": [0.5, 0.5, 0.3]}), "CLI circle")
    sides = {"rects": [[0.02, 0.40, 0.25, 0.42], [0.75, 0.40, 0.98, 0.42]]}
    box, m = qa.region_mask(sides, W, H)
    check(box == (38, 432, 1882, 454) and m is not None and int(m.sum()) == 2 * 442 * 22 and
          not m[:, 480 - 38:1440 - 38].any(), "rects: union of both side ranges, centre columns excluded (box %s, %d px)"
          % (box, int(m.sum()) if m is not None else -1))
    check(qa.resolve_region(sides, W, H)[2] is None and qa.resolve_region({"rects": []}, W, H)[2] is not None and
          qa.resolve_region({"rects": [[0.5, 0.5, 0.5, 0.6]]}, W, H)[2] is not None,
          "rects: valid union; empty list and zero-width union are errors")
    check(qa.parse_rect_arg("sky=rects:0.02,0.4,0.25,0.42/0.75,0.4,0.98,0.42") ==
          ("sky", {"rects": [[0.02, 0.4, 0.25, 0.42], [0.75, 0.4, 0.98, 0.42]]}), "CLI rects: union")
    try:
        qa.parse_rect_arg("rects:px:1,2,30,40")
        rejected = False
    except ValueError:
        rejected = True
    check(rejected, "CLI rects: with a pixel rectangle is rejected")
    check(qa.is_doorway_shot({"tag": "KS1b_doorway_4k", "base_tag": "KS1b_doorway"}) and
          qa.is_doorway_shot({"file": "beat01_gallery_KS1b_doorway.png"}) and
          not qa.is_doorway_shot({"tag": "KS1_gallery_start", "base_tag": "KS1_gallery_start"}) and
          not qa.is_doorway_shot({"tag": "B1_end"}), "doorway shot = KS1b only (not KS1, not the beat-1 stills)")
    try:
        qa.parse_rect_arg("deck=20,300,220,340")
        rejected = False
    except ValueError as e:
        rejected = "px:" in str(e)
    check(rejected, "CLI pixel rectangle without px: is rejected")
    img = np.full((H, W, 3), 128, np.uint8)
    hz = qa.horizon_agreement(img, {"horizon_fog": [0, 400, 600, 500], "horizon_sky": [0.3, 0.4, 0.6, 0.5]})
    check(hz is not None and hz["pass"] is False and bool(hz.get("error")),
          "horizon agreement with an empty region fails (was a NaN pass)")
    nd = qa.nadir_check(img, [0.5, 0.5, 0.5, 0.5])
    check(nd["pass"] is False and bool(nd.get("error")), "nadir on an empty region fails")
    hr = qa.horizon_vs_reference(img, [0.3, 0.5, 0.7, 0.503])
    check(hr["pass"] is False and bool(hr.get("error")), "horizon row of 3 px height fails")
    check(qa.banding_score(img, (5, 5, 5, 400)).get("error") is not None, "banding_score flags an empty box")
    shots = [{"tag": "KS2a", "file": "a.png", "width": 1920, "height": 1080},
             {"tag": "KS2a", "file": "a_4k.png", "width": 3840, "height": 2160},
             {"tag": "KS2a", "file": "a.png", "width": 3840, "height": 2160},      # the 4K capture overwrote a.png
             {"tag": "KS2a", "file": "b.png", "width": 1920, "height": 1080}]      # same tag + size in a 2nd file
    d = qa.report_duplicates(shots, TMP)
    check(len(d["files"]) == 1 and d["files"][0]["file"] == "a.png" and len(d["files"][0]["shots"]) == 2,
          "duplicate file reference found (%s)" % d["files"])
    ts = sorted((x["size"], sorted(x["files"])) for x in d["tag_sizes"])
    check(ts == [("1920x1080", ["a.png", "b.png"]), ("3840x2160", ["a.png", "a_4k.png"])],
          "duplicate (tag, size) pairs found (%s)" % ts)


# ----------------------------------------------------------------------------------------------------------------------
def test_images() -> str:
    print("[image_metrics / contact_sheet / compare]")
    W, H = 1920, 1080
    cap = os.path.join(TMP, "captures", "selftest_run")
    if os.path.isdir(cap):
        shutil.rmtree(cap)
    fog = fog_gradient(W, H)
    good = quantise(fog, dither15(W, H))
    banded = quantise(np.floor(fog / 3.0) * 3.0)          # posterised: 3-level steps, 30-50 px bands
    white = quantise(fog, np.random.default_rng(3).random((H, W)))   # 1 LSB white-noise dither
    mag = good.copy()
    mag[500:506, 900:906] = (255, 0, 255)
    speck = good.copy()
    speck[700, 1000] = (255, 255, 255)
    black = np.zeros_like(good)
    good720 = quantise(fog_gradient(1280, 720), dither15(1280, 720))
    # R1-38 false positive: dithered fog with a flat 40-level panel (no gradient, a plateau of one value) whose top edge is
    # a hard step, on the same vertical lines as the fog gradient
    plateau = good.copy()
    plateau[700:760, 300:340] = (40, 47, 56)
    shots = []

    def add(tag, a, fn=None, wh=None, regions=None):
        fn = fn or "beat02_bridge_%s.png" % tag
        save(a, os.path.join(cap, fn))
        wh = wh or (a.shape[1], a.shape[0])
        s = {"tag": tag, "beat": 2, "file": fn, "width": wh[0], "height": wh[1],
             "pose": {"pos": [0, -1.3, 120], "yaw": 0, "pitch": -10, "fov": 66}, "clock": 0.0}
        if regions:
            s["regions"] = regions
        shots.append(s)

    for tag, a in (("KS2a_good", good), ("KS2a_banded", banded), ("KS2a_whitenoise", white),
                   ("KS2a_magenta", mag), ("KS2a_speck", speck), ("KS2a_black", black), ("KS2a_plateau_edge", plateau)):
        add(tag, a)
    add("KS2a_size", good, wh=(3840, 2160))
    add("KS2a_good", good720, fn="beat02_bridge_KS2a_good_1280x720.png")        # same tag, second resolution
    add("KS2a_overshoot", banded, regions={"banding": {"void": [-0.01, 0.6, 1.02, 1.0]}})
    add("KS2a_pxregion", banded, regions={"banding": {"void": {"px": [0, 648, 1920, 1080]}}})
    add("KS2a_tiny", good, regions={"banding": {"tiny": [0.5, 0.5, 0.502, 0.502]},
                                    "horizon_fog": [0, 400, 600, 500], "horizon_sky": [0.3, 0.4, 0.6, 0.5]})
    qa.write_json(os.path.join(cap, "report.json"), {"run": "selftest_run", "shots": shots})
    out = os.path.join(TMP, "metrics", "selftest_run")
    p = run(os.path.join(HERE, "image_metrics.py"), cap, "--out", out, "--quiet")
    check(p.returncode == 1, "image_metrics exits 1 when shots fail (got %d)" % p.returncode)
    m = json.load(open(os.path.join(out, "metrics.json"), encoding="utf-8"))
    by = {s["file"]: s for s in m["shots"]}

    def shot(tag):
        return by["beat02_bridge_%s.png" % tag]

    def nonfloor(sh):
        # the 14.2 floor checks: near_black_pct (2x2 opening) and, since R2-16, black_runs (1-px runs at <= 2 levels)
        return [f for f in sh["failures"] if f not in ("near_black_pct", "black_runs")]

    check(len(m["shots"]) == len(shots) and len(by) == len(shots), "one metrics entry per file (%d)" % len(by))
    g = shot("KS2a_good")
    # the synthetic void goes down to 2 levels: a 14.2 near-black floor defect (judged since R1-29), nothing else fails
    check(nonfloor(g) == [] and "near_black_pct" in g["failures"],
          "clean dithered frame passes every check but the near-black floor (failures: %s)" % g.get("failures"))
    check(0.05 <= g["luma"]["mean"] <= 0.13, "clean frame mean luma %.3f in beat-2 range" % g["luma"]["mean"])
    check(max(b["max_run"] for b in g["banding"]) <= 12, "clean frame band run <= 12 px (%s)" %
          [b["max_run"] for b in g["banding"]])
    g7 = by["beat02_bridge_KS2a_good_1280x720.png"]
    check(g7["tag"] == "KS2a_good" and g7["width"] == 1280 and nonfloor(g7) == [] and g["width"] == 1920,
          "same tag at 1920x1080 and 1280x720: two separate passing entries (%s)" % g7.get("failures"))
    b = shot("KS2a_banded")
    check(any(f.startswith("banding") for f in b["failures"]), "undithered gradient fails banding (runs %s)" %
          [x["max_run"] for x in b["banding"]])
    wn = shot("KS2a_whitenoise")
    # R1-38: white-noise dither leaves long runs by chance (raw rule: 17-25 px); the confirmed-band rule keeps only runs
    # that 5 adjacent lines share, which white noise produces rarely (statistical, near the 1-2 level clamp): information
    check(max(x["raw_max_run"] for x in wn["banding"]) > 12, "1 LSB white-noise dither: raw runs > 12 px (%s)" %
          [x["raw_max_run"] for x in wn["banding"]])
    print("       white noise, confirmed band runs (information): %s" % [x["max_run"] for x in wn["banding"]])
    fp = shot("KS2a_plateau_edge")
    check(not any(f.startswith("banding") for f in fp["failures"]) and max(x["raw_max_run"] for x in fp["banding"]) > 12,
          "flat plateau beside a hard edge: raw run %s px, no confirmed band (R1-38 false positive)" %
          [x["raw_max_run"] for x in fp["banding"]])
    check(shot("KS2a_magenta")["checks"]["magenta_ratio"]["pass"] is False, "magenta block detected")
    check("nan_like_pixels" in shot("KS2a_speck")["failures"] and
          shot("KS2a_speck")["specks"]["samples"][0] == [1000, 700], "isolated white speck detected at (1000, 700)")
    check("black_frame" in shot("KS2a_black")["failures"], "black frame detected")
    check(nonfloor(shot("KS2a_size")) == ["size"], "size mismatch detected and nothing else (%s)" %
          shot("KS2a_size")["failures"])
    for tag in ("KS2a_overshoot", "KS2a_pxregion"):
        bv = shot(tag)["banding"][0]
        check("banding:void" in shot(tag)["failures"] and not bv.get("error") and
              bv["box_px"] == [0, 648, 1920, 1080] and bv["max_run"] > 12,
              "%s: lower-void region measured on the full box (box %s, run %s)" % (
                  tag, bv.get("box_px"), bv.get("max_run")))
    t = shot("KS2a_tiny")
    check("banding:tiny" in t["failures"] and t["checks"]["banding:tiny"]["value"] is None and
          "minimum" in t["checks"]["banding:tiny"].get("error", ""), "tiny banding region fails with an error")
    check("horizon_agreement" in t["failures"] and '"px"' in t["checks"]["horizon_agreement"].get("error", ""),
          "bare pixel horizon region fails with a px hint")
    check(any("beat02_bridge_KS2a_tiny.png" in f for f in m["summary"]["failures"]), "failure list names the file")
    check(m["duplicates"] == {"files": [], "tag_sizes": []}, "no duplicates reported (%s)" % m["duplicates"])
    check(os.path.isfile(os.path.join(out, "summary.md")), "summary.md written")

    # single-image mode: horizon agreement, nopost horizon row, nadir
    hz = np.zeros((360, 640, 3), np.uint8)
    hz[:, :] = qa.OUT_HORIZ_RGB
    hz[:, 0:200] = (131, 151, 166)        # fog sample within 1 level
    hz[:, 440:640] = (133, 149, 164)      # backdrop within 1 level
    hpath = os.path.join(TMP, "single", "horizon_ok.png")
    save(hz, hpath)
    p = run(os.path.join(HERE, "image_metrics.py"), "--single", hpath, "--beat", "7", "--zone", "outside",
            "--horizon-fog", "0,0.4,0.3,0.6", "--horizon-sky", "0.35,0.4,0.65,0.6",
            "--horizon-backdrop", "0.7,0.4,1,0.6", "--horizon-row", "0.35,0.47,0.65,0.53", "--nopost",
            "--no-fail-exit")
    r = json.loads(p.stdout)
    check(r["horizon"]["pass"] and r["horizon"]["max_diff_levels"] <= 3, "horizon colours agree (%.1f levels)" %
          r["horizon"]["max_diff_levels"])
    check(r["horizon_row"]["applies"] and r["horizon_row"]["pass"], "-nopost horizon row equals OUT_HORIZ")
    hz2 = hz.copy()
    hz2[:, 440:640] = (139, 150, 165)
    save(hz2, hpath.replace("ok", "bad"))
    p = run(os.path.join(HERE, "image_metrics.py"), "--single", hpath.replace("ok", "bad"), "--beat", "7",
            "--horizon-fog", "0,0.4,0.3,0.6", "--horizon-sky", "0.35,0.4,0.65,0.6",
            "--horizon-backdrop", "0.7,0.4,1,0.6", "--no-fail-exit")
    r = json.loads(p.stdout)
    check(not r["horizon"]["pass"], "horizon mismatch of 7 levels fails (%.1f)" % r["horizon"]["max_diff_levels"])
    # KS7c-like frame: OUT_HORIZ at the sides, the centre +2 levels (vignette) with the dark goal door and its lamp at
    # columns 0.477-0.523: the symmetric side samples agree, a centred sky box or horizon row does not
    hz3 = np.zeros((360, 640, 3), np.float64)
    hz3[:, :] = qa.OUT_HORIZ_RGB
    hz3[:, 192:448] += 2.0
    hz3[126:198, 305:335] = (30, 32, 36)
    hz3[118:126, 314:326] = (255, 190, 90)
    hz3 = np.clip(np.rint(hz3), 0, 255).astype(np.uint8)
    hpath3 = os.path.join(TMP, "single", "horizon_door.png")
    save(hz3, hpath3)
    side = lambda y0, y1: "rects:0.02,%g,0.25,%g/0.75,%g,0.98,%g" % (y0, y1, y0, y1)   # noqa: E731
    p = run(os.path.join(HERE, "image_metrics.py"), "--single", hpath3, "--beat", "7", "--zone", "outside",
            "--horizon-sky", side(0.40, 0.44), "--horizon-fog", side(0.48, 0.52),
            "--horizon-backdrop", side(0.53, 0.57), "--horizon-row", side(0.46, 0.49), "--nopost", "--no-fail-exit")
    r = json.loads(p.stdout)
    check(r["horizon"]["pass"] and r["horizon"]["max_diff_levels"] < 0.5 and r["horizon_row"]["pass"],
          "side-range horizon samples skip the door, lamp and vignette (%.2f levels)" % r["horizon"]["max_diff_levels"])
    p = run(os.path.join(HERE, "image_metrics.py"), "--single", hpath3, "--beat", "7", "--zone", "outside",
            "--horizon-sky", "0.35,0.40,0.65,0.44", "--horizon-fog", side(0.48, 0.52),
            "--horizon-row", "0.30,0.46,0.70,0.49", "--nopost", "--no-fail-exit")
    r = json.loads(p.stdout)
    check(not r["horizon"]["pass"] and not r["horizon_row"]["pass"],
          "a centred sky box / horizon row over the door fails (%.1f levels)" % r["horizon"]["max_diff_levels"])
    nd = quantise(np.broadcast_to(np.array(qa.FOG_DOWN_RGB, np.float64), (360, 640, 3)) - 0.5, ign(640, 360))
    npath = os.path.join(TMP, "single", "nadir_ok.png")
    save(nd, npath)
    p = run(os.path.join(HERE, "image_metrics.py"), "--single", npath, "--nadir", "0.2,0.2,0.8,0.8",
            "--no-fail-exit")
    r = json.loads(p.stdout)
    check(r["nadir"]["pass"], "FOG_DOWN nadir passes (mean %.4f, max %.4f, edge %.4f)" % (
        r["nadir"]["mean_luma"], r["nadir"]["max_pixel_luma"], r["nadir"]["max_horizontal_edge"]))
    p = run(os.path.join(HERE, "image_metrics.py"), "--single", npath, "--nadir", "px:128,72,512,288",
            "--no-fail-exit")
    r = json.loads(p.stdout)
    check(r["nadir"]["pass"] and r["nadir"]["mean_luma"] is not None, "nadir with a px: region passes")
    p = run(os.path.join(HERE, "image_metrics.py"), "--single", npath, "--nadir", "128,72,512,288", expect=(2,))
    check(p.returncode == 2 and "px:" in p.stderr, "pixel rectangle without px: on the CLI exits 2 (rc %d)" %
          p.returncode)
    nd2 = nd.copy()
    nd2[180:, :] = np.clip(nd2[180:, :].astype(int) + 14, 0, 255).astype(np.uint8)
    save(nd2, npath.replace("ok", "bad"))
    p = run(os.path.join(HERE, "image_metrics.py"), "--single", npath.replace("ok", "bad"), "--nadir",
            "0.2,0.2,0.8,0.8", "--no-fail-exit")
    r = json.loads(p.stdout)
    check(not r["nadir"]["pass"], "nadir with a step edge fails (edge %.3f)" % r["nadir"]["max_horizontal_edge"])

    # contact sheet with metrics colouring; tiles must match metrics by file, not by the shared tag
    sheet = os.path.join(TMP, "contact", "selftest_run.jpg")
    p = run(os.path.join(HERE, "contact_sheet.py"), cap, "--out", sheet, "--metrics",
            os.path.join(out, "metrics.json"), "--cols", "3")
    check(p.returncode == 0 and os.path.isfile(sheet), "contact sheet written")
    if os.path.isfile(sheet):
        with Image.open(sheet) as im:
            check(im.size[0] == 3 * 480 + 4 * 6, "contact sheet width %d for 3 columns" % im.size[0])
    import contact_sheet as cs   # in-process for the matching logic (bytecode writing is off)
    col = cs.collect(cap, False)
    met = cs.load_metrics(os.path.join(out, "metrics.json"))
    ent = {e["key"]: e for e in col["entries"]}
    m1 = cs._met(met, ent["beat02_bridge_KS2a_good.png"])
    m2 = cs._met(met, ent["beat02_bridge_KS2a_good_1280x720.png"])
    check(len(col["entries"]) == len(shots) and m1.get("width") == 1920 and m2.get("width") == 1280,
          "contact sheet: one tile per file, metrics matched by file (%s / %s)" % (m1.get("width"), m2.get("width")))

    # compare: copy the run, modify the 1080p file of a tag that also exists at 720p, add a duplicate reference
    cap2 = os.path.join(TMP, "captures", "selftest_run2")
    if os.path.isdir(cap2):
        shutil.rmtree(cap2)
    shutil.copytree(cap, cap2)
    mod = good.copy()
    mod[100:300, 100:500] = 200
    save(mod, os.path.join(cap2, "beat02_bridge_KS2a_good.png"))
    dup = dict(shots[0])
    dup.update({"width": 3840, "height": 2160})
    qa.write_json(os.path.join(cap2, "report.json"), {"run": "selftest_run2", "shots": shots + [dup]})
    cout = os.path.join(TMP, "compare")
    p = run(os.path.join(HERE, "compare.py"), cap, cap2, "--out", cout)
    c = json.load(open(os.path.join(cout, "compare.json"), encoding="utf-8"))
    ct = {t["key"]: t for t in c["shots"]}
    k1, k7 = "beat02_bridge_KS2a_good.png", "beat02_bridge_KS2a_good_1280x720.png"
    check(c["matched"] == len(shots) and k1 in ct and k7 in ct, "compare matches every file (%d)" % c["matched"])
    check(ct[k1]["mean_abs_levels"] > 1.0 and ct[k1]["changed_pct"] > 3.0,
          "compare finds the modified file (mean %.2f levels)" % ct[k1]["mean_abs_levels"])
    check(all(ct[k]["mean_abs_levels"] == 0.0 for k in ct if k != k1),
          "unmodified files are identical, including the same tag at 720p")
    check(os.path.isfile(ct[k1]["heatmap"]) and ct[k1]["heatmap"] != ct[k7]["heatmap"], "heatmaps written per file")
    check(c["duplicates_a"]["files"] == [] and len(c["duplicates_b"]["files"]) == 1 and
          c["duplicates_b"]["files"][0]["file"] == k1, "duplicate file reference in run B reported")
    return cap


# ----------------------------------------------------------------------------------------------------------------------
def test_doorway() -> None:
    """14.5.1 doorway luma (calibrated band 0.40-0.60, D6; spec text 0.65-0.90) with under 0.3 % clipped: judged for KS1b
    only (KS1 cannot see the doorway)."""
    print("[image_metrics doorway (KS1b only)]")
    W, H = 1920, 1080
    cap = os.path.join(TMP, "captures", "selftest_doorway")
    if os.path.isdir(cap):
        shutil.rmtree(cap)
    aperture = qa.default_doorway_region(W, H)
    x0, y0, x1, y1 = qa.rect_to_px(aperture, W, H)

    def frame(door_level: float, hot: bool = False) -> np.ndarray:
        lv = np.full((H, W, 3), 38.0)                       # gallery around luma 0.15
        lv[y0:y1, x0:x1] = door_level
        if hot:                                             # a clipped core of about 1 % of the aperture
            cy, cx = (y0 + y1) // 2, (x0 + x1) // 2
            lv[cy - 13:cy + 12, cx - 13:cx + 12] = 252.0
        return quantise(lv, dither15(W, H))

    shots = []

    def add(name: str, a: np.ndarray, base: str, regions=None) -> None:
        fn = "beat01_gallery_%s.png" % name
        save(a, os.path.join(cap, fn))
        s = {"tag": base, "base_tag": base, "kind": "keyshot", "beat": 1, "file": fn, "width": W, "height": H}
        if regions is not None:
            s["regions"] = regions
        shots.append(s)

    door = {"doorway": aperture}
    add("KS1b_lit", frame(128.0), "KS1b_doorway", door)                 # luma 0.50
    add("KS1b_dark", frame(100.0), "KS1b_doorway", door)                # luma 0.39
    add("KS1b_bright", frame(205.0), "KS1b_doorway", door)              # luma 0.80: above the calibrated band
    add("KS1b_hot", frame(128.0, hot=True), "KS1b_doorway", door)       # luma ok, 1 % clipped in the aperture
    add("KS1b_default", frame(128.0), "KS1b_doorway")                   # no region: default KS1b aperture
    add("KS1_with_region", frame(100.0), "KS1_gallery_start", door)     # a doorway region on KS1 is never judged
    qa.write_json(os.path.join(cap, "report.json"), {"run": "selftest_doorway", "shots": shots})
    out = os.path.join(TMP, "metrics", "selftest_doorway")
    run(os.path.join(HERE, "image_metrics.py"), cap, "--out", out, "--quiet")
    m = json.load(open(os.path.join(out, "metrics.json"), encoding="utf-8"))
    by = {s["file"]: s for s in m["shots"]}

    def shot(name: str) -> dict:
        return by["beat01_gallery_%s.png" % name]

    lit = shot("KS1b_lit")
    check(lit["doorway"]["applies"] and lit["checks"]["doorway_luma"]["pass"] and
          lit["checks"]["doorway_clipped_pct"]["pass"] and abs(lit["doorway"]["mean_luma"] - 0.50) < 0.01,
          "KS1b doorway at luma %.3f passes" % lit["doorway"]["mean_luma"])
    check("doorway_luma" in shot("KS1b_dark")["failures"], "KS1b doorway at luma 0.39 fails")
    check("doorway_luma" in shot("KS1b_bright")["failures"], "KS1b doorway at luma 0.80 fails (calibrated band 0.40-0.60)")
    hot = shot("KS1b_hot")
    check(hot["checks"]["doorway_luma"]["pass"] and "doorway_clipped_pct" in hot["failures"] and
          hot["checks"]["clipped_pct"]["pass"], "1 %% clipped inside the aperture fails (%.2f %%), the frame-level "
          "0.3 %% still passes (%.3f %%)" % (hot["doorway"]["clipped_pct"], hot["clipped_pct"]))
    dflt = shot("KS1b_default")
    check(dflt["doorway"]["region_source"].startswith("default") and dflt["checks"]["doorway_luma"]["pass"] and
          "banding:doorway_glow" in dflt["checks"], "KS1b without a region: default aperture, doorway_glow band")
    ks1 = shot("KS1_with_region")
    check(ks1["doorway"]["applies"] is False and "doorway_luma" not in ks1["checks"] and
          "banding:gallery_centre" in ks1["checks"] and "banding:doorway_glow" not in ks1["checks"],
          "KS1: doorway region reported as not applicable, never judged; band named gallery_centre")


# ----------------------------------------------------------------------------------------------------------------------
def test_sequences() -> None:
    print("[sequence_metrics]")
    W, H, N = 640, 360, 90
    seq = os.path.join(TMP, "sequences", "pop")
    if os.path.isdir(seq):
        shutil.rmtree(seq)
    rng = np.random.default_rng(7)
    yy, xx = np.mgrid[0:H, 0:W]
    for i in range(N):
        base = 40 + 30 * np.sin((xx + 1.3 * i) / 37.0) * np.cos(yy / 53.0)   # slow pan -> non-zero diff baseline
        img = np.repeat(base[..., None], 3, axis=2)
        if i >= 45:
            img[100:200, 300:500] += 60                                       # an object pops in at frame 45
        img[300:340, 20:220] = 90                                             # static deck
        img[20:40, 20:620] = 100 + rng.normal(0, 12, (20, 600, 1))            # shimmering thin line
        save(np.clip(np.rint(img), 0, 255).astype(np.uint8), os.path.join(seq, "frame_%d.png" % i))
    out = os.path.join(TMP, "seqmetrics", "pop")
    p = run(os.path.join(HERE, "sequence_metrics.py"), seq, "--fps", "30", "--out", out, "--plot",
            "--std", "thin=px:20,20,620,40", "--static", "deck=px:20,300,220,340", "--diff-region", "0,0.2,1,0.8")
    r = json.load(open(os.path.join(out, "sequence.json"), encoding="utf-8"))
    frames = [s["frame"] for s in r["spikes"]]
    check(45 in frames, "pop at frame 45 flagged by the 4-sigma rule (spikes %s)" % frames)
    check(len(frames) == 1, "no other spikes")
    check(not r["checks"]["std"]["pass"] and r["std_regions"][0]["value"] > 6, "thin-line temporal std %.1f > 6"
          % r["std_regions"][0]["value"])
    check(r["checks"]["static"]["pass"], "static deck unchanged")
    check(p.returncode == 1, "sequence_metrics exits 1 on failure")
    check(os.path.isfile(os.path.join(out, "plot.png")) and os.path.isfile(os.path.join(out, "sequence.csv")),
          "plot and CSV written")

    seq2 = os.path.join(TMP, "sequences", "exposure_ramp")
    if os.path.isdir(seq2):
        shutil.rmtree(seq2)
    for i in range(40):
        v = 30 + 2.5 * i                                    # about 0.0098 luma per frame, then a jump
        if i >= 30:
            v += 20
        save(np.full((90, 160, 3), int(round(v)), np.uint8), os.path.join(seq2, "f%03d.png" % i))
    out2 = os.path.join(TMP, "seqmetrics", "exposure_ramp")
    run(os.path.join(HERE, "sequence_metrics.py"), seq2, "--fps", "30", "--out", out2, "--judge",
        "luma_step,ev_rate")
    r = json.load(open(os.path.join(out2, "sequence.json"), encoding="utf-8"))
    check(not r["checks"]["luma_step"]["pass"] and r["checks"]["luma_step"]["worst_frame"] == 30,
          "luma jump at frame 30 fails the 0.02 step (%.3f)" % r["checks"]["luma_step"]["value"])
    check(not r["checks"]["ev_rate"]["pass"], "fast exposure ramp fails 0.35 EV/s (%.2f)" %
          r["checks"]["ev_rate"]["value"])

    # --luma-region csv (R1-07 t12_ev): a bright "aperture" that ramps fast inside a constant frame; the per-frame region
    # from frames.csv sees the ramp, the whole frame much less
    seq3 = os.path.join(TMP, "sequences", "aperture")
    if os.path.isdir(seq3):
        shutil.rmtree(seq3)
    rows = ["frame,file,mean_luma,region_x0,region_y0,region_x1,region_y1"]
    for i in range(30):
        img = np.full((90, 160, 3), 30, np.uint8)
        img[30:60, 60:100] = int(round(60 + 6 * i))           # about 2 EV in 1 s inside the aperture
        fn = "frame_%05d.png" % i
        save(img, os.path.join(seq3, fn))
        rows.append("%d,%s,0,%.5f,%.5f,%.5f,%.5f" % (i, fn, 60 / 160, 30 / 90, 100 / 160, 60 / 90))
    with open(os.path.join(seq3, "frames.csv"), "w", encoding="utf-8") as f:
        f.write("\n".join(rows) + "\n")
    out3 = os.path.join(TMP, "seqmetrics", "aperture")
    run(os.path.join(HERE, "sequence_metrics.py"), seq3, "--fps", "30", "--out", out3, "--judge", "ev_rate",
        "--luma-region", "csv")
    ra = json.load(open(os.path.join(out3, "sequence.json"), encoding="utf-8"))
    run(os.path.join(HERE, "sequence_metrics.py"), seq3, "--fps", "30", "--out", out3 + "_full", "--judge", "ev_rate")
    rf = json.load(open(os.path.join(out3 + "_full", "sequence.json"), encoding="utf-8"))
    check(not ra["checks"]["ev_rate"]["pass"] and ra["checks"]["ev_rate"]["value"] > rf["checks"]["ev_rate"]["value"],
          "per-frame aperture region (frames.csv) sees the fast ramp (%.2f EV/s vs whole frame %.2f)" % (
              ra["checks"]["ev_rate"]["value"], rf["checks"]["ev_rate"]["value"]))


# ----------------------------------------------------------------------------------------------------------------------
def test_acceptance() -> None:
    """14.5 image acceptance checks (critique R1-29), the R1-38 luma rows and the recalibrated encode band rule."""
    print("[acceptance checks / encode band rule]")
    rng = np.random.default_rng(11)
    # near-black: a 1-px dark crevice is ignored, a 3x3 crushed blob is not
    img = np.full((120, 200, 3), 40, np.uint8)
    img[60, 10:190] = 2
    check(qa.near_black_check(img)["pct"] == 0.0, "1-px crevice at 2 levels is not a near-black area")
    img[20:23, 20:23] = 3
    nb = qa.near_black_check(img)
    check(not nb["pass"] and nb["pixels"] == 9, "3x3 blob at 3 levels fails the near-black floor (%d px)" % nb["pixels"])
    # KS2b side strips
    st = np.full((540, 960, 3), 9, np.uint8)
    check(qa.side_strips_check(st, {"left": [0, 0, 0.3, 1], "right": [0.7, 0, 1, 1]})["pass"], "dark side strips pass")
    st[300:304, 800:820] = 120                                   # a lit window 0.47 luma in the right strip
    r = qa.side_strips_check(st, {"left": [0, 0, 0.3, 1], "right": [0.7, 0, 1, 1]})
    check(not r["pass"] and not r["strips"]["right"]["pass"] and r["strips"]["left"]["pass"],
          "a lit window in a side strip fails (max %.2f, edge %.3f)" % (r["max_luma"], r["max_edge"]))
    # amber dots: 60 warm 2x2 dots on a cold haze
    hz = np.zeros((540, 960, 3), np.uint8)
    hz[:, :] = (120, 135, 150)
    pts = set()
    while len(pts) < 60:
        pts.add((int(rng.integers(200, 520)), int(rng.integers(10, 950))))
    pts = [(y - y % 8, x - x % 8) for y, x in pts]
    for y, x in set(pts):
        hz[y:y + 2, x:x + 2] = (230, 170, 90)
    ad = qa.amber_dots_check(hz, [0, 0.34, 1, 1])
    check(ad["dots"] == len(set(pts)) and ad["pass"], "amber dots counted (%d of %d)" % (ad["dots"], len(set(pts))))
    # readable cells: synthetic cells with a dark interior point set and a lighter web set
    cells = []
    for k in range(160):
        u, v = 0.05 + (k % 16) * 0.055, 0.40 + (k // 16) * 0.055
        c = [u, v, 0.01] + [u, v] * 5 + [u + 0.02, v] * 6
        cells.append(c)
    cimg = np.full((540, 960, 3), 100, np.uint8)
    for c in cells:
        x, y = int(c[0] * 960), int(c[1] * 540)
        cimg[y - 2:y + 3, x - 2:x + 3] = 60
    rc = qa.readable_cells_check(cimg, cells)
    check(rc["readable"] == 160 and rc["pass"], "160 readable synthetic cells (%d)" % rc["readable"])
    cimg[:, :] = 100
    check(qa.readable_cells_check(cimg, cells)["readable"] == 0, "uniform cells are not readable")
    # luma rows by the content in view (R1-38)
    rows = qa.luma_rows_for({"e": 62.0}, (2, None))
    check((1, None) in rows and (2, None) in rows, "E 62 (1->2 blend) judges against beats 1 and 2 (%s)" % rows)
    check(qa.luma_rows_for({"e": 120.0}, (2, None)) == [(2, None)], "E 120 judges against beat 2 only")
    # encode band rule: a gentle dithered gradient whose decode is a flat plateau within 1 level of the truth is no band;
    # the same gradient flattened into 3-level steps is
    h, w = 400, 64
    grad = 40.0 + 0.012 * np.arange(h)[:, None] + np.zeros((h, w))      # 4.8 levels over 400 px
    grad[200:, :] += 30.0                                                # a qualifying change (> 24 levels) on every line
    src = np.repeat(np.clip(np.floor(grad + 0.5 + 2.0 * (rng.random((h, w)) - 0.5)), 0, 255)[:, :, None], 3, 2).astype(np.uint8)
    flat = np.repeat(np.rint(grad)[:, :, None], 3, 2).astype(np.uint8)
    r1 = qa.encode_band_runs(flat, src, (0, 0, w, h), None, 24.0)
    check(r1["band_runs"] == 0 and r1["raw_max_run"] > 24, "faithful decode of a gentle gradient: raw run %d px, no band"
          % r1["raw_max_run"])
    steep = 40.0 + 0.1 * np.arange(h)[:, None] + np.zeros((h, w))      # 0.1 level / px
    src2 = np.repeat(np.clip(np.floor(steep + 0.5 + 2.0 * (rng.random((h, w)) - 0.5)), 0, 255)[:, :, None], 3, 2).astype(np.uint8)
    post = np.repeat((np.floor(steep / 4.0) * 4.0)[:, :, None], 3, 2).astype(np.uint8)   # 40 px plateaus, 4-level steps
    r2 = qa.encode_band_runs(post, src2, (0, 0, w, h), None, 24.0)
    check(r2["band_runs"] > 0 and r2["band_max_run"] >= 30, "4-level steps of a smooth 0.1 level/px gradient are bands "
          "(%d runs, longest %d px)" % (r2["band_runs"], r2["band_max_run"]))


# ----------------------------------------------------------------------------------------------------------------------
def test_encode() -> None:
    print("[encode_test]")
    W, H, N = 1280, 720, 30
    fog = fog_gradient(W, H)
    for name, dith in (("enc_banded", False), ("enc_dithered", True)):
        seq = os.path.join(TMP, "sequences", name)
        if os.path.isdir(seq):
            shutil.rmtree(seq)
        for i in range(N):
            a = quantise(fog, dither15(W, H, i)) if dith else quantise(np.floor(fog / 4.0) * 4.0)
            save(a, os.path.join(seq, "frame_%04d.png" % i))
        out = os.path.join(TMP, "encode", name)
        p = run(os.path.join(HERE, "encode_test.py"), seq, "--fps", "60", "--beat", "2", "--out", out,
                "--png-every", "10")
        if p.returncode == 3:
            check(False, "ffmpeg / libx264 available for the encode test")
            return
        r = json.load(open(os.path.join(out, "encode.json"), encoding="utf-8"))
        labels = [e["encode"] for e in r["encodes"]]
        check("x264_crf20" in labels and "x264_crf23" in labels and "x264_crf30" in labels,
              "%s: x264 CRF 20/23/30 encoded (%s)" % (name, labels))
        check(all(e.get("decoded_frames") == N for e in r["encodes"]), "%s: every encode decoded to %d frames" %
              (name, N))
        check(os.path.isfile(os.path.join(out, "crops_worst.png")), "%s: crops written" % name)
        if name == "enc_banded":
            check(p.returncode == 1 and not r["pass"], "banded sequence fails the gated x264 CRF 20 (run %s px)" %
                  [e.get("max_run") for e in r["encodes"]])
        else:
            check(r["source_banding"]["pass_still_limit"], "dithered source passes the still limit (%d px)" %
                  r["source_banding"]["max_run"])
            print("       dithered: " + ", ".join("%s run %s px %s" % (e["encode"], e.get("max_run"),
                                                                      "PASS" if e["pass"] else "FAIL")
                                                 for e in r["encodes"]))


# ----------------------------------------------------------------------------------------------------------------------
def test_metrics() -> None:
    print("[metrics.py]")
    base = os.path.join(TMP, "transcripts")
    if os.path.isdir(base):
        shutil.rmtree(base)
    root = os.path.join(TMP, "fake_project")
    qa.ensure_dir(root)
    proj = qa.ensure_dir(os.path.join(base, "W--fake-1st-Megastructures-1st-Megastructures"))
    sib = qa.ensure_dir(os.path.join(base, "W--fake-2nd-Megastructures-2nd-Megastructures"))
    parent = qa.ensure_dir(os.path.join(base, "W--YouTube-Megastructures"))

    def asst(mid, ts, inp, cr, c5, c1, out, cwd=root, model="claude-opus-5-5", speed="standard"):
        return {"type": "assistant", "cwd": cwd, "timestamp": ts, "requestId": "req_" + mid, "uuid": "u" + mid + ts,
                "message": {"id": mid, "model": model, "usage": {
                    "input_tokens": inp, "cache_read_input_tokens": cr, "cache_creation_input_tokens": c5 + c1,
                    "cache_creation": {"ephemeral_5m_input_tokens": c5, "ephemeral_1h_input_tokens": c1},
                    "output_tokens": out, "output_tokens_details": {"thinking_tokens": out // 2},
                    "server_tool_use": {"web_search_requests": 0, "web_fetch_requests": 0},
                    "inference_geo": "global", "speed": speed}}}

    def wr(path, recs):
        qa.ensure_dir(os.path.dirname(path))
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            for r in recs:
                f.write(json.dumps(r) + "\n")

    main = [{"type": "user", "cwd": root, "timestamp": "2026-01-01T10:00:00.000Z", "message": {"content": "go"}},
            asst("m1", "2026-01-01T10:00:05.000Z", 10, 1000, 500, 0, 100),
            asst("m1", "2026-01-01T10:00:06.000Z", 10, 1000, 500, 0, 100),         # duplicate content block
            asst("m2", "2026-01-01T10:05:00.000Z", 20, 2000, 0, 300, 200),
            {"type": "user", "cwd": root, "timestamp": "2026-01-01T10:30:00.000Z", "message": {"content": "x"}},
            asst("m3", "2026-01-01T10:31:00.000Z", 5, 500, 0, 0, 50),
            {"type": "assistant", "cwd": root, "timestamp": "2026-01-01T10:31:01.000Z",
             "message": {"id": "syn", "model": "<synthetic>", "usage": {"input_tokens": 999, "output_tokens": 999}}}]
    wr(os.path.join(proj, "sess1.jsonl"), main)
    wr(os.path.join(proj, "sess1", "subagents", "workflows", "wf_abc", "agent-a1.jsonl"),
       [asst("s1", "2026-01-01T10:06:00.000Z", 1, 100, 1000, 2000, 400)])
    wr(os.path.join(proj, "sess1", "subagents", "workflows", "wf_abc", "journal.jsonl"),
       [{"type": "launched"}, {"type": "started", "agentId": "a1"}])
    wr(os.path.join(proj, "foreign.jsonl"),
       [asst("f1", "2026-01-01T12:00:00.000Z", 10 ** 6, 0, 0, 0, 10 ** 6, cwd=r"C:\elsewhere")])
    wr(os.path.join(sib, "s.jsonl"), [asst("x1", "2026-01-01T09:00:00.000Z", 10 ** 6, 0, 0, 0, 0)])
    wr(os.path.join(parent, "s.jsonl"), [asst("x2", "2026-01-01T09:00:00.000Z", 10 ** 6, 0, 0, 0, 0)])
    state = os.path.join(root, "STATE.md")
    qa.write_text(state, "# STATE\n\nRun started at 2026-01-01 11:02:00 +01:00 (anchor).\n")
    report = os.path.join(root, "REPORT.md")
    qa.write_text(report, "# Report\n\n## Project metrics\n\nold numbers\n\n### old sub\n\nx\n\n## Next section\n\nkeep\n")
    js = os.path.join(TMP, "metrics_selftest.json")
    p = run(os.path.join(os.path.dirname(HERE), "metrics.py"), "--projects-dir", base, "--project-root", root,
            "--state", state, "--json", js, "--update-report", report)
    check(p.returncode == 0, "metrics.py runs (rc %d)" % p.returncode)
    d = json.load(open(js, encoding="utf-8"))
    t = d["tokens"]
    check(d["messages"] == 4 and d["assistant_records"] == 5, "dedup: 4 messages from 5 records (%d / %d)" % (
        d["messages"], d["assistant_records"]))
    check(t == {"input": 36, "cache_read": 3600, "cache_write_5m": 1500, "cache_write_1h": 2300, "output": 750},
          "token categories exact (%s)" % t)
    exp_cost = (36 * 4 + 3600 * 0.2 + 1500 * 5 + 2300 * 8 + 750 * 20) / 1e6
    check(abs(d["cost_usd"]["total"] - exp_cost) < 1e-12, "cost %.6f == %.6f" % (d["cost_usd"]["total"], exp_cost))
    # every record's timestamp counts (the synthetic one at 10:31:01 too): wall 10:00:00 -> 10:31:01,
    # one gap 10:06:00 -> 10:30:00 (24 min), active 31:01 - 24:00 = 7:01
    check(d["time"]["wall"] == "0:31:01" and len(d["time"]["gaps"]) == 1 and d["time"]["active"] == "0:07:01",
          "wall 0:31:01, one gap, active 0:07:01 (%s, %d, %s)" % (d["time"]["wall"], len(d["time"]["gaps"]),
                                                                  d["time"]["active"]))
    check(d["time"]["anchor_wall"] == "0:29:01", "anchor 10:02Z to 10:31:01Z = 0:29:01 (%s)" %
          d["time"]["anchor_wall"])
    check(any("foreign.jsonl" in f[0] for f in d["files_excluded"]), "foreign-cwd file excluded")
    ex = [f[0] for f in d["folders_excluded"]]
    check("W--fake-2nd-Megastructures-2nd-Megastructures" in ex and "W--YouTube-Megastructures" in ex,
          "sibling and parent folders excluded")
    kinds = sorted(f["kind"] for f in d["files_included"])
    check(kinds == ["main_session", "workflow_journal", "workflow_subagent"], "file kinds %s" % kinds)
    rep = open(report, encoding="utf-8").read()
    check("old numbers" not in rep and "old sub" not in rep and "## Next section\n\nkeep" in rep and
          "| Metric | Value | How obtained |" in rep, "report section replaced, following section kept")


def test_r2_checks() -> None:
    """Fix round R2 image checks on synthetic frames: 1-px pure-black runs (R2-16), amber dot-core saturation (R2-21),
    amber coverage (R2-24), pour lifts on projected curves (R2-02), wall board steps and wall macro residual (R2-02),
    bilinear readable-cell sampling (R2-04) and the B5_end band row (R2-36 f)."""
    print("[R2 image checks]")
    W, H = 640, 360
    rng = np.random.default_rng(3)
    base = np.full((H, W, 3), 40, dtype=np.uint8)
    # R2-16: a 1-px black outline (14 px) fails; isolated black pixels and a 5-px run pass; near_black misses the outline
    a = base.copy()
    a[100, 200:214] = 0
    a[50, 50] = 0
    a[60, 60:65] = 1
    br = qa.black_run_check(a)
    check(not br["pass"] and br["runs"] == 1 and br["largest_run_px"] == 14, "a 14 px 1-px black line fails (%s)" % br["runs"])
    check(qa.near_black_check(a)["pass"], "near_black (2x2 opening) alone misses it, as critique R2-16 found")
    check(qa.black_run_check(base)["pass"], "no black pixels: pass")
    # R2-21: cream-white dot cores fail, saturated amber cores pass
    def dots(core: tuple) -> np.ndarray:
        f = np.full((H, W, 3), 0.0)
        f[:] = (110, 118, 128)
        for k in range(60):
            y, x = 140 + (k // 12) * 40, 20 + (k % 12) * 50
            f[y - 1:y + 2, x - 1:x + 2] = (200, 120, 60)    # amber limb
            f[y, x] = core
        return f.astype(np.uint8)
    cream = qa.amber_saturation_check(dots((222, 198, 171)), [0, 0.34, 1, 1])
    amber = qa.amber_saturation_check(dots((255, 182, 95)), [0, 0.34, 1, 1])
    check(not cream["pass"] and cream["core_saturation"] < 0.3, "cream cores S %.2f fail" % cream["core_saturation"])
    check(amber["pass"] and 28 <= amber["core_hue"] <= 38, "amber cores S %.2f hue %.1f pass" % (amber["core_saturation"],
                                                                                              amber["core_hue"]))
    cov = qa.amber_coverage_check(dots((255, 182, 95)))
    check(cov["pixels"] >= 60 and cov["pct"] > 0.0, "amber coverage counts the emitters (%.3f %%)" % cov["pct"])
    # R2-02: lift joints along curves: 4 curves with a 6-level dark line read, 1 curve with a 2-level line does not
    Y = np.full((H, W), 90.0) + rng.normal(0, 0.6, (H, W))
    curves = []
    for j, (yy, depth) in enumerate([(60, 6.0), (110, 6.0), (160, 6.0), (210, 2.0), (260, 6.0)]):
        xs = np.arange(40, 600, 8)
        ys = (yy + 10 * np.sin(xs / 90.0)).astype(int)        # a curved joint
        for x, y in zip(xs, ys):
            Y[y, x - 4:x + 4] -= depth
        pts = []
        for x, y in zip(xs, ys):
            pts += [float(x + 0.5) / W, float(y + 0.5) / H]
        curves.append([3.0 * (j + 1)] + pts)
    img = np.repeat(np.clip(Y, 0, 255).astype(np.uint8)[:, :, None], 3, axis=2)
    lc = qa.lift_curves_check(img, curves)
    check(lc["pass"] and lc["lifts"] == 4, "pour lifts on curves: %s of 5 read (one at 2 levels)" % lc["lifts"])
    check(not qa.lift_curves_check(img, curves[3:4])["pass"], "a single faint lift fails")
    # R2-02: wall board steps (rows of +/-4 levels pass, a smooth ramp fails) and wall macro residual
    yy, xx = np.mgrid[0:H, 0:W]
    ramp = 50.0 + 30.0 * yy / H
    boards = ramp + np.where(((yy // 24) % 2) == 0, 4.0, -4.0)
    def grey(v: np.ndarray) -> np.ndarray:
        return np.repeat(np.clip(v + rng.normal(0, 0.6, v.shape), 0, 255).astype(np.uint8)[:, :, None], 3, axis=2)
    check(qa.wall_board_check(grey(boards), [0.05, 0.05, 0.3, 0.95])["pass"], "board rows of +/-4 levels pass")
    check(not qa.wall_board_check(grey(ramp), [0.05, 0.05, 0.3, 0.95])["pass"], "a smooth wall ramp fails")
    macro = ramp + 7.0 * np.sin(xx / 23.0) * np.cos(yy / 17.0)     # std about 3.5 levels
    rects = {"rects": [[0.6, 0.1, 0.95, 0.3], [0.6, 0.5, 0.95, 0.7]]}
    check(qa.wall_macro_check(grey(macro), rects)["pass"], "macro noise of +/-7 levels (std about 3.5) passes")
    check(not qa.wall_macro_check(grey(ramp), rects)["pass"], "a plain gradient wall fails the macro residual")
    # R2-04: bilinear cell sampling at sub-pixel positions (a web on a pixel boundary is sampled between its pixels)
    g = np.full((H, W, 3), 30, dtype=np.uint8)
    g[:, 300:302] = 130
    cell = [0.4, 0.5, 0.05] + [0.4, 0.5] * 5 + [301.0 / W, 0.5] * 6
    rc = qa.readable_cells_check(g, [cell])
    check(rc["readable"] == 1 and rc["readable_nearest"] == 1 and rc["sampling"].startswith("bilinear"),
          "readable cell, bilinear and nearest both reported")
    # R2-36 (f): B5_end (E 588.54, the bottom dock) is judged on the union of the beat-5 and beat-6 bands
    rows = qa.luma_rows_for({"e": 588.54}, (5, None))
    check((6, None) in rows, "B5_end at E 588.54 uses the beat-5 and beat-6 rows (%s)" % rows)
    # lead decision D8: KS5b (all sizes) is judged on the beat-6 row alone; KS5c and KS5a keep beat 5
    for t in ("KS5b_lift_fan", "KS5b_lift_fan_4k", "KS5b_lift_fan_21x9"):
        check(qa.luma_rows_for({"tag": t, "e": 540.0}, (5, None)) == [(6, None)], "%s judged on the beat-6 row (D8)" % t)
    check(qa.luma_rows_for({"tag": "KS5c_lift_down"}, (5, None)) == [(5, None)] and
          qa.content_luma_row({"tag": "KS5a_lift_city"}) is None, "KS5c / KS5a keep the beat-5 row")


def test_no_bytecode(since: float) -> None:
    """No tool may leave __pycache__ / .pyc files in Tools/ (every entry script sets sys.dont_write_bytecode)."""
    print("[bytecode]")
    tools = os.path.dirname(HERE)
    new, old = [], []
    for base, dirs, files in os.walk(tools):
        dirs[:] = [d for d in dirs if d != "fastcompile"]     # C# build output, no Python
        for f in files:
            if f.endswith((".pyc", ".pyo")):
                p = os.path.join(base, f)
                (new if os.path.getmtime(p) >= since - 2.0 else old).append(os.path.relpath(p, tools))
    check(not new, "no .pyc written into Tools/ during the self-test (%s)" % (new or "none"))
    if old:
        print("       note: older bytecode left by someone else: %s" % old)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--keep", action="store_true", help="keep QA/tmp/selftest afterwards")
    ap.add_argument("--skip-encode", action="store_true")
    args = ap.parse_args()
    started = time.time()
    qa.ensure_dir(TMP)
    test_regions()
    test_images()
    test_doorway()
    test_sequences()
    test_acceptance()
    test_r2_checks()
    if not args.skip_encode:
        test_encode()
    test_metrics()
    test_no_bytecode(started)
    print("")
    if FAILS:
        print("SELFTEST FAILED: %d assertion(s)" % len(FAILS))
        for f in FAILS:
            print("  - " + f)
        return 1
    print("SELFTEST PASSED (outputs in %s)" % TMP)
    if not args.keep:
        shutil.rmtree(TMP, ignore_errors=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
