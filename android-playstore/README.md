# Chess Board — Android Play Store package

Signed Android wrapper around [`WebPlayable/`](../WebPlayable/) for Google Play upload.

| Field | Value |
|-------|--------|
| Application ID | `com.chessduel.game` |
| App name | Chess Board |
| Min / target SDK | 24 / 34 |

> **Note:** This agent environment does not include Unity Editor, so this ships the playable web client inside a WebView (same rules, menus, Firebase). A native Unity IL2CPP AAB still requires Unity 2022.3 + Android Build Support on a machine with a Unity license (see root README).

## Build release APK + AAB

```bash
cd android-playstore
./scripts/make-release.sh
```

Outputs:

- `dist/ChessBoard-1.0.0.apk` — sideload / internal testing
- `dist/ChessBoard-1.0.0.aab` — **upload this to Play Console** (Play prefers AAB)

The script creates `upload-keystore.jks` + `keystore.properties` on first run. **Back up the keystore** — you need the same key for every Play update.
