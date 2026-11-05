import {
  BISHOP,
  BLACK,
  KNIGHT,
  QUEEN,
  ROOK,
  WHITE,
  createGame,
  isInCheck,
  legalFrom,
  moveToUci,
  pieceGlyph,
  resetGame,
  sideName,
  tryMove,
  tryUci,
} from "./chess.js";
import { mountProfileUI } from "./firebase/profiles-ui.js";
import {
  firebaseReady,
  createMatchDoc,
  joinMatchByRoomCode,
  listenMatch,
  pushMatchMove,
  recordMatchOutcome,
  auth,
} from "./firebase/app.js";

const CHECKMATES_TO_WIN = 2;

function findKing(board, color) {
  for (let i = 0; i < 64; i++) {
    const p = board[i];
    if (p && p.type === 6 && p.color === color) return i;
  }
  return -1;
}

const menu = document.getElementById("menu");
const gameScreen = document.getElementById("game");
const boardEl = document.getElementById("board");
const turnLabel = document.getElementById("turn-label");
const scoreLabel = document.getElementById("score-label");
const statusLabel = document.getElementById("status-label");
const menuHint = document.getElementById("menu-hint");
const joinInput = document.getElementById("join-code");
const promoModal = document.getElementById("promo-modal");

const state = {
  game: createGame(),
  mode: "offline",
  localColor: WHITE,
  selected: null,
  legal: [],
  pendingPromo: null,
  roomCode: null,
  channel: null,
  flip: false,
  whiteCheckmates: 0,
  blackCheckmates: 0,
  matchWinner: null, // "white" | "black" | null
  scoredCurrentGame: false,
  profile: null,
  firestoreMatchId: null,
  unlistenMatch: null,
  lastMove: null, // { from, to }
};

resetGame(state.game);

mountProfileUI({
  root: document.getElementById("profile-root"),
  onProfile: (profile) => {
    state.profile = profile;
    menuHint.textContent = profile
      ? `Signed in as ${profile.displayName || profile.username || "Player"}. Offline anytime · online via Firebase.`
      : "Sign in to create a cloud profile for online play.";
    menuHint.style.color = "";
  },
});

function requireProfileForOnline() {
  if (!firebaseReady()) {
    menuHint.textContent = "Firebase is not linked yet — online cloud play is unavailable.";
    menuHint.style.color = "var(--danger)";
    return false;
  }
  if (!auth?.currentUser || !state.profile) {
    menuHint.textContent = "Create or sign into a profile before hosting/joining online.";
    menuHint.style.color = "var(--danger)";
    return false;
  }
  return true;
}

function scoreLine() {
  return `Match ${state.whiteCheckmates}–${state.blackCheckmates} · first to ${CHECKMATES_TO_WIN}`;
}

function resetMatchScores() {
  state.whiteCheckmates = 0;
  state.blackCheckmates = 0;
  state.matchWinner = null;
  state.scoredCurrentGame = false;
}

function applyCheckmateScore() {
  if (state.scoredCurrentGame || state.matchWinner) return;
  const result = state.game.result;
  if (!result || result === "draw") {
    state.scoredCurrentGame = true;
    return;
  }

  state.scoredCurrentGame = true;
  if (result === "white") state.whiteCheckmates += 1;
  if (result === "black") state.blackCheckmates += 1;

  if (state.whiteCheckmates >= CHECKMATES_TO_WIN) state.matchWinner = "white";
  if (state.blackCheckmates >= CHECKMATES_TO_WIN) state.matchWinner = "black";
}

function canInteract() {
  if (state.matchWinner || state.game.result) return false;
  if (state.mode === "offline") return true;
  return state.game.side === state.localColor;
}

function showMenu() {
  teardownOnline();
  gameScreen.classList.add("hidden");
  menu.classList.remove("hidden");
}

function showGame() {
  menu.classList.add("hidden");
  gameScreen.classList.remove("hidden");
  renderBoard();
  refreshHud();
}

function setStatus(msg) {
  statusLabel.textContent = msg;
}

function refreshHud() {
  const g = state.game;
  scoreLabel.textContent = scoreLine();

  if (state.matchWinner) {
    turnLabel.textContent = `Match over — ${sideName(state.matchWinner === "white" ? WHITE : BLACK)} wins`;
    turnLabel.style.color = "var(--gold)";
  } else if (g.result === "white") {
    turnLabel.textContent = "Checkmate — White scores";
    turnLabel.style.color = "var(--gold)";
  } else if (g.result === "black") {
    turnLabel.textContent = "Checkmate — Black scores";
    turnLabel.style.color = "var(--gold)";
  } else if (g.result === "draw") {
    turnLabel.textContent = `Draw — ${g.drawReason || "agreement"}`;
    turnLabel.style.color = "var(--gold)";
  } else if (isInCheck(g)) {
    turnLabel.textContent = `${sideName(g.side)} in check`;
    turnLabel.style.color = "var(--danger)";
  } else {
    turnLabel.textContent = `${sideName(g.side)} to move`;
    turnLabel.style.color = "";
  }
}

function syncBoardRails() {
  const files = state.flip ? "hgfedcba" : "abcdefgh";
  const ranks = state.flip ? "12345678" : "87654321";
  document.querySelectorAll(".board-rail--files").forEach((rail) => {
    [...rail.children].forEach((el, i) => {
      el.textContent = files[i];
    });
  });
  document.querySelectorAll(".board-rail--ranks").forEach((rail) => {
    [...rail.children].forEach((el, i) => {
      el.textContent = ranks[i];
    });
  });
}

function renderBoard() {
  const g = state.game;
  const checkSq = isInCheck(g) ? findKing(g.board, g.side) : -1;
  const legalTos = new Set(state.legal.map((m) => m.to));
  syncBoardRails();

  boardEl.innerHTML = "";
  for (let vr = 0; vr < 8; vr++) {
    for (let vf = 0; vf < 8; vf++) {
      let logicalFile = vf;
      let logicalRank = 7 - vr;
      if (state.flip) {
        logicalFile = 7 - vf;
        logicalRank = vr;
      }
      const logical = logicalRank * 8 + logicalFile;
      const piece = g.board[logical];
      const btn = document.createElement("button");
      btn.type = "button";
      btn.className = "sq " + ((logicalFile + logicalRank) % 2 === 1 ? "light" : "dark");
      btn.dataset.square = String.fromCharCode(97 + logicalFile) + (logicalRank + 1);
      btn.setAttribute("aria-label", btn.dataset.square);
      if (state.lastMove && (logical === state.lastMove.from || logical === state.lastMove.to)) {
        btn.classList.add("last-move");
      }
      if (state.selected === logical) btn.classList.add("selected");
      if (legalTos.has(logical)) {
        btn.classList.add("legal");
        if (piece) btn.classList.add("has-piece");
      }
      if (logical === checkSq) btn.classList.add("check");
      if (piece) {
        const span = document.createElement("span");
        span.className = "piece " + (piece.color === WHITE ? "white" : "black");
        span.textContent = pieceGlyph(piece.type, piece.color);
        btn.appendChild(span);
      }
      btn.addEventListener("pointerup", (ev) => {
        ev.preventDefault();
        onSquareClick(logical);
      });
      boardEl.appendChild(btn);
    }
  }
}

function onSquareClick(logical) {
  if (!canInteract()) return;
  const g = state.game;

  if (state.selected != null) {
    const options = state.legal.filter((m) => m.to === logical);
    if (options.length) {
      const promos = options.filter((m) => m.promo);
      if (promos.length) {
        state.pendingPromo = { from: state.selected, to: logical, options: promos };
        promoModal.classList.remove("hidden");
        return;
      }
      playLocalMove(options[0]);
      return;
    }
    const p = g.board[logical];
    if (p && p.color === g.side) {
      selectSquare(logical);
      return;
    }
    clearSelection();
    return;
  }

  const p = g.board[logical];
  if (p && p.color === g.side) selectSquare(logical);
}

function selectSquare(logical) {
  state.selected = logical;
  state.legal = legalFrom(state.game, logical);
  renderBoard();
}

function clearSelection() {
  state.selected = null;
  state.legal = [];
  renderBoard();
}

function afterGameProgress() {
  if (!state.game.result) return;
  const scoredCheckmate = state.game.result === "white" || state.game.result === "black";
  const iScored =
    (state.game.result === "white" && state.localColor === WHITE) ||
    (state.game.result === "black" && state.localColor === BLACK);
  applyCheckmateScore();
  refreshHud();

  const matchWon =
    state.matchWinner &&
    ((state.matchWinner === "white" && state.localColor === WHITE) ||
      (state.matchWinner === "black" && state.localColor === BLACK));
  const matchLost = Boolean(state.matchWinner) && !matchWon;

  if (firebaseReady() && auth?.currentUser && state.mode !== "offline") {
    recordMatchOutcome({
      scoredCheckmate: scoredCheckmate && iScored,
      wonMatch: matchWon,
      lostMatch: matchLost,
      draw: state.game.result === "draw",
    }).catch(() => {});
  }

  if (state.matchWinner) {
    setStatus(`${scoreLine()}. Match over — ${state.matchWinner === "white" ? "White" : "Black"} wins!`);
  } else if (state.game.result === "draw") {
    setStatus(`${scoreLine()}. Draws don't count — tap Next Game.`);
  } else {
    setStatus(`${scoreLine()}. First to ${CHECKMATES_TO_WIN} checkmates wins. Tap Next Game.`);
  }
}

async function syncMoveToCloud(uci) {
  if (!state.firestoreMatchId) return;
  const finished = Boolean(state.matchWinner);
  await pushMatchMove(state.firestoreMatchId, {
    uci,
    fen: "",
    moveSeq: state.game.history.length,
    whiteScore: state.whiteCheckmates,
    blackScore: state.blackCheckmates,
    status: finished ? "finished" : "active",
    winnerUid: null,
  });
}

function playLocalMove(move) {
  if (!tryMove(state.game, move)) return;
  state.lastMove = { from: move.from, to: move.to };
  clearSelection();
  refreshHud();
  const uci = moveToUci(move);
  if (state.mode !== "offline" && state.channel) {
    state.channel.postMessage({ type: "move", uci, room: state.roomCode });
  }
  if (state.game.result) afterGameProgress();
  else updateTurnStatus();
  if (state.firestoreMatchId) syncMoveToCloud(uci).catch(() => {});
  renderBoard();
}

function updateTurnStatus() {
  if (state.game.result || state.matchWinner) return;
  if (state.mode === "offline") {
    setStatus(`${scoreLine()} · ${sideName(state.game.side)} to move`);
    return;
  }
  setStatus(
    `${scoreLine()} · ${state.game.side === state.localColor ? "Your turn" : "Opponent's turn"}`
  );
}

function startNextGame() {
  if (state.matchWinner) {
    resetMatchScores();
    resetGame(state.game);
    state.scoredCurrentGame = false;
    state.lastMove = null;
    clearSelection();
    setStatus("New match — White to move");
    refreshHud();
    renderBoard();
    return;
  }

  if (!state.game.result) {
    setStatus("Finish the current game first.");
    return;
  }

  resetGame(state.game);
  state.scoredCurrentGame = false;
  state.lastMove = null;
  clearSelection();
  setStatus(`${scoreLine()} · White to move`);
  refreshHud();
  renderBoard();

  if (state.mode !== "offline" && state.channel) {
    state.channel.postMessage({ type: "next", room: state.roomCode });
  }
}

function startOffline() {
  teardownOnline();
  state.mode = "offline";
  state.localColor = WHITE;
  state.flip = false;
  state.lastMove = null;
  resetMatchScores();
  resetGame(state.game);
  clearSelection();
  setStatus("Offline dual player — White to move");
  showGame();
}

function randomCode() {
  return String(Math.floor(100000 + Math.random() * 900000));
}

function teardownOnline() {
  if (state.channel) {
    state.channel.close();
    state.channel = null;
  }
  if (state.unlistenMatch) {
    state.unlistenMatch();
    state.unlistenMatch = null;
  }
  state.firestoreMatchId = null;
}

function attachFirestoreListener(matchId) {
  state.firestoreMatchId = matchId;
  if (state.unlistenMatch) state.unlistenMatch();
  let lastSeq = 0;
  state.unlistenMatch = listenMatch(matchId, (data) => {
    if (data.status === "active" && data.guestUid && state.mode === "online-host") {
      setStatus("Opponent joined via Firebase. You are White.");
    }
    if (typeof data.moveSeq === "number" && data.moveSeq > lastSeq && data.lastMove) {
      lastSeq = data.moveSeq;
      if (!canInteract() && tryUci(state.game, data.lastMove)) {
        const hist = state.game.history;
        const last = hist[hist.length - 1];
        if (last) state.lastMove = { from: last.from, to: last.to };
        state.whiteCheckmates = data.whiteScore || state.whiteCheckmates;
        state.blackCheckmates = data.blackScore || state.blackCheckmates;
        clearSelection();
        refreshHud();
        if (state.game.result) afterGameProgress();
        else updateTurnStatus();
        renderBoard();
      }
    }
  });
}

async function startHost() {
  if (!requireProfileForOnline()) return;
  teardownOnline();
  const code = randomCode();
  state.mode = "online-host";
  state.localColor = WHITE;
  state.flip = false;
  state.roomCode = code;
  state.lastMove = null;
  resetMatchScores();
  resetGame(state.game);
  clearSelection();

  // Local tab fallback + Firebase cloud room
  const channel = new BroadcastChannel("chess-duel-" + code);
  state.channel = channel;
  channel.onmessage = (ev) => handleOnlineMessage(ev.data);

  try {
    const matchId = await createMatchDoc(code);
    attachFirestoreListener(matchId);
    setStatus(`Room ${code} (Firebase) — first to 2 checkmates. Waiting for opponent`);
  } catch (e) {
    setStatus(`Room ${code} (local tab only) — ${e.message || e}`);
  }
  menuHint.textContent = `Hosting ${code} as ${state.profile.displayName}`;
  showGame();
}

async function startJoin(code) {
  code = (code || "").trim();
  if (!/^\d{6}$/.test(code)) {
    menuHint.textContent = "Enter a 6-digit room code from the host.";
    menuHint.style.color = "var(--danger)";
    return;
  }
  if (!requireProfileForOnline()) return;
  teardownOnline();
  state.mode = "online-join";
  state.localColor = BLACK;
  state.flip = true;
  state.roomCode = code;
  state.lastMove = null;
  resetMatchScores();
  resetGame(state.game);
  clearSelection();

  const channel = new BroadcastChannel("chess-duel-" + code);
  state.channel = channel;
  channel.onmessage = (ev) => handleOnlineMessage(ev.data);
  channel.postMessage({ type: "hello", room: code });

  try {
    const matchId = await joinMatchByRoomCode(code);
    attachFirestoreListener(matchId);
    setStatus("Joined Firebase match — you are Black.");
  } catch (e) {
    setStatus(`Joined local channel — ${e.message || e}`);
  }
  showGame();
}

function handleOnlineMessage(data) {
  if (!data || data.room !== state.roomCode) return;
  if (data.type === "hello" && state.mode === "online-host") {
    setStatus("Opponent joined. You are White.");
    state.channel.postMessage({ type: "welcome", room: state.roomCode });
    return;
  }
  if (data.type === "welcome" && state.mode === "online-join") {
    setStatus("Connected. Opponent (White) to move.");
    return;
  }
  if (data.type === "next") {
    resetGame(state.game);
    state.scoredCurrentGame = false;
    clearSelection();
    setStatus(`${scoreLine()}. White to move`);
    refreshHud();
    renderBoard();
    return;
  }
  if (data.type === "move" && data.uci) {
    if (canInteract()) return;
    if (tryUci(state.game, data.uci)) {
      const hist = state.game.history;
      const last = hist[hist.length - 1];
      if (last) state.lastMove = { from: last.from, to: last.to };
      clearSelection();
      refreshHud();
      if (state.game.result) afterGameProgress();
      else updateTurnStatus();
      renderBoard();
    }
  }
}

promoModal.querySelectorAll("[data-promo]").forEach((btn) => {
  btn.addEventListener("click", () => {
    const map = { q: QUEEN, r: ROOK, b: BISHOP, n: KNIGHT };
    const promo = map[btn.dataset.promo];
    const pending = state.pendingPromo;
    promoModal.classList.add("hidden");
    state.pendingPromo = null;
    if (!pending) return;
    const move = pending.options.find((m) => m.promo === promo) || pending.options[0];
    playLocalMove(move);
  });
});

document.getElementById("btn-offline").addEventListener("click", startOffline);
document.getElementById("btn-host").addEventListener("click", startHost);
document.getElementById("btn-join").addEventListener("click", () => startJoin(joinInput.value));
document.getElementById("btn-menu").addEventListener("click", showMenu);
document.getElementById("btn-new").addEventListener("click", startNextGame);

joinInput.addEventListener("keydown", (e) => {
  if (e.key === "Enter") startJoin(joinInput.value);
});

void BLACK;
