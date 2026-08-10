#!/usr/bin/env python3
"""
Cut recorded dice rolls into individual impact samples.

WHY THIS EXISTS
---------------
Every dice recording you can download is a *roll* - one die tumbling for a second or
several, containing anything from four to fifty bounces. The game needs the opposite: one
short sample per collision, triggered when the physics actually reports a contact, with
volume and pitch driven by impact force.

Playing a whole roll as a one-shot drifts within half a second, because the recording's
bounces land at fixed times and the physics dice land whenever they land. What you hear
stops matching what you see, and it reads as cheap without being obviously broken.

So: find the transients, cut them out, and hand Godot a bag of impacts for
AudioStreamRandomizer to shuffle.

WHAT IT PRODUCES
----------------
Mono, 48 kHz, 16-bit WAV, one per impact, named <freesound-id>_NN.wav so provenance
survives in the filename. THIRD_PARTY.md maps the id back to its source and licence.

Mono is not a preference. AudioStreamPlayer3D cannot spatialise a stereo sample - it needs
mono to pan and attenuate with distance. Ship stereo and positional audio silently does
nothing, with no error to tell you why.

USAGE
-----
    python tools/slice_impacts.py                      # every wav in assets/
    python tools/slice_impacts.py assets/foo.wav       # just one
    python tools/slice_impacts.py --out game/audio/dice_stone

No dependencies beyond numpy - the RIFF parsing is done here on purpose, because Python's
wave module refuses 32-bit float files and half the good recordings are exactly that.
"""

import argparse
import glob
import os
import struct
import sys

import numpy as np

TARGET_RATE = 48_000
PEAK_DBFS = -3.0


# ---------------------------------------------------------------- reading

def read_wav(path):
    """Any RIFF/WAVE: PCM 8/16/24/32-bit, IEEE float 32/64, mono or stereo, EXTENSIBLE."""
    raw = open(path, "rb").read()
    if raw[:4] != b"RIFF" or raw[8:12] != b"WAVE":
        raise ValueError("not a RIFF/WAVE file")

    fmt = None
    data = None
    i = 12
    while i < len(raw) - 8:
        cid = raw[i:i + 4]
        size = struct.unpack("<I", raw[i + 4:i + 8])[0]
        body = raw[i + 8:i + 8 + size]

        if cid == b"fmt ":
            tag, ch, rate, _, _, bits = struct.unpack("<HHIIHH", body[:16])
            if tag == 0xFFFE and size >= 40:            # EXTENSIBLE: real format is in the GUID
                tag = struct.unpack("<H", body[24:26])[0]
            fmt = (tag, ch, rate, bits)
        elif cid == b"data":
            data = body

        i += 8 + size + (size & 1)                      # chunks are word-aligned

    if fmt is None or data is None:
        raise ValueError("missing fmt or data chunk")

    tag, ch, rate, bits = fmt

    if tag == 3:                                        # IEEE float
        a = np.frombuffer(data, dtype={32: np.float32, 64: np.float64}[bits]).astype(np.float64)
    elif tag == 1 and bits == 24:                       # numpy has no 24-bit type
        b = np.frombuffer(data[:len(data) // 3 * 3], dtype=np.uint8).reshape(-1, 3)
        v = b[:, 0].astype(np.int32) | b[:, 1].astype(np.int32) << 8 | b[:, 2].astype(np.int32) << 16
        a = np.where(v & 0x800000, v - 0x1000000, v).astype(np.float64) / 8388608.0
    elif tag == 1:
        dt = {8: np.uint8, 16: np.int16, 32: np.int32}[bits]
        a = np.frombuffer(data, dtype=dt).astype(np.float64)
        a = (a - 128) / 128.0 if bits == 8 else a / float(2 ** (bits - 1))
    else:
        raise ValueError(f"unsupported format tag {tag} at {bits} bits")

    if ch > 1:
        a = a[:len(a) // ch * ch].reshape(-1, ch).mean(axis=1)
    return a, rate


def write_wav(path, samples, rate):
    pcm = np.clip(samples, -1.0, 1.0)
    pcm = (pcm * 32767).astype("<i2").tobytes()
    hdr = b"RIFF" + struct.pack("<I", 36 + len(pcm)) + b"WAVEfmt " \
        + struct.pack("<IHHIIHH", 16, 1, 1, rate, rate * 2, 2, 16) \
        + b"data" + struct.pack("<I", len(pcm))
    open(path, "wb").write(hdr + pcm)


def resample_to(a, rate, target):
    """Linear resample. Fine for short percussive samples; nobody can hear the difference."""
    if rate == target:
        return a
    n = int(round(len(a) * target / rate))
    return np.interp(np.linspace(0, len(a) - 1, n), np.arange(len(a)), a)


# ---------------------------------------------------------------- slicing

def find_impacts(a, rate, threshold, min_gap_ms, quiet_before_ms):
    """
    Onsets that are actually usable: loud enough to matter, and preceded by enough quiet
    that the sample starts on the attack rather than halfway through the previous bounce.
    """
    win = max(1, int(rate * 0.004))
    env = np.abs(a[:len(a) // win * win].reshape(-1, win)).max(axis=1)
    if env.max() <= 0:
        return []

    thr = threshold * env.max()
    gap = max(1, int(min_gap_ms / 1000 * rate / win))
    quiet = max(1, int(quiet_before_ms / 1000 * rate / win))

    hits, last = [], -10 ** 9
    for i in range(1, len(env)):
        if env[i] <= thr or env[i - 1] > thr:
            continue
        if i - last < gap:
            continue
        # the run-up has to be genuinely quieter, or we're mid-bounce
        if env[max(0, i - quiet):i].max() > thr * 0.5:
            continue
        hits.append(i * win)
        last = i
    return hits


def slice_file(path, outdir, args):
    a, rate = read_wav(path)
    hits = find_impacts(a, rate, args.threshold, args.min_gap, args.quiet_before)

    pre = int(rate * 0.003)
    tail = int(rate * args.max_len)
    fade = int(rate * 0.010)
    gain = 10 ** (PEAK_DBFS / 20)

    # id from a freesound-style filename, so the sample keeps its provenance
    stem = os.path.basename(path).split("__")[0] or os.path.splitext(os.path.basename(path))[0]
    stem = "".join(c for c in stem if c.isalnum())[:12] or "clip"

    scored = []
    for n, h in enumerate(hits):
        start = max(0, h - pre)
        end = min(len(a), hits[n + 1] - pre if n + 1 < len(hits) else start + tail, start + tail)
        seg = a[start:end].copy()
        if len(seg) < int(rate * 0.03):
            continue
        scored.append((np.abs(seg).max(), seg))

    scored.sort(key=lambda s: -s[0])
    kept = scored[:args.max_per_file]

    written = []
    for n, (_, seg) in enumerate(kept, 1):
        f = min(fade, len(seg))
        seg[-f:] *= np.linspace(1, 0, f)

        # A cut that lands mid-waveform starts at a non-zero value, and the speaker jumping
        # from silence to that value is an audible click. 1 ms is 48 samples - long enough to
        # remove it, far too short to soften a percussive attack.
        lead = min(int(TARGET_RATE * 0.001 * rate / TARGET_RATE), len(seg))
        if lead > 1:
            seg[:lead] *= np.linspace(0, 1, lead)

        seg /= max(1e-9, np.abs(seg).max())
        seg *= gain
        seg = resample_to(seg, rate, TARGET_RATE)
        out = os.path.join(outdir, f"{stem}_{n:02d}.wav")
        write_wav(out, seg, TARGET_RATE)
        written.append((out, len(seg) / TARGET_RATE * 1000))

    return len(a) / rate, len(hits), written


def main():
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("files", nargs="*", default=None)
    p.add_argument("--out", default="game/audio/dice_wood")
    p.add_argument("--threshold", type=float, default=0.14,
                   help="onset level, as a fraction of the file's peak")
    p.add_argument("--min-gap", type=float, default=45,
                   help="ms between accepted impacts")
    p.add_argument("--quiet-before", type=float, default=25,
                   help="ms of relative quiet required before an onset")
    p.add_argument("--max-len", type=float, default=0.30, help="seconds per sample")
    p.add_argument("--max-per-file", type=int, default=25)
    a = p.parse_args()

    files = a.files or sorted(glob.glob("assets/*.wav"))
    if not files:
        print("no input files"); return 1

    os.makedirs(a.out, exist_ok=True)
    total = 0
    for f in files:
        try:
            dur, found, written = slice_file(f, a.out, a)
        except Exception as e:
            print(f"  SKIP {os.path.basename(f)[:56]:<58} [{type(e).__name__}] {e}")
            continue
        total += len(written)
        print(f"  {os.path.basename(f)[:56]:<58}{dur:>7.2f}s  {found:>3} onsets  -> {len(written)} kept")
        for path, ms in written[:3]:
            print(f"       {os.path.basename(path):<22}{ms:>7.0f} ms")
        if len(written) > 3:
            print(f"       ... and {len(written) - 3} more")

    print(f"\n{total} impact samples in {a.out}  (mono, {TARGET_RATE // 1000} kHz, 16-bit, {PEAK_DBFS:+g} dBFS)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
