# Firebase setup — Chess Duel

**Project:** `chess-duel-426622`  
**Console:** https://console.firebase.google.com/project/chess-duel-426622/overview

## What’s provisioned

| Resource | Status |
|----------|--------|
| Firebase project | Created |
| Web app `Chess Duel Web` | Created |
| Android app `com.chessduel.game` | Created |
| Firestore `(default)` in `asia-south1` (STANDARD) | Created |
| Auth: Anonymous + Email/Password | Enabled |
| Security rules + indexes | Deployed |

## Collections (profiles + matches)

- `profiles/{uid}` — display name, username, email, stats  
- `usernames/{username}` — unique username claim  
- `matches/{matchId}` — online rooms, scores (first to 2 checkmates), moves  

## Local web app

Config lives in `WebPlayable/firebase/config.js`.

```bash
cd WebPlayable && python3 -m http.server 8765
# open http://127.0.0.1:8765/
```

Create a profile (guest or email), then Host/Join online.

## Unity / Android

- `ChessMobile/Assets/StreamingAssets/google-services.json`  
- `FirebaseProfileService` signs in anonymously and writes a Firestore profile  
- For production, also import the official Firebase Auth + Firestore Unity packages  

## Redeploy rules

```bash
npx -y firebase-tools@latest deploy --only firestore,auth --project chess-duel-426622
```

I've set up prototype Security Rules to keep Firestore data safe (signed-in only; users can only write their own profile; match updates limited to participants). Review them in `firestore.rules` before a broad release.
