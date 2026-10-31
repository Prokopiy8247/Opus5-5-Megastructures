# Encode test summary (Part 11.12)

Clips: 10 s at 60 fps (1920x1080) from the built player, encoded with ffmpeg libx264 (CRF 23, 30, preset medium, yuv420p) and libvpx-vp9 (CRF 31, 40), decoded, BandScore on declared regions (Tools/qa/encode_test.py). Per-clip reports: QA/reports/encode_test_enc_*.md, crops in QA/encode/.

| Clip | Region | Source score | Source steps/1000 | Worst decoded steps/1000 | Steps added by encoding | Worst decoded run (px) | Worst residual rms (codes) | Region |
|---|---|---|---|---|---|---|---|---|
| enc_beat01_sky | cli0 | 0.46 | 0.00 | 0.00 | +0.00 | 24 | 1.03 | clean gradient |
| enc_beat01_sky_lsb1.5 | cli0 | 0.33 | 0.00 | 0.00 | +0.00 | 26 | 1.29 | clean gradient |
| enc_beat01_sky_lsb2.0 | cli0 | 0.33 | 0.00 | 0.00 | +0.00 | 28 | 1.36 | clean gradient |
| enc_beat02_fog | cli0 | 51.53 | 92.42 | 90.57 | -1.85 | 25 | 3.28 | region contains structure |
| enc_beat03_bank | cli0 | 0.71 | 0.00 | 0.00 | +0.00 | 20 | 0.57 | clean gradient |
| enc_beat04_shade | cli0 | 173.09 | 79.43 | 97.76 | +18.33 | 29 | 17.23 | region contains structure |
| enc_beat05_sky | cli0 | 60.13 | 35.75 | 36.80 | +1.05 | 23 | 4.37 | region contains structure |
| enc_beat06_sea | cli0 | 281.90 | 563.81 | 563.81 | +0.00 | 25 | 16.24 | region contains structure |
| enc_beat06_sea | cli1 | 6.00 | 7.67 | 10.23 | +2.56 | 13 | 1.03 | region contains structure |
| enc_beat06_sky | cli0 | 42.59 | 85.17 | 86.23 | +1.06 | 28 | 1.32 | region contains structure |
| enc_beat07_dark | cli0 | 344.01 | 215.56 | 208.20 | -7.36 | 29 | 22.65 | region contains structure |
| enc_beat07_limb | cli0 | 26.82 | 53.64 | 57.47 | +3.83 | 26 | 1.17 | region contains structure |
| enc_trans_bank | cli0 | 73.08 | 146.16 | 148.26 | +2.10 | 30 | 3.80 | region contains structure |
| enc_trans_deck | cli0 | 302.75 | 351.21 | 359.62 | +8.41 | 52 | 26.61 | region contains structure |

Findings: (1) In the clean gradient regions (beat-1 day sky, beat-3 white bank) the source passes the full BandScore (score <= 0.72, 0 steps) and no codec adds a step edge (0.00 steps/1000 after decoding). (2) The encoders flatten the 1 LSB dither into plateaus (longest constant run 13-28 px, residual 0.34-1.36 codes RMS), which fails the strict run/residual sub-criteria; raising the dither to 1.5 or 2.0 LSB does not change this (tested: encode_test_enc_beat01_sky_lsb1.5 / _lsb2.0), because at these bitrates (130-540 kbit/s) the encoders discard the noise. Visually (200 percent crops) CRF 30 shows faint macroblock blotches but no gradient bands. (3) Regions that contain real structure (stars, rails, cable ridges, the cloud field) are not gradient tests; there the decoded step counts track the source within the run-to-run noise. Decision: keep the specified temporal TPDF dither at 1.0 LSB (x1.5 in the dark), recommend uploading at 1440p or 4K (YouTube assigns VP9/AV1 at higher bitrates).
