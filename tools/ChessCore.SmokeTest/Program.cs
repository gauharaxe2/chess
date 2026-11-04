using System;
using ChessMobile.Core;

static int Fail(string msg)
{
    Console.Error.WriteLine("FAIL: " + msg);
    return 1;
}

var game = new ChessGame();
if (game.GetLegalMoves().Count != 20)
    return Fail("expected 20 opening moves, got " + game.GetLegalMoves().Count);

if (!game.TryMakeMoveUci("e2e4") || !game.TryMakeMoveUci("e7e5"))
    return Fail("opening moves");

game.Reset();
game.TryMakeMoveUci("e2e4");
game.TryMakeMoveUci("e7e5");
game.TryMakeMoveUci("d1h5");
game.TryMakeMoveUci("b8c6");
game.TryMakeMoveUci("f1c4");
game.TryMakeMoveUci("g8f6");
if (!game.TryMakeMoveUci("h5f7"))
    return Fail("scholars mate move");
if (game.Result != GameResult.WhiteWins)
    return Fail("expected white checkmate, got " + game.Result);

game.Reset();
game.TryMakeMoveUci("e2e4");
game.TryMakeMoveUci("e7e5");
game.TryMakeMoveUci("g1f3");
game.TryMakeMoveUci("b8c6");
game.TryMakeMoveUci("f1c4");
game.TryMakeMoveUci("g8f6");
if (!game.TryMakeMoveUci("e1g1"))
    return Fail("castling");
if (game.Board[new Square(6, 0)].Type != PieceType.King)
    return Fail("king not on g1");

var fen = BoardState.CreateInitial().ToFen();
if (BoardState.FromFen(fen).ToFen() != fen)
    return Fail("fen roundtrip");

var promo = new ChessGame("8/P7/8/8/8/8/8/4K2k w - - 0 1");
if (!promo.TryMakeMoveUci("a7a8q"))
    return Fail("promotion");
if (promo.Board[new Square(0, 7)].Type != PieceType.Queen)
    return Fail("promoted piece");

Console.WriteLine("Chess core smoke test PASSED");
return 0;
