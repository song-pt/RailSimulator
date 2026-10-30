#!/bin/sh
set -eu
ROOT="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
CACHE=/Volumes/RailRestoreWork
if [ ! -d "$CACHE" ]; then hdiutil attach "$ROOT/Work/BuildCache.sparseimage" -nobrowse; fi
UNITY="${UNITY:-$CACHE/EditorFull/Unity.pkg.tmp/Payload/Unity/Unity.app/Contents/MacOS/Unity}"
if [ ! -x "$UNITY" ]; then echo 'Unity cache not installed; use Unity Hub to install Editor 6000.6.4f1 with iOS support, then set UNITY environment path.'; exit 1; fi
PROJECT="$CACHE/Project"
mkdir -p "$PROJECT" "$CACHE/Tmp" "$CACHE/Outputs"
rsync -r --exclude '._*' --exclude '.DS_Store' "$ROOT/Source/UnityProject/" "$PROJECT/"
export TMPDIR="$CACHE/Tmp" TMP="$CACHE/Tmp" TEMP="$CACHE/Tmp" UPM_CACHE_ROOT="$CACHE/UPMCache" DEVELOPER_DIR="/Volumes/lkx/临时Xcode/Xcode.app/Contents/Developer"
if [ ! -f "$PROJECT/Assets/Resources/Train/E235Cab.prefab" ]; then "$UNITY" -batchmode -nographics -job-worker-count 2 -buildTarget iOS -projectPath "$PROJECT" -executeMethod Restore104Builder.PrepareAndExit -logFile "$ROOT/Reports/prepare.log"; fi
"$UNITY" -batchmode -nographics -job-worker-count 2 -buildTarget iOS -projectPath "$PROJECT" -executeMethod Restore104Checks.Run -logFile "$ROOT/Reports/checks.log"
"$UNITY" -batchmode -nographics -job-worker-count 2 -buildTarget iOS -projectPath "$PROJECT" -executeMethod Restore104Builder.Build -logFile "$ROOT/Reports/unity_build.log"
rsync -r --exclude '._*' --exclude '.DS_Store' "$PROJECT/Assets/" "$ROOT/Source/UnityProject/Assets/"
rsync -r "$PROJECT/ProjectSettings/" "$ROOT/Source/UnityProject/ProjectSettings/"
PYTHON="${PYTHON:-/Users/tongcu/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/bin/python3}"
"$PYTHON" "$ROOT/Tools/package_ios.py"
