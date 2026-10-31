#!/bin/bash
# Thread - double-click this file to start the game on macOS (the first time: right-click > Open).
cd "$(dirname "$0")" || exit 1
APP="Game/Mac/Thread.app"
[ -d "$APP" ] || APP="Builds/Mac/Thread.app"
if [ ! -d "$APP" ]; then
  echo "Thread.app not found. Download the whole folder, including Game/Mac."
  read -r -p "Press Enter to close."
  exit 1
fi
# The app is not signed by Apple: clear the download quarantine and sign it locally (ad hoc), once.
xattr -cr "$APP" 2>/dev/null
chmod +x "$APP"/Contents/MacOS/* 2>/dev/null
codesign --force --deep --sign - "$APP" >/dev/null 2>&1
open "$APP"
