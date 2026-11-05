import { StatusBar } from "expo-status-bar";
import { useMemo, useState } from "react";
import {
  Pressable,
  SafeAreaView,
  ScrollView,
  StyleSheet,
  Text,
  useWindowDimensions,
  View,
} from "react-native";
import {
  BLACK,
  BISHOP,
  createGame,
  isInCheck,
  KNIGHT,
  legalFrom,
  pieceGlyph,
  QUEEN,
  resetGame,
  ROOK,
  sideName,
  tryMove,
  WHITE,
} from "./src/chess.js";

const CHECKMATES_TO_WIN = 2;
const LIGHT = "#f0d9b5";
const DARK = "#b58863";
const INK = "#0b1220";
const GOLD = "#e8c478";
const CREAM = "#f4efe6";
const MUTED = "#9aa6bf";

type Screen = "menu" | "game";
type Move = { from: number; to: number; promo?: number };

function findKing(board: Array<{ type: number; color: number } | null>, color: number) {
  for (let i = 0; i < 64; i++) {
    const p = board[i];
    if (p && p.type === 6 && p.color === color) return i;
  }
  return -1;
}

export default function App() {
  const { width } = useWindowDimensions();
  const boardSize = Math.min(width - 24, 420);
  const squareSize = boardSize / 8;

  const [screen, setScreen] = useState<Screen>("menu");
  const [game, setGame] = useState(() => createGame());
  const [selected, setSelected] = useState<number | null>(null);
  const [legal, setLegal] = useState<Move[]>([]);
  const [lastMove, setLastMove] = useState<{ from: number; to: number } | null>(null);
  const [whiteScore, setWhiteScore] = useState(0);
  const [blackScore, setBlackScore] = useState(0);
  const [matchWinner, setMatchWinner] = useState<"white" | "black" | null>(null);
  const [scoredCurrent, setScoredCurrent] = useState(false);
  const [pendingPromo, setPendingPromo] = useState<{
    from: number;
    to: number;
    options: Move[];
  } | null>(null);
  const [status, setStatus] = useState("Ready");

  const legalTos = useMemo(() => new Set(legal.map((m) => m.to)), [legal]);

  function bump(nextGame: ReturnType<typeof createGame>) {
    setGame({ ...nextGame, board: [...nextGame.board], history: [...nextGame.history] });
  }

  function scoreLine(w = whiteScore, b = blackScore) {
    return `Match ${w}–${b} · first to ${CHECKMATES_TO_WIN}`;
  }

  function turnText() {
    if (matchWinner) return `Match over — ${matchWinner === "white" ? "White" : "Black"} wins`;
    if (game.result === "white") return "Checkmate — White scores";
    if (game.result === "black") return "Checkmate — Black scores";
    if (game.result === "draw") return `Draw — ${game.drawReason || "agreement"}`;
    if (isInCheck(game)) return `${sideName(game.side)} in check`;
    return `${sideName(game.side)} to move`;
  }

  function startOffline() {
    const g = createGame();
    setGame(g);
    setSelected(null);
    setLegal([]);
    setLastMove(null);
    setWhiteScore(0);
    setBlackScore(0);
    setMatchWinner(null);
    setScoredCurrent(false);
    setPendingPromo(null);
    setStatus(`${scoreLine(0, 0)} · White to move`);
    setScreen("game");
  }

  function nextGame() {
    if (matchWinner) {
      setScreen("menu");
      return;
    }
    const g = createGame();
    resetGame(g);
    setGame(g);
    setSelected(null);
    setLegal([]);
    setLastMove(null);
    setScoredCurrent(false);
    setPendingPromo(null);
    setStatus(`${scoreLine()} · White to move`);
  }

  function applyCheckmateScore(result: string | null) {
    if (scoredCurrent || matchWinner) return;
    setScoredCurrent(true);
    if (!result || result === "draw") return;
    let w = whiteScore;
    let b = blackScore;
    if (result === "white") w += 1;
    if (result === "black") b += 1;
    setWhiteScore(w);
    setBlackScore(b);
    if (w >= CHECKMATES_TO_WIN) setMatchWinner("white");
    if (b >= CHECKMATES_TO_WIN) setMatchWinner("black");
    if (w >= CHECKMATES_TO_WIN || b >= CHECKMATES_TO_WIN) {
      setStatus(`${scoreLine(w, b)}. Match over — ${w >= CHECKMATES_TO_WIN ? "White" : "Black"} wins!`);
    } else if (result === "draw") {
      setStatus(`${scoreLine(w, b)}. Draws don't count — tap Next Game.`);
    } else {
      setStatus(`${scoreLine(w, b)}. First to ${CHECKMATES_TO_WIN} checkmates. Tap Next Game.`);
    }
  }

  function playMove(move: Move) {
    if (matchWinner || game.result) return;
    if (!tryMove(game, move)) return;
    setLastMove({ from: move.from, to: move.to });
    setSelected(null);
    setLegal([]);
    bump(game);
    if (game.result) applyCheckmateScore(game.result);
    else setStatus(`${scoreLine()} · ${sideName(game.side)} to move`);
  }

  function onSquarePress(logical: number) {
    if (matchWinner || game.result || pendingPromo) return;

    if (selected != null) {
      const options = legal.filter((m) => m.to === logical);
      if (options.length) {
        const promos = options.filter((m) => m.promo);
        if (promos.length) {
          setPendingPromo({ from: selected, to: logical, options: promos });
          return;
        }
        playMove(options[0]);
        return;
      }
      const p = game.board[logical];
      if (p && p.color === game.side) {
        setSelected(logical);
        setLegal(legalFrom(game, logical));
        return;
      }
      setSelected(null);
      setLegal([]);
      return;
    }

    const p = game.board[logical];
    if (p && p.color === game.side) {
      setSelected(logical);
      setLegal(legalFrom(game, logical));
    }
  }

  function squares() {
    const nodes = [];
    const checkSq = isInCheck(game) ? findKing(game.board, game.side) : -1;
    for (let vr = 0; vr < 8; vr++) {
      for (let vf = 0; vf < 8; vf++) {
        const logicalFile = vf;
        const logicalRank = 7 - vr;
        const logical = logicalRank * 8 + logicalFile;
        const piece = game.board[logical];
        const light = (logicalFile + logicalRank) % 2 === 1;
        const isSelected = selected === logical;
        const isLegal = legalTos.has(logical);
        const isLast =
          lastMove && (logical === lastMove.from || logical === lastMove.to);
        const inCheck = logical === checkSq;

        nodes.push(
          <Pressable
            key={logical}
            onPress={() => onSquarePress(logical)}
            style={[
              styles.sq,
              {
                width: squareSize,
                height: squareSize,
                backgroundColor: light ? LIGHT : DARK,
              },
              isLast ? { backgroundColor: light ? "#e8c478" : "#c9a15b" } : null,
              isSelected ? styles.selected : null,
              inCheck ? styles.check : null,
            ]}
          >
            {isLegal && !piece ? <View style={styles.legalDot} /> : null}
            {isLegal && piece ? (
              <View pointerEvents="none" style={styles.legalRing} />
            ) : null}
            {piece ? (
              <Text
                style={[
                  styles.piece,
                  { fontSize: squareSize * 0.62 },
                  piece.color === WHITE ? styles.pieceWhite : styles.pieceBlack,
                ]}
              >
                {pieceGlyph(piece.type, piece.color)}
              </Text>
            ) : null}
          </Pressable>
        );
      }
    }
    return nodes;
  }

  if (screen === "menu") {
    return (
      <SafeAreaView style={styles.root}>
        <StatusBar style="light" />
        <ScrollView contentContainerStyle={styles.menuPad}>
          <Text style={styles.brand}>CHESS{"\n"}BOARD</Text>
          <Text style={styles.tagline}>
            Two players. One board. First to{" "}
            <Text style={styles.em}>two checkmates</Text> takes the match.
          </Text>
          <Pressable style={styles.btnPrimary} onPress={startOffline}>
            <Text style={styles.btnLabel}>Offline Dual Player</Text>
            <Text style={styles.btnMeta}>Pass & play · Expo Go</Text>
          </Pressable>
          <Text style={styles.hint}>
            Open this project in Expo Go, then tap Offline Dual Player.
          </Text>
        </ScrollView>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.root}>
      <StatusBar style="light" />
      <View style={styles.hud}>
        <View style={{ flex: 1 }}>
          <Text style={styles.hudBrand}>CHESS BOARD</Text>
          <Text style={styles.turn}>{turnText()}</Text>
          <Text style={styles.score}>{scoreLine()}</Text>
        </View>
        <View style={styles.hudActions}>
          <Text style={styles.status} numberOfLines={3}>
            {status}
          </Text>
          <Pressable style={styles.ghostBtn} onPress={() => setScreen("menu")}>
            <Text style={styles.ghostText}>Menu</Text>
          </Pressable>
        </View>
      </View>

      <View style={[styles.board, { width: boardSize, height: boardSize }]}>
        {squares()}
      </View>

      <View style={styles.footer}>
        {(game.result || matchWinner) && (
          <Pressable style={styles.btnPrimary} onPress={nextGame}>
            <Text style={styles.btnLabel}>{matchWinner ? "Back to Menu" : "Next Game"}</Text>
          </Pressable>
        )}
      </View>

      {pendingPromo ? (
        <View style={styles.modal}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>Promote to</Text>
            <View style={styles.promoRow}>
              {[QUEEN, ROOK, BISHOP, KNIGHT].map((t) => (
                <Pressable
                  key={t}
                  style={styles.promoBtn}
                  onPress={() => {
                    const move = pendingPromo.options.find((m) => m.promo === t);
                    setPendingPromo(null);
                    if (move) playMove(move);
                  }}
                >
                  <Text style={styles.promoGlyph}>{pieceGlyph(t, game.side)}</Text>
                </Pressable>
              ))}
            </View>
          </View>
        </View>
      ) : null}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: INK },
  menuPad: { padding: 24, paddingTop: 48, gap: 16 },
  brand: {
    color: GOLD,
    fontSize: 56,
    fontWeight: "800",
    lineHeight: 52,
    letterSpacing: -1,
  },
  tagline: { color: MUTED, fontSize: 17, lineHeight: 26, maxWidth: 340 },
  em: { color: CREAM, fontWeight: "700" },
  btnPrimary: {
    backgroundColor: "#b8893a",
    borderRadius: 14,
    paddingVertical: 16,
    paddingHorizontal: 18,
  },
  btnLabel: { color: CREAM, fontWeight: "800", fontSize: 17 },
  btnMeta: { color: "rgba(244,239,230,0.72)", marginTop: 4, fontSize: 14 },
  hint: { color: MUTED, fontSize: 14, lineHeight: 20 },
  hud: {
    flexDirection: "row",
    justifyContent: "space-between",
    gap: 12,
    paddingHorizontal: 16,
    paddingTop: 12,
    paddingBottom: 10,
  },
  hudBrand: { color: GOLD, fontWeight: "800", fontSize: 13, letterSpacing: 1 },
  turn: { color: CREAM, fontWeight: "700", fontSize: 18, marginTop: 4 },
  score: { color: MUTED, marginTop: 2, fontSize: 13 },
  hudActions: { alignItems: "flex-end", maxWidth: 140, gap: 8 },
  status: { color: MUTED, fontSize: 12, textAlign: "right" },
  ghostBtn: {
    borderWidth: 1,
    borderColor: "rgba(232,196,120,0.28)",
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  ghostText: { color: CREAM, fontWeight: "700", fontSize: 13 },
  board: {
    alignSelf: "center",
    flexDirection: "row",
    flexWrap: "wrap",
    borderRadius: 8,
    overflow: "hidden",
  },
  sq: { alignItems: "center", justifyContent: "center" },
  selected: { borderWidth: 3, borderColor: "#f0c14a" },
  check: { borderWidth: 3, borderColor: "#ff5a5a" },
  legalDot: {
    width: 14,
    height: 14,
    borderRadius: 99,
    backgroundColor: "rgba(74,210,149,0.85)",
  },
  legalRing: {
    position: "absolute",
    top: 4,
    right: 4,
    bottom: 4,
    left: 4,
    borderRadius: 99,
    borderWidth: 3,
    borderColor: "rgba(74,210,149,0.9)",
  },
  piece: { fontWeight: "700" },
  pieceWhite: {
    color: "#f7f4ea",
    textShadowColor: "#111",
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 1,
  },
  pieceBlack: { color: "#1c1714" },
  footer: { padding: 16 },
  modal: {
    ...StyleSheet.absoluteFillObject,
    backgroundColor: "rgba(8,14,12,0.72)",
    alignItems: "center",
    justifyContent: "center",
  },
  modalCard: {
    backgroundColor: "#121a2b",
    borderRadius: 14,
    padding: 20,
    alignItems: "center",
  },
  modalTitle: { color: CREAM, fontWeight: "700", fontSize: 16, marginBottom: 12 },
  promoRow: { flexDirection: "row", gap: 10 },
  promoBtn: {
    width: 56,
    height: 56,
    borderRadius: 10,
    backgroundColor: "#b8893a",
    alignItems: "center",
    justifyContent: "center",
  },
  promoGlyph: { fontSize: 30, color: CREAM },
});
