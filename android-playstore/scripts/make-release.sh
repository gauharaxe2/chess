#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

SDK_DIR="${ANDROID_SDK_ROOT:-${ANDROID_HOME:-$HOME/android-sdk}}"
if [[ ! -d "$SDK_DIR" ]]; then
  echo "Android SDK not found. Set ANDROID_SDK_ROOT." >&2
  exit 1
fi
echo "sdk.dir=$SDK_DIR" > local.properties

# Refresh embedded web assets from WebPlayable/
if [[ -d "$ROOT/../WebPlayable" ]]; then
  rm -rf app/src/main/assets/www
  mkdir -p app/src/main/assets/www
  cp -a "$ROOT/../WebPlayable/." app/src/main/assets/www/
fi

KEYSTORE="$ROOT/upload-keystore.jks"
PROPS="$ROOT/keystore.properties"
if [[ ! -f "$KEYSTORE" ]]; then
  echo "Creating upload keystore at $KEYSTORE"
  keytool -genkeypair \
    -keystore "$KEYSTORE" \
    -alias upload \
    -keyalg RSA \
    -keysize 2048 \
    -validity 10000 \
    -storepass chessboard-upload \
    -keypass chessboard-upload \
    -dname "CN=Chess Board, OU=Mobile, O=ChessBoard, L=Karachi, ST=Sindh, C=PK"
  cat > "$PROPS" <<EOF
storeFile=upload-keystore.jks
storePassword=chessboard-upload
keyAlias=upload
keyPassword=chessboard-upload
EOF
  echo "IMPORTANT: back up upload-keystore.jks and keystore.properties securely."
fi

chmod +x ./gradlew
./gradlew --no-daemon clean assembleRelease bundleRelease

mkdir -p dist
VERSION="$(grep versionName app/build.gradle | head -1 | sed -E 's/.*"([^"]+)".*/\1/')"
cp -f app/build/outputs/apk/release/app-release.apk "dist/ChessBoard-${VERSION}.apk"
cp -f app/build/outputs/bundle/release/app-release.aab "dist/ChessBoard-${VERSION}.aab"

echo
echo "Built:"
ls -lh "dist/ChessBoard-${VERSION}.apk" "dist/ChessBoard-${VERSION}.aab"
echo
echo "Upload the .aab to Google Play Console (preferred)."
echo "APK path also copied for sideload/testing."
