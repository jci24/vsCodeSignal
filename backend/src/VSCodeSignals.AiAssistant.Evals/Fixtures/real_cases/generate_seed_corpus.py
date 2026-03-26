#!/usr/bin/env python3
from __future__ import annotations

import math
import random
import wave
from array import array
from pathlib import Path


ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / "assets"
SAMPLE_RATE = 22_050


def sine_mix(duration: float, components: list[tuple[float, float]], gain: float = 1.0) -> list[float]:
    frame_count = int(duration * SAMPLE_RATE)
    samples: list[float] = []
    for index in range(frame_count):
        t = index / SAMPLE_RATE
        envelope = 1.0
        if t < 0.02:
            envelope = t / 0.02
        elif duration - t < 0.04:
            envelope = max(0.0, (duration - t) / 0.04)

        value = 0.0
        for freq, amplitude in components:
            value += amplitude * math.sin(2.0 * math.pi * freq * t)
        samples.append(value * gain * envelope)
    return samples


def add_white_noise(samples: list[float], amount: float, seed: int) -> list[float]:
    rng = random.Random(seed)
    return [sample + rng.uniform(-amount, amount) for sample in samples]


def clip(samples: list[float], ceiling: float = 1.0) -> list[float]:
    return [max(-ceiling, min(ceiling, sample)) for sample in samples]


def with_leading_silence(samples: list[float], seconds: float) -> list[float]:
    silence = [0.0] * int(seconds * SAMPLE_RATE)
    return silence + samples


def with_bursts(base_freq: float, burst_freq: float, duration: float) -> list[float]:
    frame_count = int(duration * SAMPLE_RATE)
    samples: list[float] = []
    for index in range(frame_count):
        t = index / SAMPLE_RATE
        base = 0.16 * math.sin(2.0 * math.pi * base_freq * t)
        burst_gate = 1.0 if (0.35 <= t <= 0.55) or (1.15 <= t <= 1.35) or (1.95 <= t <= 2.20) else 0.0
        burst = burst_gate * 0.14 * math.sin(2.0 * math.pi * burst_freq * t)
        air = burst_gate * 0.04 * math.sin(2.0 * math.pi * (burst_freq * 1.8) * t)
        samples.append(base + burst + air)
    return clip(samples, 0.98)


def write_wav(path: Path, samples: list[float]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = array("h", (int(max(-1.0, min(1.0, sample)) * 32767.0) for sample in samples))
    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(SAMPLE_RATE)
        handle.writeframes(pcm.tobytes())


def write_text(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.strip() + "\n", encoding="utf-8")


def build_seed_compare_candidate_tonal_shift() -> None:
    case_dir = ASSETS / "seed_compare_candidate_tonal_shift"
    baseline = sine_mix(3.0, [(120.0, 0.18), (320.0, 0.28), (1180.0, 0.08)], gain=0.95)
    candidate = sine_mix(3.0, [(120.0, 0.10), (510.0, 0.23), (1440.0, 0.12)], gain=0.78)
    write_wav(case_dir / "baseline.wav", clip(baseline))
    write_wav(case_dir / "candidate.wav", clip(candidate))
    write_text(
        case_dir / "notes.md",
        """
        # Seed case: candidate tonal shift

        - Baseline centers around 320 Hz with modest high-frequency support.
        - Candidate is quieter overall and shifts its dominant emphasis upward.
        - This case is meant to exercise compare wording around level and spectral change.
        """,
    )


def build_seed_compare_transform_after_filter() -> None:
    case_dir = ASSETS / "seed_compare_transform_after_filter"
    baseline = sine_mix(3.0, [(55.0, 0.24), (180.0, 0.16), (320.0, 0.14), (1250.0, 0.06)], gain=1.15)
    candidate = sine_mix(3.0, [(55.0, 0.04), (180.0, 0.12), (320.0, 0.12), (1250.0, 0.11)], gain=0.92)
    write_wav(case_dir / "baseline.wav", clip(baseline))
    write_wav(case_dir / "candidate.wav", clip(candidate))
    write_text(
        case_dir / "notes.md",
        """
        # Seed case: after high-pass style change

        - Baseline contains stronger low-end energy and is intentionally hot.
        - Candidate reduces sub/low energy and shifts emphasis upward.
        - This approximates a post-filter before/after comparison for guided explanations.
        """,
    )


def build_seed_explain_spectrogram_variation() -> None:
    case_dir = ASSETS / "seed_explain_spectrogram_variation"
    signal = with_bursts(base_freq=640.0, burst_freq=4200.0, duration=2.8)
    write_wav(case_dir / "signal.wav", signal)
    write_text(
        case_dir / "notes.md",
        """
        # Seed case: spectrogram variation

        - Stable mid-band base tone.
        - Repeating high-band bursts create visible time variation.
        - Intended for spectrogram explanations about sustained energy and temporal change.
        """,
    )


def build_seed_explain_waveform_headroom_coverage() -> None:
    case_dir = ASSETS / "seed_explain_waveform_headroom_coverage"
    core = sine_mix(3.0, [(82.0, 0.24), (209.9, 0.32), (420.0, 0.14), (980.0, 0.08)], gain=1.55)
    noisy = add_white_noise(core, amount=0.018, seed=44)
    hot = clip(noisy, 0.98)
    write_wav(case_dir / "signal.wav", hot)
    write_text(
        case_dir / "notes.md",
        """
        # Seed case: waveform headroom issue

        - Hot mix with clipped peaks and dense sustained energy.
        - Intended to trigger plain-language headroom explanations.
        """,
    )


def build_seed_trimmed_intro_example() -> None:
    case_dir = ASSETS / "seed_trimmed_intro_example"
    core = sine_mix(2.4, [(180.0, 0.18), (360.0, 0.12), (960.0, 0.06)], gain=0.82)
    baseline = with_leading_silence(core, 0.5)
    candidate = core
    write_wav(case_dir / "baseline.wav", clip(baseline))
    write_wav(case_dir / "candidate.wav", clip(candidate))
    write_text(
        case_dir / "notes.md",
        """
        # Seed case: trimmed intro example

        - Baseline includes 0.5 seconds of leading silence.
        - Candidate removes the intro silence.
        - This case is generated as a spare asset for future duration/trim evals.
        """,
    )


def build_seed_no_change_control() -> None:
    case_dir = ASSETS / "seed_no_change_control"
    signal = sine_mix(2.8, [(145.0, 0.18), (290.0, 0.13), (870.0, 0.05)], gain=0.84)
    write_wav(case_dir / "baseline.wav", clip(signal))
    write_wav(case_dir / "candidate.wav", clip(signal))
    write_text(
        case_dir / "notes.md",
        """
        # Seed case: no-change control

        - Baseline and candidate are intentionally identical.
        - This case should make the assistant say the runs are closely matched.
        """,
    )


def build_seed_noise_floor_increase() -> None:
    case_dir = ASSETS / "seed_noise_floor_increase"
    baseline = sine_mix(3.0, [(170.0, 0.16), (340.0, 0.11), (1020.0, 0.04)], gain=0.80)
    candidate = add_white_noise(baseline, amount=0.028, seed=19)
    air = sine_mix(3.0, [(4800.0, 0.018), (7200.0, 0.010)], gain=1.0)
    candidate = [sample + air_sample for sample, air_sample in zip(candidate, air)]
    write_wav(case_dir / "baseline.wav", clip(baseline))
    write_wav(case_dir / "candidate.wav", clip(candidate))
    write_text(
        case_dir / "notes.md",
        """
        # Seed case: raised noise floor

        - Baseline is a clean tonal signal with modest upper-band content.
        - Candidate adds broadband white noise plus a small amount of very high-frequency energy.
        - Intended to exercise compare wording around hiss-like or raised background noise.
        """,
    )


def main() -> None:
    build_seed_compare_candidate_tonal_shift()
    build_seed_compare_transform_after_filter()
    build_seed_explain_spectrogram_variation()
    build_seed_explain_waveform_headroom_coverage()
    build_seed_trimmed_intro_example()
    build_seed_no_change_control()
    build_seed_noise_floor_increase()
    print(f"Seed corpus generated under {ASSETS}")


if __name__ == "__main__":
    main()
