# Real-Case Assets

Put actual artifacts for each case in a dedicated subfolder:

```text
assets/
  case_id/
    baseline.wav
    candidate.wav
    notes.md
    screenshot.png
```

Recommended conventions:

- Prefer `baseline.wav` and `candidate.wav` as lossless files.
- Add `notes.md` with the human explanation of what actually changed.
- Add `screenshot.png` only when the visual evidence matters for the case.
- Keep filenames stable so the matching JSON scenario does not need to change.

These files are for human review and dataset management. The current eval runner does not decode them yet; it uses the scenario metadata and expectations.

Seed corpus note:

- `../generate_seed_corpus.py` creates a starter set of deterministic audio files in this folder.
- You can regenerate those files safely at any time.
