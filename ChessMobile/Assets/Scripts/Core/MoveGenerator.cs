using System.Collections.Generic;

namespace ChessMobile.Core
{
    public static class MoveGenerator
    {
        private static readonly int[] KnightDeltas = { -17, -15, -10, -6, 6, 10, 15, 17 };
        private static readonly int[] KingDeltas = { -9, -8, -7, -1, 1, 7, 8, 9 };
        private static readonly int[] BishopDirs = { -9, -7, 7, 9 };
        private static readonly int[] RookDirs = { -8, -1, 1, 8 };

        public static List<Move> GenerateLegalMoves(BoardState board)
        {
            var pseudo = GeneratePseudoLegalMoves(board);
            var legal = new List<Move>(pseudo.Count);
            foreach (var move in pseudo)
            {
                if (IsLegal(board, move))
                    legal.Add(move);
            }

            return legal;
        }

        public static List<Move> GenerateLegalMovesFrom(BoardState board, Square from)
        {
            var all = GenerateLegalMoves(board);
            var filtered = new List<Move>();
            foreach (var move in all)
            {
                if (move.From == from)
                    filtered.Add(move);
            }

            return filtered;
        }

        public static bool IsLegal(BoardState board, Move move)
        {
            var clone = board.Clone();
            ApplyMove(clone, move);
            return !IsInCheck(clone, board.SideToMove);
        }

        public static bool IsInCheck(BoardState board, PieceColor color)
        {
            Square king = board.FindKing(color);
            return IsSquareAttacked(board, king, Opposite(color));
        }

        public static bool IsSquareAttacked(BoardState board, Square square, PieceColor byColor)
        {
            // Pawns
            int pawnDir = byColor == PieceColor.White ? 1 : -1;
            for (int df = -1; df <= 1; df += 2)
            {
                var s = new Square(square.File - df, square.Rank - pawnDir);
                if (s.IsValid)
                {
                    var p = board[s];
                    if (p.Type == PieceType.Pawn && p.Color == byColor)
                        return true;
                }
            }

            // Knights
            foreach (int delta in KnightDeltas)
            {
                if (!TryOffset(square, delta, out Square target)) continue;
                var p = board[target];
                if (p.Type == PieceType.Knight && p.Color == byColor)
                    return true;
            }

            // King
            foreach (int delta in KingDeltas)
            {
                if (!TryOffset(square, delta, out Square target)) continue;
                var p = board[target];
                if (p.Type == PieceType.King && p.Color == byColor)
                    return true;
            }

            // Sliding
            if (IsAttackedBySlider(board, square, byColor, BishopDirs, true)) return true;
            if (IsAttackedBySlider(board, square, byColor, RookDirs, false)) return true;

            return false;
        }

        private static bool IsAttackedBySlider(BoardState board, Square square, PieceColor byColor, int[] dirs, bool diagonal)
        {
            foreach (int dir in dirs)
            {
                Square current = square;
                while (TryOffset(current, dir, out Square next))
                {
                    current = next;
                    var p = board[current];
                    if (p.IsEmpty) continue;

                    if (p.Color != byColor) break;

                    if (diagonal)
                    {
                        if (p.Type == PieceType.Bishop || p.Type == PieceType.Queen)
                            return true;
                    }
                    else
                    {
                        if (p.Type == PieceType.Rook || p.Type == PieceType.Queen)
                            return true;
                    }

                    break;
                }
            }

            return false;
        }

        public static List<Move> GeneratePseudoLegalMoves(BoardState board)
        {
            var moves = new List<Move>(64);
            for (int i = 0; i < 64; i++)
            {
                var square = Square.FromIndex(i);
                var piece = board[square];
                if (piece.IsEmpty || piece.Color != board.SideToMove)
                    continue;

                switch (piece.Type)
                {
                    case PieceType.Pawn:
                        GeneratePawnMoves(board, square, piece, moves);
                        break;
                    case PieceType.Knight:
                        GenerateKnightMoves(board, square, piece, moves);
                        break;
                    case PieceType.Bishop:
                        GenerateSlidingMoves(board, square, piece, BishopDirs, moves);
                        break;
                    case PieceType.Rook:
                        GenerateSlidingMoves(board, square, piece, RookDirs, moves);
                        break;
                    case PieceType.Queen:
                        GenerateSlidingMoves(board, square, piece, BishopDirs, moves);
                        GenerateSlidingMoves(board, square, piece, RookDirs, moves);
                        break;
                    case PieceType.King:
                        GenerateKingMoves(board, square, piece, moves);
                        break;
                }
            }

            return moves;
        }

        private static void GeneratePawnMoves(BoardState board, Square from, Piece piece, List<Move> moves)
        {
            int dir = piece.Color == PieceColor.White ? 1 : -1;
            int startRank = piece.Color == PieceColor.White ? 1 : 6;
            int promoRank = piece.Color == PieceColor.White ? 7 : 0;

            var one = new Square(from.File, from.Rank + dir);
            if (one.IsValid && board[one].IsEmpty)
            {
                if (one.Rank == promoRank)
                    AddPromotions(from, one, MoveFlags.None, moves);
                else
                    moves.Add(new Move(from, one));

                if (from.Rank == startRank)
                {
                    var two = new Square(from.File, from.Rank + 2 * dir);
                    if (two.IsValid && board[two].IsEmpty)
                        moves.Add(new Move(from, two, MoveFlags.DoublePawnPush));
                }
            }

            for (int df = -1; df <= 1; df += 2)
            {
                var capture = new Square(from.File + df, from.Rank + dir);
                if (!capture.IsValid) continue;

                var target = board[capture];
                if (!target.IsEmpty && target.Color != piece.Color)
                {
                    if (capture.Rank == promoRank)
                        AddPromotions(from, capture, MoveFlags.Capture, moves);
                    else
                        moves.Add(new Move(from, capture, MoveFlags.Capture));
                }

                if (board.EnPassantTarget.HasValue && capture == board.EnPassantTarget.Value)
                    moves.Add(new Move(from, capture, MoveFlags.EnPassant | MoveFlags.Capture));
            }
        }

        private static void AddPromotions(Square from, Square to, MoveFlags baseFlags, List<Move> moves)
        {
            PieceType[] promos = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };
            foreach (var promo in promos)
                moves.Add(new Move(from, to, baseFlags | MoveFlags.Promotion, promo));
        }

        private static void GenerateKnightMoves(BoardState board, Square from, Piece piece, List<Move> moves)
        {
            foreach (int delta in KnightDeltas)
            {
                if (!TryOffset(from, delta, out Square to)) continue;
                var target = board[to];
                if (target.IsEmpty)
                    moves.Add(new Move(from, to));
                else if (target.Color != piece.Color)
                    moves.Add(new Move(from, to, MoveFlags.Capture));
            }
        }

        private static void GenerateSlidingMoves(BoardState board, Square from, Piece piece, int[] dirs, List<Move> moves)
        {
            foreach (int dir in dirs)
            {
                Square current = from;
                while (TryOffset(current, dir, out Square next))
                {
                    current = next;
                    var target = board[current];
                    if (target.IsEmpty)
                    {
                        moves.Add(new Move(from, current));
                        continue;
                    }

                    if (target.Color != piece.Color)
                        moves.Add(new Move(from, current, MoveFlags.Capture));
                    break;
                }
            }
        }

        private static void GenerateKingMoves(BoardState board, Square from, Piece piece, List<Move> moves)
        {
            foreach (int delta in KingDeltas)
            {
                if (!TryOffset(from, delta, out Square to)) continue;
                var target = board[to];
                if (target.IsEmpty)
                    moves.Add(new Move(from, to));
                else if (target.Color != piece.Color)
                    moves.Add(new Move(from, to, MoveFlags.Capture));
            }

            // Castling
            if (IsInCheck(board, piece.Color))
                return;

            if (piece.Color == PieceColor.White)
            {
                if (board.WhiteCastleKingSide)
                    TryAddCastle(board, from, new Square(6, 0), new Square(5, 0), MoveFlags.CastleKingSide, moves);
                if (board.WhiteCastleQueenSide)
                    TryAddCastle(board, from, new Square(2, 0), new Square(3, 0), MoveFlags.CastleQueenSide, moves, alsoEmpty: new Square(1, 0));
            }
            else
            {
                if (board.BlackCastleKingSide)
                    TryAddCastle(board, from, new Square(6, 7), new Square(5, 7), MoveFlags.CastleKingSide, moves);
                if (board.BlackCastleQueenSide)
                    TryAddCastle(board, from, new Square(2, 7), new Square(3, 7), MoveFlags.CastleQueenSide, moves, alsoEmpty: new Square(1, 7));
            }
        }

        private static void TryAddCastle(
            BoardState board,
            Square kingFrom,
            Square kingTo,
            Square rookPass,
            MoveFlags flag,
            List<Move> moves,
            Square? alsoEmpty = null)
        {
            if (!board[kingTo].IsEmpty || !board[rookPass].IsEmpty)
                return;
            if (alsoEmpty.HasValue && !board[alsoEmpty.Value].IsEmpty)
                return;

            PieceColor enemy = Opposite(board.SideToMove);
            if (IsSquareAttacked(board, rookPass, enemy) || IsSquareAttacked(board, kingTo, enemy))
                return;

            moves.Add(new Move(kingFrom, kingTo, flag));
        }

        public static void ApplyMove(BoardState board, Move move)
        {
            var moving = board[move.From];
            var captured = board[move.To];
            bool isPawn = moving.Type == PieceType.Pawn;
            bool isCapture = move.IsCapture || !captured.IsEmpty;

            board[move.To] = moving;
            board[move.From] = Piece.Empty;

            if ((move.Flags & MoveFlags.EnPassant) != 0)
            {
                int capRank = moving.Color == PieceColor.White ? move.To.Rank - 1 : move.To.Rank + 1;
                board[move.To.File, capRank] = Piece.Empty;
                isCapture = true;
            }

            if ((move.Flags & MoveFlags.CastleKingSide) != 0)
            {
                int rank = move.From.Rank;
                board[5, rank] = board[7, rank];
                board[7, rank] = Piece.Empty;
            }
            else if ((move.Flags & MoveFlags.CastleQueenSide) != 0)
            {
                int rank = move.From.Rank;
                board[3, rank] = board[0, rank];
                board[0, rank] = Piece.Empty;
            }

            if (move.IsPromotion)
                board[move.To] = new Piece(move.PromotionPiece, moving.Color);

            // Castling rights
            UpdateCastlingRights(board, move, moving);

            // En passant target
            if ((move.Flags & MoveFlags.DoublePawnPush) != 0)
            {
                int epRank = (move.From.Rank + move.To.Rank) / 2;
                board.EnPassantTarget = new Square(move.From.File, epRank);
            }
            else
            {
                board.EnPassantTarget = null;
            }

            board.HalfMoveClock = (isPawn || isCapture) ? 0 : board.HalfMoveClock + 1;
            if (board.SideToMove == PieceColor.Black)
                board.FullMoveNumber++;

            board.SideToMove = Opposite(board.SideToMove);
        }

        private static void UpdateCastlingRights(BoardState board, Move move, Piece moving)
        {
            if (moving.Type == PieceType.King)
            {
                if (moving.Color == PieceColor.White)
                {
                    board.WhiteCastleKingSide = false;
                    board.WhiteCastleQueenSide = false;
                }
                else
                {
                    board.BlackCastleKingSide = false;
                    board.BlackCastleQueenSide = false;
                }
            }

            if (moving.Type == PieceType.Rook)
            {
                if (move.From.File == 0 && move.From.Rank == 0) board.WhiteCastleQueenSide = false;
                if (move.From.File == 7 && move.From.Rank == 0) board.WhiteCastleKingSide = false;
                if (move.From.File == 0 && move.From.Rank == 7) board.BlackCastleQueenSide = false;
                if (move.From.File == 7 && move.From.Rank == 7) board.BlackCastleKingSide = false;
            }

            // Captured rook removes rights
            if (move.To.File == 0 && move.To.Rank == 0) board.WhiteCastleQueenSide = false;
            if (move.To.File == 7 && move.To.Rank == 0) board.WhiteCastleKingSide = false;
            if (move.To.File == 0 && move.To.Rank == 7) board.BlackCastleQueenSide = false;
            if (move.To.File == 7 && move.To.Rank == 7) board.BlackCastleKingSide = false;
        }

        public static Move ResolveUci(BoardState board, Square from, Square to, PieceType promotion)
        {
            foreach (var move in GenerateLegalMoves(board))
            {
                if (move.From != from || move.To != to)
                    continue;
                if (move.IsPromotion && move.PromotionPiece != promotion && promotion != PieceType.None)
                    continue;
                if (move.IsPromotion && promotion == PieceType.None && move.PromotionPiece != PieceType.Queen)
                    continue;
                if (!move.IsPromotion || move.PromotionPiece == promotion || (promotion == PieceType.None && move.PromotionPiece == PieceType.Queen))
                    return move;
            }

            // Fallback: first matching from/to
            foreach (var move in GenerateLegalMoves(board))
            {
                if (move.From == from && move.To == to)
                    return move;
            }

            throw new System.InvalidOperationException($"Illegal move {from}{to}");
        }

        private static bool TryOffset(Square from, int delta, out Square to)
        {
            // Convert to 0-63 with file wrap checks via rank/file math.
            int fromIndex = from.Index;
            int toIndex = fromIndex + delta;
            if (toIndex < 0 || toIndex > 63)
            {
                to = default;
                return false;
            }

            to = Square.FromIndex(toIndex);
            int fileDiff = System.Math.Abs(to.File - from.File);
            int rankDiff = System.Math.Abs(to.Rank - from.Rank);

            // Reject wrapping around the board edges for knight/king/slider steps.
            if (delta == -17 || delta == -15 || delta == 15 || delta == 17)
                return fileDiff == 1 && rankDiff == 2;
            if (delta == -10 || delta == -6 || delta == 6 || delta == 10)
                return fileDiff == 2 && rankDiff == 1;
            if (delta == -9 || delta == -7 || delta == 7 || delta == 9)
                return fileDiff == 1 && rankDiff == 1;
            if (delta == -8 || delta == 8)
                return fileDiff == 0 && rankDiff == 1;
            if (delta == -1 || delta == 1)
                return fileDiff == 1 && rankDiff == 0;

            return false;
        }

        private static PieceColor Opposite(PieceColor color) =>
            color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }
}
