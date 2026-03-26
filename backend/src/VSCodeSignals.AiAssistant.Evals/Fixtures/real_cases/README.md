# Real-Case Ground Truth

Drop real product cases in this folder as JSON suites that follow the same schema as `guided_compare_ground_truth.json`.

Recommended workflow:

1. Create or capture a real baseline/candidate pair as lossless audio when possible:
   - `baseline.wav`
   - `candidate.wav`
2. Store those files, screenshots, and notes under `assets/<case-id>/`.
3. Create or update a JSON suite in this folder, for example `hardware_validation.real_cases.json`.
4. Add one or more scenarios with:
   - `id`
   - `label`
   - `enabled`
   - `prompt`
   - `operation`
   - `activeView`
   - `signal`
   - optional `comparison`
   - optional `artifacts`
   - `expected`
5. Keep the scenario disabled while it is still draft:
   - `"enabled": false`
6. Enable it only when the expectations are final and you want it to gate the eval run.

Recommended folder layout:

```text
real_cases/
  README.md
  template.real_case.json
  hardware_validation.real_cases.json
  filter_regressions.real_cases.json
  spectrogram_review.real_cases.json
  assets/
    README.md
    case_001/
      baseline.wav
      candidate.wav
      notes.md
      screenshot.png
```

Guidelines:

- Keep each scenario focused on one primary expected finding.
- Use `requiredAnswerPhrases` for wording that must appear in the answer.
- Use `requiredPrimaryFindingPhrases` and `requiredImpactPhrases` to lock the compare briefing structure.
- Use `requiredFactCodes` to ensure the summary card stays grounded in the intended evidence.
- Use `forbiddenAnswerPhrases` to block bad wording, overclaiming, or user-unfriendly phrasing.
- Prefer real product language over DSP jargon when you write expectations.
- Prefer WAV or FLAC for the actual artifacts. Keep MP3 only when the real-world case depends on the encoded file itself.

Start from:

- `template.real_case.json`
- one of the seeded starter suites in this folder

Seed corpus:

- Run `python3 generate_seed_corpus.py` from this folder to create a small deterministic WAV corpus under `assets/`.
- The generated suite in `seed_corpus.real_cases.json` is enabled by default and is intended to be your first always-on ground-truth set.
