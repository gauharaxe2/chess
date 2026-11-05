import { StatusBar } from "expo-status-bar";
import { useMemo, useReducer } from "react";
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
type Game = ReturnType<typeof createGame>;

type State = {
  screen: Screen;
  game: Game;
  selected: number | null;
  legal: Move[];
  lastMove: { from: number; to: number } | null;
  whiteScore: number;
  blackScore: number;
  matchWinner: "white" | "black" | null;
  scoredCurrent: boolean;
  pendingPromo: { from: number; to: number; options: Move[] } | null;
  status: string;
};

type Action =
  | { type: "START_MATCH" }
  | { type: "BACK_MENU" }
  | { type: "NEXT_GAME" }
  | { type: "SELECT"; square: number; legal: Move[] }
  | { type: "CLEAR_SELECT" }
  | { type: "PROMO_OPEN"; from: number; to: number; options: Move[] }
  | { type: "PROMO_CLOSE" }
  | { type: "AFTER_MOVE"; game: Game; from: number; to: number };

function freshGame(): Game {
  const g = createGame();
  resetGame(g);
  return cloneGame(g);
}

function cloneGame(g: Game): Game {
  return {
    ...g,
    board: g.board.map((p) => (p ? { ...p } : null)),
    castling: { ...g.castling },
    history: [...g.history],
    positionKeys: [...(g.positionKeys || [])],
  };
}

function scoreLine(w: number, b: number) {
  return `Match ${w}–${b} · first to ${CHECKMATES_TO_WIN}`;
}

function initialState(): State {
  return {
    screen: "menu",
    game: freshGame(),
    selected: null,
    legal: [],
    lastMove: null,
    whiteScore: 0,
    blackScore: 0,
    matchWinner: null,
    scoredCurrent: false,
    pendingPromo: null,
    status: "Ready",
  };
}

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case "START_MATCH":
      return {
        ...initialState(),
        screen: "game",
        status: `${scoreLine(0, 0)} · White to move`,
      };
    case "BACK_MENU":
      return { ...initialState(), screen: "menu" };
    case "NEXT_GAME": {
      if (state.matchWinner) {
        return { ...initialState(), screen: "menu" };
      }
      if (!state.game.result) {
        return { ...state, status: "Finish the current game first." };
      }
      return {
        ...state,
        game: freshGame(),
        selected: null,
        legal: [],
        lastMove: null,
        scoredCurrent: false,
        pendingPromo: null,
        status: `${scoreLine(state.whiteScore, state.blackScore)} · White to move`,
      };
    }
    case "SELECT":
      return { ...state, selected: action.square, legal: action.legal };
    case "CLEAR_SELECT":
      return { ...state, selected: null, legal: [] };
    case "PROMO_OPEN":
      return {
        ...state,
        pendingPromo: {
          from: action.from,
          to: action.to,
          options: action.options,
        },
      };
    case "PROMO_CLOSE":
      return { ...state, pendingPromo: null };
    case "AFTER_MOVE": {
      const game = action.game;
      let whiteScore = state.whiteScore;
      let blackScore = state.blackScore;
      let matchWinner = state.matchWinner;
      let scoredCurrent = state.scoredCurrent;
      let status = `${scoreLine(whiteScore, blackScore)} · ${sideName(game.side)} to move`;

      if (game.result && !scoredCurrent && !matchWinner) {
        scoredCurrent = true;
        if (game.result === "white") whiteScore += 1;
        if (game.result === "black") blackScore += 1;

        if (whiteScore >= CHECKMATES_TO_WIN) matchWinner = "white";
        if (blackScore >= CHECKMATES_TO_WIN) matchWinner = "black";

        if (matchWinner) {
          status = `${scoreLine(whiteScore, blackScore)}. Match over — ${
            matchWinner === "white" ? "White" : "Black"
          } wins!`;
        } else if (game.result === "draw") {
          status = `${scoreLine(whiteScore, blackScore)}. Draws don't count — tap Next Game.`;
        } else {
          status = `${scoreLine(whiteScore, blackScore)}. First to ${CHECKMATES_TO_WIN} checkmates. Tap Next Game.`;
        }
      } else if (game.result && matchWinner) {
        status = `${scoreLine(whiteScore, blackScore)}. Match over — ${
          matchWinner === "white" ? "White" : "Black"
        } wins!`;
      }

      return {
        ...state,
        game,
        selected: null,
        legal: [],
        lastMove: { from: action.from, to: action.to },
        pendingPromo: null,
        whiteScore,
        blackScore,
        matchWinner,
        scoredCurrent,
        status,
      };
    }
    default:
      return state;
  }
}

function findKing(board: Game["board"], color: number) {
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
  const [state, dispatch] = useReducer(reducer, undefined, initialState);

  const legalTos = useMemo(() => new Set(state.legal.map((m) => m.to)), [state.legal]);

  function turnText() {
    const { matchWinner, game } = state;
    if (matchWinner) return `Match over — ${matchWinner === "white" ? "White" : "Black"} wins`;
    if (game.result === "white") return "Checkmate — White scores";
    if (game.result === "black") return "Checkmate — Black scores";
    if (game.result === "draw") return `Draw — ${game.drawReason || "agreement"}`;
    if (isInCheck(game)) return `${sideName(game.side)} in check`;
    return `${sideName(game.side)} to move`;
  }

  function playMove(move: Move) {
    if (state.matchWinner || state.game.result) return;
    const draft = cloneGame(state.game);
    if (!tryMove(draft, move)) return;
    dispatch({ type: "AFTER_MOVE", game: draft, from: move.from, to: move.to });
  }

  function onSquarePress(logical: number) {
    if (state.matchWinner || state.game.result || state.pendingPromo) return;

    if (state.selected != null) {
      const options = state.legal.filter((m) => m.to === logical);
      if (options.length) {
        const promos = options.filter((m) => m.promo);
        if (promos.length) {
          dispatch({
            type: "PROMO_OPEN",
            from: state.selected,
            to: logical,
            options: promos,
          });
          return;
        }
        playMove(options[0]);
        return;
      }
      const p = state.game.board[logical];
      if (p && p.color === state.game.side) {
        dispatch({
          type: "SELECT",
          square: logical,
          legal: legalFrom(state.game, logical),
        });
        return;
      }
      dispatch({ type: "CLEAR_SELECT" });
      return;
    }

    const p = state.game.board[logical];
    if (p && p.color === state.game.side) {
      dispatch({
        type: "SELECT",
        square: logical,
        legal: legalFrom(state.game, logical),
      });
    }
  }

  function squares() {
    const nodes = [];
    const checkSq = isInCheck(state.game) ? findKing(state.game.board, state.game.side) : -1;
    for (let vr = 0; vr < 8; vr++) {
      for (let vf = 0; vf < 8; vf++) {
        const logicalFile = vf;
        const logicalRank = 7 - vr;
        const logical = logicalRank * 8 + logicalFile;
        const piece = state.game.board[logical];
        const light = (logicalFile + logicalRank) % 2 === 1;
        const isSelected = state.selected === logical;
        const isLegal = legalTos.has(logical);
        const isLast =
          state.lastMove &&
          (logical === state.lastMove.from || logical === state.lastMove.to);
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
                backgroundColor: isLast
                  ? light
                    ? "#e8c478"
                    : "#c9a15b"
                  : light
                    ? LIGHT
                    : DARK,
              },
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

  if (state.screen === "menu") {
    return (
      <SafeAreaView style={styles.root}>
        <StatusBar style="light" />
        <ScrollView contentContainerStyle={styles.menuPad}>
          <Text style={styles.brand}>CHESS{"\n"}BOARD</Text>
          <Text style={styles.tagline}>
            Two players. One board. First to{" "}
            <Text style={styles.em}>two checkmates</Text> takes the match.
          </Text>
          <Pressable style={styles.btnPrimary} onPress={() => dispatch({ type: "START_MATCH" })}>
            <Text style={styles.btnLabel}>Offline Dual Player</Text>
            <Text style={styles.btnMeta}>Pass & play · Expo Go</Text>
          </Pressable>
          <Text style={styles.hint}>
            Score a checkmate to earn a point. First to 2 wins the match.
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
          <Text style={styles.score}>{scoreLine(state.whiteScore, state.blackScore)}</Text>
        </View>
        <View style={styles.hudActions}>
          <Text style={styles.status} numberOfLines={3}>
            {state.status}
          </Text>
          <Pressable style={styles.ghostBtn} onPress={() => dispatch({ type: "BACK_MENU" })}>
            <Text style={styles.ghostText}>Menu</Text>
          </Pressable>
        </View>
      </View>

      <View style={[styles.board, { width: boardSize, height: boardSize }]}>
        {squares()}
      </View>

      <View style={styles.footer}>
        {(state.game.result || state.matchWinner) && (
          <Pressable style={styles.btnPrimary} onPress={() => dispatch({ type: "NEXT_GAME" })}>
            <Text style={styles.btnLabel}>
              {state.matchWinner ? "Finish match" : "Next Game"}
            </Text>
            <Text style={styles.btnMeta}>
              {state.matchWinner
                ? "Return to menu — match complete"
                : `Score ${state.whiteScore}–${state.blackScore} · play another board`}
            </Text>
          </Pressable>
        )}
      </View>

      {state.matchWinner ? (
        <View style={styles.modal}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>
              {state.matchWinner === "white" ? "White" : "Black"} wins the match
            </Text>
            <Text style={styles.modalSub}>{scoreLine(state.whiteScore, state.blackScore)}</Text>
            <Pressable style={styles.btnPrimary} onPress={() => dispatch({ type: "BACK_MENU" })}>
              <Text style={styles.btnLabel}>Back to menu</Text>
            </Pressable>
          </View>
        </View>
      ) : null}

      {state.pendingPromo ? (
        <View style={styles.modal}>
          <View style={styles.modalCard}>
            <Text style={styles.modalTitle}>Promote to</Text>
            <View style={styles.promoRow}>
              {[QUEEN, ROOK, BISHOP, KNIGHT].map((t) => (
                <Pressable
                  key={t}
                  style={styles.promoBtn}
                  onPress={() => {
                    const move = state.pendingPromo?.options.find((m) => m.promo === t);
                    dispatch({ type: "PROMO_CLOSE" });
                    if (move) playMove(move);
                  }}
                >
                  <Text style={styles.promoGlyph}>{pieceGlyph(t, state.game.side)}</Text>
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
    padding: 24,
  },
  modalCard: {
    backgroundColor: "#121a2b",
    borderRadius: 14,
    padding: 20,
    alignItems: "center",
    gap: 12,
    width: "100%",
    maxWidth: 340,
  },
  modalTitle: { color: CREAM, fontWeight: "800", fontSize: 20, textAlign: "center" },
  modalSub: { color: MUTED, fontSize: 14, marginBottom: 4 },
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
