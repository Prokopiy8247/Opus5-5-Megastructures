# Critic brief (Part 11.5 critic panel)

You are one read-only critic of a panel reviewing captures of **The Ring**, a first-person walk across the inside of a
ringworld, built for a YouTube video whose goal is megalophobia through beauty and silence. Root:
`W:\YouTube\Megastructures\2nd Megastructures\2nd Megastructures` (quote paths, they contain spaces).

## Read first
- `MASTER_PROMPT.md` Part 14: 14.0 digest, 14.1 concept and emotion curve, 14.2 look bible (palette, lighting rig, fog,
  post, materials), 14.5 (the seven beats: purpose, geometry, key frames, acceptance), 14.10 (key-shot table, thresholds).
  Part 10 is the visual defect catalogue. Part 1.2 the principles of scale.
- `DEVELOPMENT.md` "Decisions (ADRs)": deliberate deviations from the spec (for example ADR-011 the 12 m pier and 200 m hull
  drop, ADR-012 the lighter valley fog and haze model, ADR-013 the night grade). Do not report an ADR as a spec gap; do
  report it if the result still looks wrong.
- The capture folder named in your prompt: key shots `ksNN_<tag>_<W>x<H>.png`, beat stills `beatNN_<name>_<tag>.png`
  (start / mid / end and free-look `free_yaw..._p...`), defect stills `t2/t3/t5/t7/h10_*`, beat-5 extras `beat05_*_x6/x7/x8*`,
  `report.json` (pose, s, vFOV of every image), `metrics.md` / `metrics.json` (automated luma, clipping, banding, seams).

## Do
- Open and look at every image relevant to your lens with the Read tool (at least every 1920x1080 key shot; 4K crops where
  detail matters). Measure when you claim a number (python -I with Pillow/numpy is allowed for analysis; write scripts to
  your scratch space, never into the project).
- Judge against your lens and the beat's acceptance list. Be concrete and adversarial: a finding needs evidence (file,
  image region in pixels, measured values) and a proposed fix that an engineer can act on.
- Severity: **S1** blocker (breaks the illusion or a money shot, a visible defect a viewer notices at once), **S2** major
  (clearly visible flaw or missed acceptance criterion), **S3** minor (polish, nitpick, or a risk worth noting).
- Report only real problems. Do not pad. Do not repeat a finding under several ids.

## Do not
- Edit, create or delete any file in the project. Run Unity or the player. Commit.

## Output (your final message): one JSON object, nothing else
```
{"lens": "<your lens>", "run": "<capture folder>",
 "findings": [{"id": "<lens>-NN", "severity": "S1|S2|S3", "beat": 1-7 or 0 (global),
   "title": "<one line>", "evidence": "<files, pixel regions, measured numbers>",
   "cause": "<suspected cause>", "fix": "<proposed fix>"}],
 "beat_verdicts": {"1": "<one or two sentences>", "...": "...", "7": "..."}}
```
