# Chess Board — Expo Go

React Native / Expo client for **Chess Board** (offline dual-player, first to 2 checkmates).

## Run in Expo Go

```bash
cd ChessBoardExpo
npm install
npx expo start
```

Scan the QR code with **Expo Go** (Android) or the Camera app (iOS).

Tunnel (if LAN discovery fails):

```bash
npx expo start --tunnel
```

## Notes

- Uses the same rules engine as `WebPlayable/chess.js`.
- Online Firebase / Play Games modes stay in the web & Unity clients for now.
