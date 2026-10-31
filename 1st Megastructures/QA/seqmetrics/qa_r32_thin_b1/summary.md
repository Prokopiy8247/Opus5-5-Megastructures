# Sequence metrics - qa_r32_thin_b1

Source: `C:\Users\<user>\AppData\Local\Temp\claude\W--YouTube-Megastructures-1st-Megastructures-1st-Megastructures\5701b659-2980-4331-b8e2-ecd1860d7675\scratchpad\r32_thin\thin_b1`  
Frames: 30 at 30 fps (1.00 s), size 1920x1080  
Generated: 2026-10-30T16:50:19+01:00

| Check | Value | Limit | Judged | Result |
|---|---|---|---|---|
| spikes | 0 | 0 | no | PASS |
| luma_step | 0.00053994 | 0.02 | no | PASS |
| ev_rate | 0.15057 | 0.35 | no | PASS |
| std | 9.6985 | 6 | yes | FAIL |
| static | None | 1 | no | PASS |

- region `thin`: box_px=[0, 0, 1920, 1080], frames=30, std_levels={'mean': 9.698514748079534, 'p95': 32.193431034898886, 'max': 79.92082389705178}, stat=mean, value=9.698514748079534, limit_levels=6.0, worst_px=[901, 453], pass=False

Overall: **FAIL (std)**
