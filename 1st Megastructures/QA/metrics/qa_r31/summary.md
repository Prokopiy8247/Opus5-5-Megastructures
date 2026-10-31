# Image metrics - run qa_r31

Source: `W:\YouTube\Megastructures\1st Megastructures\1st Megastructures\QA\captures\qa_r31`  
Generated: 2026-10-30T16:17:17+01:00 by image_metrics.py 1.0.0  
Shots: 37, pass: 32, fail: 5, errors: 0

Luma = Rec. 709 of sRGB values (0-1); clipped = any channel >= 0.98; band = longest identical 8-bit run (px) in qualifying vertical lines (change > 24 levels) / limit (12 px at 1080p, scaled with height).
| Tag | Beat | Size | Mean luma | p99 | Clipped % | Band px | Magenta | Specks | Result |
|---|---|---|---|---|---|---|---|---|---|
| KS1_gallery_start | 1 | 1920x1080 | 0.198 [0.10-0.20] | 0.492 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS1b_doorway | 1 | 1920x1080 | 0.174 [0.05-0.20] | 0.575 [<=0.90] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS2a_bridge_ahead | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.304 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: wall_macro_residual |
| KS2b_bridge_down | 2 | 1920x1080 | 0.096 [0.05-0.13] | 0.451 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | PASS |
| KS3_column_up | 3 | 1920x1080 | 0.143 [0.06-0.15] | 0.419 [<=0.70] | 0.00 [<=0.3] | 0 / 12.0 | 0 | 0 | FAIL: pour_lifts (warn: pour_lifts_rows) |
| KS4_hall_pistons | 4 | 1920x1080 | 0.168 [0.08-0.18] | 0.498 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5a_lift_city | 5 | 1920x1080 | 0.128 [0.06-0.16] | 0.397 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5b_lift_fan | 5 | 1920x1080 | 0.204 [0.09-0.22] | 0.706 [<=0.95] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS5c_lift_down | 5 | 1920x1080 | 0.088 [0.06-0.16] | 0.204 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| KS6_gantry_fan | 6 | 1920x1080 | 0.212 [0.09-0.22] | 0.491 [<=0.95] | 0.06 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7a_tunnel_mouth | 7 tunnel | 1920x1080 | 0.095 [0.04-0.20] | 0.617 [<=0.98] | 0.00 [<=2.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb | 7 outside | 1920x1080 | 0.403 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: horizon_agreement, amber_saturation, readable_cells |
| KS7c_goal_door | 7 outside | 1920x1080 | 0.412 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: horizon_agreement (warn: amber_coverage_pct) |
| KS7d_look_back | 7 outside | 1920x1080 | 0.290 [0.25-0.45] | 0.460 [<=0.92] | 0.00 [<=1.0] | - | 0 | 0 | PASS |
| KS7b_honeycomb_px_base | 7 outside | 1920x1080 | 0.411 [0.25-0.45] | 0.528 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_move10 | 7 outside | 1920x1080 | 0.384 [0.25-0.45] | 0.497 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_px_yaw2 | 7 outside | 1920x1080 | 0.411 [0.25-0.45] | 0.530 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_px_base | 7 outside | 1920x1080 | 0.412 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_px_move10 | 7 outside | 1920x1080 | 0.384 [0.25-0.45] | 0.498 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7c_goal_door_px_yaw2 | 7 outside | 1920x1080 | 0.412 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| seam_Y0 | None | 1920x1080 | 0.053 | 0.064 | 0.00 | - | 0 | 0 | PASS |
| seam_Y-1332 | None | 1920x1080 | 0.043 | 0.053 | 0.00 | - | 0 | 0 | PASS |
| seam_X0 | None | 1920x1080 | 0.055 | 0.071 | 0.00 | - | 0 | 0 | PASS |
| seam_X1332 | None | 1920x1080 | 0.055 | 0.071 | 0.00 | - | 0 | 0 | PASS |
| seam_Z0 | None | 1920x1080 | 0.061 | 0.075 | 0.00 | - | 0 | 0 | PASS |
| seam_Z-1332 | None | 1920x1080 | 0.061 | 0.075 | 0.00 | - | 0 | 0 | PASS |
| tier_facade_near | 5 | 1920x1080 | 0.120 [0.06-0.16] | 0.270 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| tier_facade_far | 5 | 1920x1080 | 0.121 [0.06-0.16] | 0.281 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| tier_honeycomb_tier1 | 7 outside | 1920x1080 | 0.410 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| tier_honeycomb_tier2 | 7 outside | 1920x1080 | 0.409 [0.25-0.45] | 0.531 [<=0.92] | 0.00 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_freight_t30 | 5 | 1920x1080 | 0.118 [0.06-0.16] | 0.297 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| cue_freight_t31 | 5 | 1920x1080 | 0.118 [0.06-0.16] | 0.297 [<=0.80] | 0.00 [<=0.5] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p0 | 6 | 1920x1080 | 0.172 [0.09-0.22] | 0.363 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p1 | 6 | 1920x1080 | 0.165 [0.09-0.22] | 0.330 [<=0.95] | 0.02 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p2 | 6 | 1920x1080 | 0.181 [0.09-0.22] | 0.338 [<=0.95] | 0.03 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| cue_b6_lookback_p3 | 6 | 1920x1080 | 0.190 [0.09-0.22] | 0.365 [<=0.95] | 0.04 [<=1.0] | 0 / 12.0 | 0 | 0 | PASS |
| KS7b_honeycomb_nopost | 7 outside | 1920x1080 | 0.479 [0.25-0.45] | 0.632 [<=0.92] | 0.18 [<=1.0] | 0 / 12.0 | 0 | 0 | FAIL: horizon_agreement (warn: mean_luma, amber_coverage_pct) |

## Failures

- KS2a_bridge_ahead (beat02_bridge_KS2a_bridge_ahead.png): wall_macro_residual
- KS3_column_up (beat03_ledge_KS3_column_up.png): pour_lifts
- KS7b_honeycomb (beat07_reveal_KS7b_honeycomb.png): horizon_agreement, amber_saturation, readable_cells
- KS7c_goal_door (beat07_reveal_KS7c_goal_door.png): horizon_agreement
- KS7b_honeycomb_nopost (beat07_reveal_KS7b_honeycomb_nopost.png): horizon_agreement
