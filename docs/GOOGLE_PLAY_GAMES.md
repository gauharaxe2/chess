# Google Play Games sign-in

Firebase project **`chess-duel-426622`** has these auth providers enabled:

- Anonymous
- Email / password
- **Google** (`google.com`) — used by the web demo
- **Google Play Games** (`playgames.google.com`) — used by the Android / Play Store build

Web client ID (public, for `requestServerAuthCode`):

`412170643988-ss8bg9d85rh2emm6rk3vlc1rlrrjm44r.apps.googleusercontent.com`

## What you still do in Google Play Console

Play Games Services must be linked to this Firebase project for device sign-in:

1. Open [Google Play Console](https://play.google.com/console) → your app (or create one for `com.chessduel.game`).
2. **Play Games services → Setup & management → Configuration**.
3. Choose **Yes, my game already uses Google APIs** and select Firebase project `chess-duel-426622`.
4. **Add credential → Game server** → OAuth client = the **Web client** above.
5. **Add credential → Android** → OAuth client = Android client (requires your app **SHA-1** in Firebase).
6. Add tester accounts under **Testers** before release.

Add your upload-keystore SHA-1 in Firebase:

```bash
keytool -list -v -keystore your-upload.keystore -alias your-alias
# then: Firebase Console → Project settings → Android app → Add fingerprint
# or: npx -y firebase-tools@latest apps:android:sha:create <ANDROID_APP_ID> <SHA1>
```

## Unity

1. Import [Play Games Plugin for Unity](https://github.com/playgameservices/play-games-plugin-for-unity).
2. Configure it with the web client ID above.
3. Menu button **Sign in with Google Play Games** calls `GooglePlayGamesAuth`, which exchanges the server auth code with Firebase and writes `profiles/{uid}`.

## Web

Use **Sign in with Google** on http://127.0.0.1:8765/ (Play Games is Android-only).
