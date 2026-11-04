/** Pure chess rules engine (mirrors Unity Core). */

export const WHITE = 0;
export const BLACK = 1;

export const NONE = 0;
export const PAWN = 1;
export const KNIGHT = 2;
export const BISHOP = 3;
export const ROOK = 4;
export const QUEEN = 5;
export const KING = 6;

const GLYPHS = {
  [WHITE]: { [KING]: "♔", [QUEEN]: "♕", [ROOK]: "♖", [BISHOP]: "♗", [KNIGHT]: "♘", [PAWN]: "♙" },
  [BLACK]: { [KING]: "♚", [QUEEN]: "♛", [ROOK]: "♜", [BISHOP]: "♝", [KNIGHT]: "♞", [PAWN]: "♟" },
};

export function pieceGlyph(type, color) {
  return GLYPHS[color]?.[type] || "";
}

export function emptyBoard() {
  return Array(64).fill(null);
}

export function initialBoard() {
  const b = emptyBoard();
  const back = [ROOK, KNIGHT, BISHOP, QUEEN, KING, BISHOP, KNIGHT, ROOK];
  for (let f = 0; f < 8; f++) {
    b[idx(f, 0)] = { type: back[f], color: WHITE };
    b[idx(f, 1)] = { type: PAWN, color: WHITE };
    b[idx(f, 6)] = { type: PAWN, color: BLACK };
    b[idx(f, 7)] = { type: back[f], color: BLACK };
  }
  return b;
}

export function idx(file, rank) {
  return rank * 8 + file;
}

export function sq(i) {
  return { file: i % 8, rank: Math.floor(i / 8) };
}

export function algebraic(i) {
  const { file, rank } = sq(i);
  return String.fromCharCode(97 + file) + (rank + 1);
}

function cloneState(state) {
  return {
    board: state.board.map((p) => (p ? { ...p } : null)),
    side: state.side,
    castling: { ...state.castling },
    ep: state.ep,
    halfmove: state.halfmove,
    fullmove: state.fullmove,
  };
}

export function createGame() {
  return {
    board: initialBoard(),
    side: WHITE,
    castling: { wk: true, wq: true, bk: true, bq: true },
    ep: null,
    halfmove: 0,
    fullmove: 1,
    history: [],
    positionKeys: [],
    result: null,
    drawReason: null,
  };
}

function opposite(c) {
  return c === WHITE ? BLACK : WHITE;
}

function onBoard(file, rank) {
  return file >= 0 && file < 8 && rank >= 0 && rank < 8;
}

function findKing(board, color) {
  for (let i = 0; i < 64; i++) {
    const p = board[i];
    if (p && p.type === KING && p.color === color) return i;
  }
  return -1;
}

function isAttacked(board, target, byColor) {
  const t = sq(target);
  const pawnDir = byColor === WHITE ? 1 : -1;
  for (const df of [-1, 1]) {
    const f = t.file - df;
    const r = t.rank - pawnDir;
    if (onBoard(f, r)) {
      const p = board[idx(f, r)];
      if (p && p.type === PAWN && p.color === byColor) return true;
    }
  }

  const knightD = [
    [-1, -2], [1, -2], [-2, -1], [2, -1],
    [-2, 1], [2, 1], [-1, 2], [1, 2],
  ];
  for (const [df, dr] of knightD) {
    const f = t.file + df;
    const r = t.rank + dr;
    if (!onBoard(f, r)) continue;
    const p = board[idx(f, r)];
    if (p && p.type === KNIGHT && p.color === byColor) return true;
  }

  const kingD = [
    [-1, -1], [0, -1], [1, -1], [-1, 0], [1, 0], [-1, 1], [0, 1], [1, 1],
  ];
  for (const [df, dr] of kingD) {
    const f = t.file + df;
    const r = t.rank + dr;
    if (!onBoard(f, r)) continue;
    const p = board[idx(f, r)];
    if (p && p.type === KING && p.color === byColor) return true;
  }

  const rays = [
    { dirs: [[-1, -1], [-1, 1], [1, -1], [1, 1]], types: [BISHOP, QUEEN] },
    { dirs: [[-1, 0], [1, 0], [0, -1], [0, 1]], types: [ROOK, QUEEN] },
  ];
  for (const ray of rays) {
    for (const [df, dr] of ray.dirs) {
      let f = t.file + df;
      let r = t.rank + dr;
      while (onBoard(f, r)) {
        const p = board[idx(f, r)];
        if (p) {
          if (p.color === byColor && ray.types.includes(p.type)) return true;
          break;
        }
        f += df;
        r += dr;
      }
    }
  }
  return false;
}

function pushMove(list, from, to, flags = {}) {
  list.push({ from, to, ...flags });
}

function genPseudo(state) {
  const moves = [];
  const { board, side, ep, castling } = state;

  for (let from = 0; from < 64; from++) {
    const piece = board[from];
    if (!piece || piece.color !== side) continue;
    const s = sq(from);

    if (piece.type === PAWN) {
      const dir = side === WHITE ? 1 : -1;
      const start = side === WHITE ? 1 : 6;
      const promoRank = side === WHITE ? 7 : 0;
      const one = idx(s.file, s.rank + dir);
      if (onBoard(s.file, s.rank + dir) && !board[one]) {
        if (s.rank + dir === promoRank) {
          for (const promo of [QUEEN, ROOK, BISHOP, KNIGHT]) {
            pushMove(moves, from, one, { promo });
          }
        } else {
          pushMove(moves, from, one);
          if (s.rank === start) {
            const two = idx(s.file, s.rank + 2 * dir);
            if (!board[two]) pushMove(moves, from, two, { doublePush: true });
          }
        }
      }
      for (const df of [-1, 1]) {
        const f = s.file + df;
        const r = s.rank + dir;
        if (!onBoard(f, r)) continue;
        const to = idx(f, r);
        const target = board[to];
        if (target && target.color !== side) {
          if (r === promoRank) {
            for (const promo of [QUEEN, ROOK, BISHOP, KNIGHT]) {
              pushMove(moves, from, to, { promo, capture: true });
            }
          } else pushMove(moves, from, to, { capture: true });
        }
        if (ep != null && to === ep) {
          pushMove(moves, from, to, { enPassant: true, capture: true });
        }
      }
    } else if (piece.type === KNIGHT) {
      for (const [df, dr] of [
        [-1, -2], [1, -2], [-2, -1], [2, -1], [-2, 1], [2, 1], [-1, 2], [1, 2],
      ]) {
        const f = s.file + df;
        const r = s.rank + dr;
        if (!onBoard(f, r)) continue;
        const to = idx(f, r);
        const t = board[to];
        if (!t) pushMove(moves, from, to);
        else if (t.color !== side) pushMove(moves, from, to, { capture: true });
      }
    } else if (piece.type === BISHOP || piece.type === ROOK || piece.type === QUEEN) {
      const dirs = [];
      if (piece.type !== ROOK) dirs.push([-1, -1], [-1, 1], [1, -1], [1, 1]);
      if (piece.type !== BISHOP) dirs.push([-1, 0], [1, 0], [0, -1], [0, 1]);
      for (const [df, dr] of dirs) {
        let f = s.file + df;
        let r = s.rank + dr;
        while (onBoard(f, r)) {
          const to = idx(f, r);
          const t = board[to];
          if (!t) pushMove(moves, from, to);
          else {
            if (t.color !== side) pushMove(moves, from, to, { capture: true });
            break;
          }
          f += df;
          r += dr;
        }
      }
    } else if (piece.type === KING) {
      for (const [df, dr] of [
        [-1, -1], [0, -1], [1, -1], [-1, 0], [1, 0], [-1, 1], [0, 1], [1, 1],
      ]) {
        const f = s.file + df;
        const r = s.rank + dr;
        if (!onBoard(f, r)) continue;
        const to = idx(f, r);
        const t = board[to];
        if (!t) pushMove(moves, from, to);
        else if (t.color !== side) pushMove(moves, from, to, { capture: true });
      }

      const inCheck = isAttacked(board, from, opposite(side));
      if (!inCheck) {
        if (side === WHITE) {
          if (castling.wk) tryCastle(moves, state, from, 6, 5, "castleK");
          if (castling.wq) tryCastle(moves, state, from, 2, 3, "castleQ", 1);
        } else {
          if (castling.bk) tryCastle(moves, state, from, 62, 61, "castleK");
          if (castling.bq) tryCastle(moves, state, from, 58, 59, "castleQ", 57);
        }
      }
    }
  }
  return moves;
}

function tryCastle(moves, state, from, kingTo, pass, flag, alsoEmpty) {
  const { board, side } = state;
  if (board[kingTo] || board[pass]) return;
  if (alsoEmpty != null && board[alsoEmpty]) return;
  const enemy = opposite(side);
  if (isAttacked(board, pass, enemy) || isAttacked(board, kingTo, enemy)) return;
  pushMove(moves, from, kingTo, { [flag]: true });
}

function applyMove(state, move) {
  const next = cloneState(state);
  const piece = next.board[move.from];
  const captured = next.board[move.to];
  const isPawn = piece.type === PAWN;
  let isCapture = !!captured || move.enPassant;

  next.board[move.to] = piece;
  next.board[move.from] = null;

  if (move.enPassant) {
    const capRank = piece.color === WHITE ? sq(move.to).rank - 1 : sq(move.to).rank + 1;
    next.board[idx(sq(move.to).file, capRank)] = null;
    isCapture = true;
  }

  if (move.castleK) {
    const rank = sq(move.from).rank;
    next.board[idx(5, rank)] = next.board[idx(7, rank)];
    next.board[idx(7, rank)] = null;
  }
  if (move.castleQ) {
    const rank = sq(move.from).rank;
    next.board[idx(3, rank)] = next.board[idx(0, rank)];
    next.board[idx(0, rank)] = null;
  }

  if (move.promo) next.board[move.to] = { type: move.promo, color: piece.color };

  if (piece.type === KING) {
    if (piece.color === WHITE) {
      next.castling.wk = false;
      next.castling.wq = false;
    } else {
      next.castling.bk = false;
      next.castling.bq = false;
    }
  }
  if (piece.type === ROOK) {
    if (move.from === 0) next.castling.wq = false;
    if (move.from === 7) next.castling.wk = false;
    if (move.from === 56) next.castling.bq = false;
    if (move.from === 63) next.castling.bk = false;
  }
  if (move.to === 0) next.castling.wq = false;
  if (move.to === 7) next.castling.wk = false;
  if (move.to === 56) next.castling.bq = false;
  if (move.to === 63) next.castling.bk = false;

  next.ep = move.doublePush ? idx(sq(move.from).file, (sq(move.from).rank + sq(move.to).rank) / 2) : null;
  next.halfmove = isPawn || isCapture ? 0 : next.halfmove + 1;
  if (next.side === BLACK) next.fullmove += 1;
  next.side = opposite(next.side);
  return next;
}

export function legalMoves(state) {
  const pseudo = genPseudo(state);
  const legal = [];
  for (const m of pseudo) {
    const next = applyMove(state, m);
    const king = findKing(next.board, state.side);
    if (king >= 0 && !isAttacked(next.board, king, opposite(state.side))) legal.push(m);
  }
  return legal;
}

export function legalFrom(state, from) {
  return legalMoves(state).filter((m) => m.from === from);
}

export function isInCheck(state) {
  const king = findKing(state.board, state.side);
  return king >= 0 && isAttacked(state.board, king, opposite(state.side));
}

function positionKey(state) {
  let key = state.board.map((p) => (p ? `${p.color}${p.type}` : ".")).join("");
  key += state.side;
  key += `${state.castling.wk}${state.castling.wq}${state.castling.bk}${state.castling.bq}`;
  key += state.ep ?? "-";
  return key;
}

function insufficient(board) {
  let minors = 0;
  let bishops = 0;
  for (const p of board) {
    if (!p || p.type === KING) continue;
    if (p.type === PAWN || p.type === ROOK || p.type === QUEEN) return false;
    minors++;
    if (p.type === BISHOP) bishops++;
  }
  return minors === 0 || minors === 1 || (minors === 2 && bishops === 2);
}

export function tryMove(game, move) {
  if (game.result) return false;
  const legal = legalMoves(game);
  const match = legal.find(
    (m) =>
      m.from === move.from &&
      m.to === move.to &&
      (move.promo ? m.promo === move.promo : !m.promo || m.promo === QUEEN)
  );
  if (!match) return false;

  const chosen =
    match.promo && !move.promo
      ? legal.find((m) => m.from === move.from && m.to === move.to && m.promo === QUEEN) || match
      : match;

  const next = applyMove(game, chosen);
  Object.assign(game, {
    board: next.board,
    side: next.side,
    castling: next.castling,
    ep: next.ep,
    halfmove: next.halfmove,
    fullmove: next.fullmove,
  });
  game.history.push(chosen);
  game.positionKeys.push(positionKey(game));
  evaluateEnd(game);
  return true;
}

export function moveToUci(move) {
  let u = algebraic(move.from) + algebraic(move.to);
  if (move.promo) {
    u += { [QUEEN]: "q", [ROOK]: "r", [BISHOP]: "b", [KNIGHT]: "n" }[move.promo];
  }
  return u;
}

export function tryUci(game, uci) {
  if (!uci || uci.length < 4) return false;
  const from = algebraicToIndex(uci.slice(0, 2));
  const to = algebraicToIndex(uci.slice(2, 4));
  const promoChar = uci[4];
  const promo = promoChar
    ? { q: QUEEN, r: ROOK, b: BISHOP, n: KNIGHT }[promoChar.toLowerCase()]
    : undefined;
  return tryMove(game, { from, to, promo });
}

function algebraicToIndex(a) {
  return idx(a.charCodeAt(0) - 97, Number(a[1]) - 1);
}

function evaluateEnd(game) {
  const moves = legalMoves(game);
  if (moves.length === 0) {
    if (isInCheck(game)) {
      game.result = game.side === WHITE ? "black" : "white";
    } else {
      game.result = "draw";
      game.drawReason = "stalemate";
    }
    return;
  }
  if (game.halfmove >= 100) {
    game.result = "draw";
    game.drawReason = "50-move";
    return;
  }
  if (insufficient(game.board)) {
    game.result = "draw";
    game.drawReason = "material";
    return;
  }
  const key = positionKey(game);
  if (game.positionKeys.filter((k) => k === key).length >= 3) {
    game.result = "draw";
    game.drawReason = "repetition";
  }
}

export function resetGame(game) {
  const fresh = createGame();
  Object.assign(game, fresh);
  game.positionKeys = [positionKey(game)];
}

export function sideName(side) {
  return side === WHITE ? "White" : "Black";
}
