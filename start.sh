#!/usr/bin/env bash
set -euo pipefail

export DISPLAY="${DISPLAY:-:99}"

Xvfb "$DISPLAY" -screen 0 1920x1080x24 -ac >/tmp/xvfb.log 2>&1 &
sleep 2

fluxbox >/tmp/fluxbox.log 2>&1 &

x11vnc -display "$DISPLAY" -forever -shared -nopw -rfbport 5900 >/tmp/x11vnc.log 2>&1 &

websockify --web=/usr/share/novnc/ 6080 localhost:5900 >/tmp/novnc.log 2>&1 &

exec dotnet /app/LagoaSportRpa.dll
