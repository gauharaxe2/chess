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
const statusLabel = document.getElementById("status-label");
const menuHint = document.getElementById("menu-hint");
const joinInput = document.getElementById("join-code");
const promoModal = document.getElementById("promo-modal");

const state = {
  game: createGame(),
  mode: "offline", // offline | online-host | online-join
  localColor: WHITE,
  selected: null,
  legal: [],
  pendingPromo: null,
  roomCode: null,
  channel: null,
  flip: false,
};

resetGame(state.game);

function canInteract() {
  if (state.game.result) return false;
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
  if (g.result === "white") turnLabel.textContent = "Checkmate — White wins";
  else if (g.result === "black") turnLabel.textContent = "Checkmate — Black wins";
  else if (g.result === "draw") turnLabel.textContent = `Draw — ${g.drawReason || "agreement"}`;
  else if (isInCheck(g)) turnLabel.textContent = `${sideName(g.side)} in check`;
  else turnLabel.textContent = `${sideName(g.side)} to move`;

  turnLabel.style.color = isInCheck(g) && !g.result ? "var(--danger)" : "";
}

function renderBoard() {
  const g = state.game;
  const checkSq = isInCheck(g) ? findKing(g.board, g.side) : -1;
  const legalTos = new Set(state.legal.map((m) => m.to));

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
      btn.addEventListener("click", () => onSquareClick(logical));
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

function playLocalMove(move) {
  if (!tryMove(state.game, move)) return;
  clearSelection();
  refreshHud();
  if (state.mode !== "offline" && state.channel) {
    state.channel.postMessage({ type: "move", uci: moveToUci(move), room: state.roomCode });
  }
  updateTurnStatus();
  renderBoard();
}

function updateTurnStatus() {
  if (state.game.result) {
    setStatus("Game over");
    return;
  }
  if (state.mode === "offline") {
    setStatus(`${sideName(state.game.side)} to move — pass the device`);
    return;
  }
  setStatus(state.game.side === state.localColor ? "Your turn" : "Opponent's turn");
}

function startOffline() {
  teardownOnline();
  state.mode = "offline";
  state.localColor = WHITE;
  state.flip = false;
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
}

function startHost() {
  teardownOnline();
  const code = randomCode();
  state.mode = "online-host";
  state.localColor = WHITE;
  state.flip = false;
  state.roomCode = code;
  resetGame(state.game);
  clearSelection();

  const channel = new BroadcastChannel("chess-duel-" + code);
  state.channel = channel;
  channel.onmessage = (ev) => handleOnlineMessage(ev.data);

  // Publish room presence for joiners scanning localStorage
  localStorage.setItem("chess-duel-room-" + code, JSON.stringify({ host: true, t: Date.now() }));

  setStatus(`Room code: ${code} — open another tab and Join`);
  menuHint.textContent = `Hosting ${code}`;
  showGame();
}

function startJoin(code) {
  code = (code || "").trim();
  if (!/^\d{6}$/.test(code)) {
    menuHint.textContent = "Enter a 6-digit room code from the host.";
    menuHint.style.color = "var(--danger)";
    return;
  }
  teardownOnline();
  state.mode = "online-join";
  state.localColor = BLACK;
  state.flip = true;
  state.roomCode = code;
  resetGame(state.game);
  clearSelection();

  const channel = new BroadcastChannel("chess-duel-" + code);
  state.channel = channel;
  channel.onmessage = (ev) => handleOnlineMessage(ev.data);
  channel.postMessage({ type: "hello", room: code });

  setStatus("Joined — you are Black. Waiting for White…");
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
  if (data.type === "move" && data.uci) {
    // Only apply opponent moves
    if (canInteract()) return; // ignore if somehow our turn echo
    if (tryUci(state.game, data.uci)) {
      clearSelection();
      refreshHud();
      updateTurnStatus();
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
document.getElementById("btn-new").addEventListener("click", () => {
  if (state.mode === "offline") startOffline();
  else setStatus("Start a new online match from the menu.");
});

joinInput.addEventListener("keydown", (e) => {
  if (e.key === "Enter") startJoin(joinInput.value);
});

void BLACK;
