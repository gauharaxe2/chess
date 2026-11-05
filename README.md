# Chess Board

**C# 2D chess for Android (Google Play)** — offline dual-player on one device, and online multiplayer via Unity Gaming Services Lobby.

Open the Unity project in [`ChessMobile/`](ChessMobile/).

**Browser preview (no Unity required):** serve [`WebPlayable/`](WebPlayable/) and open it locally:

```bash
cd WebPlayable && python3 -m http.server 8765
# then open http://127.0.0.1:8765/
```

**Firebase** (`chess-duel-426622`): Auth (Google / Play Games / guest / email), Firestore profiles/matches, Android `google-services.json`. See [`docs/FIREBASE.md`](docs/FIREBASE.md) and [`docs/GOOGLE_PLAY_GAMES.md`](docs/GOOGLE_PLAY_GAMES.md).

## Quick start

1. Install **Unity Hub** + **Unity 2022.3 LTS** with the **Android Build Support** module (SDK + NDK + OpenJDK).
2. Hub → **Add** → select the `ChessMobile` folder.
3. Open the project, press **Play**. The UI boots automatically (no scene wiring needed).
4. Menu → **Chess Board → Run Engine Self-Test** to verify rules.

## Game modes

| Mode | How it works |
|------|----------------|
| **Offline Dual Player** | Hotseat on one phone — first to **2 checkmates** wins the match. |
| **Online — Host** | Creates a lobby and shows a room code. You play White. First to 2 checkmates. |
| **Online — Join** | Enter the room code. You play Black. |

### Online backend

- **Production (Play Store):** Unity Authentication + Lobby (turn-based move sync). Link a Unity Project under *Edit → Project Settings → Services*.
- **Fallback without Services:** same-WiFi `LocalLanTransport` on device builds; editor mock for UI checks.

## Play Store build (Android wrapper)

Prebuilt release packages live under [`android-playstore/`](android-playstore/) (WebPlayable inside a signed WebView app — usable without Unity Editor).

```bash
cd android-playstore && ./scripts/make-release.sh
# → dist/ChessBoard-1.0.0.aab  (upload to Play Console)
# → dist/ChessBoard-1.0.0.apk
```

Package ID: `com.chessduel.game`. Prefer uploading the **.aab**. Back up `upload-keystore.jks`.

Native Unity IL2CPP builds still need Unity 2022.3 LTS + Android modules locally (checklist below).

## Play Store publish checklist

1. *Edit → Project Settings → Player → Android*
   - Package Name: `com.chessduel.game` (change to your ID)
   - Minimum API 24, Target API 34+
   - Scripting Backend: **IL2CPP**, Target Architectures: **ARM64** (required by Play)
2. Create a **upload keystore** (*Player → Publishing Settings*).
3. *File → Build Settings → Android → Build App Bundle (AAB)*.
4. Upload the `.aab` in [Google Play Console](https://play.google.com/console).
5. Complete Store listing, content rating, Data safety (mention online multiplayer + Unity Services / anonymous auth).

Internet permission is enabled for online play (`Force Internet Permission`).

## Project layout

```
ChessMobile/
  Assets/Scripts/
    Core/         Chess rules (board, moves, checkmate, FEN)
    Board/        Touch board + piece glyphs
    Game/         Session + bootstrap
    Networking/   Lobby / LAN / mock transports
    UI/           Main menu + HUD (built at runtime)
  ProjectSettings/  Android player defaults
```

## Rules covered

Legal move generation, checks, checkmate, stalemate, castling, en passant, promotion (auto-queen in UI), 50-move rule, threefold repetition, insufficient material.

## License

Project scaffolding for your Play Store release — replace package ID, art, and store listing before publishing.
