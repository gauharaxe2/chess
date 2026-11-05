declare module "./chess.js" {
  export const WHITE: number;
  export const BLACK: number;
  export const PAWN: number;
  export const KNIGHT: number;
  export const BISHOP: number;
  export const ROOK: number;
  export const QUEEN: number;
  export const KING: number;
  export function pieceGlyph(type: number, color: number): string;
  export function createGame(): {
    board: Array<{ type: number; color: number } | null>;
    side: number;
    result: string | null;
    drawReason: string | null;
    history: unknown[];
  };
  export function resetGame(game: ReturnType<typeof createGame>): void;
  export function legalFrom(
    state: ReturnType<typeof createGame>,
    from: number
  ): Array<{ from: number; to: number; promo?: number }>;
  export function isInCheck(state: ReturnType<typeof createGame>): boolean;
  export function tryMove(
    game: ReturnType<typeof createGame>,
    move: { from: number; to: number; promo?: number }
  ): boolean;
  export function sideName(side: number): string;
}
