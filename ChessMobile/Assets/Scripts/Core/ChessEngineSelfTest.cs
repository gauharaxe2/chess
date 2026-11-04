using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChessMobile.Core
{
    /// <summary>
    /// Lightweight in-editor / runtime sanity checks for the rules engine.
    /// Run from menu or call ChessEngineSelfTest.RunAll().
    /// </summary>
    public static class ChessEngineSelfTest
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void LogOnLoad()
        {
            // Only auto-run in editor to keep mobile startup clean.
#if UNITY_EDITOR
            var results = RunAll();
            Debug.Log($"[ChessEngineSelfTest] {results.passed}/{results.total} passed");
#endif
        }

        public static (int passed, int total) RunAll()
        {
            int passed = 0;
            int total = 0;

            void Check(string name, bool ok)
            {
                total++;
                if (ok) passed++;
                else Debug.LogError("[ChessEngineSelfTest] FAIL: " + name);
            }

            var game = new ChessGame();
            Check("start side white", game.Board.SideToMove == PieceColor.White);
            Check("20 opening moves for white", game.GetLegalMoves().Count == 20);

            Check("e2e4 ok", game.TryMakeMoveUci("e2e4"));
            Check("e7e5 ok", game.TryMakeMoveUci("e7e5"));
            Check("illegal e2e4 rejected", !game.TryMakeMoveUci("e2e4"));

            // Scholar's mate path
            game.Reset();
            game.TryMakeMoveUci("e2e4");
            game.TryMakeMoveUci("e7e5");
            game.TryMakeMoveUci("d1h5");
            game.TryMakeMoveUci("b8c6");
            game.TryMakeMoveUci("f1c4");
            game.TryMakeMoveUci("g8f6");
            Check("scholars mate", game.TryMakeMoveUci("h5f7"));
            Check("black checkmated", game.Result == GameResult.WhiteWins);

            // Castling rights preserved until king/rook moves
            game.Reset();
            game.TryMakeMoveUci("e2e4");
            game.TryMakeMoveUci("e7e5");
            game.TryMakeMoveUci("g1f3");
            game.TryMakeMoveUci("b8c6");
            game.TryMakeMoveUci("f1c4");
            game.TryMakeMoveUci("g8f6");
            bool castled = game.TryMakeMoveUci("e1g1");
            Check("white kingside castle", castled);
            Check("king on g1", game.Board[new Square(6, 0)].Type == PieceType.King);
            Check("rook on f1", game.Board[new Square(5, 0)].Type == PieceType.Rook);

            // FEN roundtrip
            string fen = BoardState.CreateInitial().ToFen();
            var fromFen = BoardState.FromFen(fen);
            Check("fen roundtrip", fromFen.ToFen() == fen);

            // Promotion
            var promoBoard = BoardState.FromFen("8/P7/8/8/8/8/8/4K2k w - - 0 1");
            var promoGame = new ChessGame(promoBoard.ToFen());
            // Reconstruct via FEN constructor — ChessGame(string fen) exists
            Check("promo available", promoGame.GetLegalMovesFrom(new Square(0, 6)).Exists(m => m.IsPromotion));
            Check("promo queen", promoGame.TryMakeMoveUci("a7a8q"));
            Check("queen on a8", promoGame.Board[new Square(0, 7)].Type == PieceType.Queen);

            return (passed, total);
        }
    }
}
