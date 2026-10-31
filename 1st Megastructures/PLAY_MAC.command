#!/bin/bash
# Abyss - start the game on macOS (Intel and Apple silicon).
# The game is not signed by Apple, so macOS marks it as "downloaded from the internet":
# this removes that mark from the game only, then opens it.
cd "$(dirname "$0")" || exit 1
APP="Game/Mac/Abyss.app"
xattr -cr "$APP" 2>/dev/null
chmod +x "$APP/Contents/MacOS/"* 2>/dev/null
open "$APP"
