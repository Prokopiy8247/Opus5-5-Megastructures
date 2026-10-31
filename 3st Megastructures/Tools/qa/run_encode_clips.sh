#!/usr/bin/env bash
# Encode test (Part 11.12) clip by clip: capture one clip in the built player, encode/decode with ffmpeg,
# measure banding, keep the report and crops, delete the PNG frames (disk budget).
cd "$(dirname "$0")/../.."
clips="${*:-enc_beat01_sky enc_beat02_fog enc_beat03_bank enc_beat04_shade enc_beat05_sky enc_beat06_sea enc_beat06_sky enc_beat07_limb enc_beat07_dark enc_trans_bank enc_trans_deck}"
for c in $clips; do
  d="QA/captures/enc_$c"
  rm -rf "$d"
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/run-player.ps1 -Name "enc_$c" -TimeoutMin 20 -PlayerArgs "-capture,$d,-capture-set,encode,-capture-only,$c${EXTRA:+,$EXTRA}" | grep -E "capture done" | head -1
  case "$c" in
    enc_beat01_sky)   reg="--region 1500,20,1900,300" ;;
    enc_beat02_fog)   reg="--region 870,150,1050,700" ;;
    enc_beat03_bank)  reg="--region 200,40,1700,380" ;;
    enc_beat04_shade) reg="--region 1300,150,1700,650" ;;
    enc_beat06_sea)   reg="--region 100,560,800,1000 --region 100,20,800,420" ;;
    enc_beat07_limb)  reg="--region 60,30,700,300" ;;
    *)                reg="--region 200,40,1700,1000" ;;
  esac
  python -I Tools/qa/encode_test.py "$d/$c" $reg --codecs x264,vp9 --crf-x264 23,30 --crf-vp9 31,40 --sample 8 --run "$c${TAG:-}" 2>&1 | grep -E "^\[encode|FAIL|PASS" | tail -4
  find "$d" -name 'frame_*.png' -delete
done
