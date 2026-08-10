"""Trim a .wav to a given length, with a short fade so the cut is inaudible.

Written for the button click, which shipped at 0.93s: its attack peaks around
150ms and everything past ~300ms is a decay tail under 12%. Clicked through a
menu at a normal pace that tail is still sounding two or three clicks later,
which reads as mush rather than as a click.

Cutting a waveform at an arbitrary sample leaves a discontinuity, and a
discontinuity is a click in the literal DSP sense -- an audible tick at the
splice. The fade-out is not politeness, it is what stops the trim adding a
defect of its own.

    py tools/trim_wav.py "path/to/sound.wav" 250          # keep first 250ms
    py tools/trim_wav.py "path/to/sound.wav" 250 --fade 20
    py tools/trim_wav.py "path/to/sound.wav" 250 --dry-run

Reversibility is git's job: the original is in history, so this writes in
place rather than leaving a .bak beside it for someone to commit by accident.
"""

import argparse
import io
import os
import struct
import sys


def parse_wav(raw):
    """-> (fmt_chunk, data_bytes, channels, rate, bits). Raises on anything odd."""
    if raw[:4] != b"RIFF" or raw[8:12] != b"WAVE":
        raise ValueError("not a RIFF/WAVE file")

    pos, fmt, data = 12, None, None
    while pos + 8 <= len(raw):
        chunk_id = raw[pos:pos + 4]
        size = struct.unpack("<I", raw[pos + 4:pos + 8])[0]
        body = raw[pos + 8:pos + 8 + size]
        if chunk_id == b"fmt ":
            fmt = body
        elif chunk_id == b"data":
            data = body
        pos += 8 + size + (size & 1)

    if fmt is None or data is None:
        raise ValueError("missing fmt or data chunk")

    audio_format = struct.unpack("<H", fmt[0:2])[0]
    channels, rate = struct.unpack("<HI", fmt[2:8])
    bits = struct.unpack("<H", fmt[14:16])[0]

    # 1 == PCM. Anything else (float, ADPCM, compressed) would need decoding
    # before the samples could be faded, and silently writing a wrong-format
    # file is worse than refusing.
    if audio_format != 1:
        raise ValueError(f"only uncompressed PCM is supported, got format {audio_format}")
    if bits not in (8, 16, 24, 32):
        raise ValueError(f"unsupported bit depth {bits}")

    return fmt, data, channels, rate, bits


def sample_range(bits):
    return -(1 << (bits - 1)), (1 << (bits - 1)) - 1


def trim(path, keep_ms, fade_ms, dry_run):
    raw = io.open(path, "rb").read()
    fmt, data, channels, rate, bits = parse_wav(raw)

    width = bits // 8
    frame = width * channels
    total_frames = len(data) // frame
    keep_frames = min(total_frames, int(rate * keep_ms / 1000))
    fade_frames = min(keep_frames, int(rate * fade_ms / 1000))

    print(f"{os.path.basename(path)}: {total_frames / rate:.3f}s "
          f"({channels}ch {rate}Hz {bits}bit)")

    if keep_frames >= total_frames:
        print(f"  already {total_frames / rate * 1000:.0f}ms or shorter -- nothing to do")
        return False

    print(f"  -> {keep_frames / rate:.3f}s, with a {fade_frames / rate * 1000:.0f}ms fade-out")
    if dry_run:
        print("  (dry run, nothing written)")
        return False

    kept = bytearray(data[:keep_frames * frame])

    # Linear fade across the last fade_frames. Linear rather than exponential
    # because the fade is short enough that the curve shape is inaudible, and
    # linear cannot overshoot the sample range.
    lo, hi = sample_range(bits)
    fade_start = keep_frames - fade_frames
    for f in range(fade_start, keep_frames):
        gain = (keep_frames - f) / float(fade_frames)
        for c in range(channels):
            off = f * frame + c * width
            chunk = kept[off:off + width]
            value = int.from_bytes(chunk, "little", signed=True)
            faded = max(lo, min(hi, int(value * gain)))
            kept[off:off + width] = faded.to_bytes(width, "little", signed=True)

    # Rebuilt from scratch rather than patched in place, so any other chunk
    # the original carried (LIST, cue, junk) is dropped rather than left
    # describing a length that no longer exists.
    body = b"fmt " + struct.pack("<I", len(fmt)) + fmt
    body += b"data" + struct.pack("<I", len(kept)) + bytes(kept)
    out = b"RIFF" + struct.pack("<I", 4 + len(body)) + b"WAVE" + body

    io.open(path, "wb").write(out)
    print(f"  written ({len(out)} bytes, was {len(raw)})")
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("path")
    parser.add_argument("keep_ms", type=float, help="how much of the head to keep, in milliseconds")
    parser.add_argument("--fade", type=float, default=15.0, help="fade-out length in ms (default 15)")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if not os.path.isfile(args.path):
        sys.exit(f"No file at {args.path}")

    try:
        trim(args.path, args.keep_ms, args.fade, args.dry_run)
    except ValueError as e:
        sys.exit(f"{args.path}: {e}")


if __name__ == "__main__":
    main()
