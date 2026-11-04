using System;

namespace ChessMobile.Core
{
    /// <summary>Chess square as file (0-7 = a-h) and rank (0-7 = 1-8).</summary>
    [Serializable]
    public readonly struct Square : IEquatable<Square>
    {
        public readonly int File;
        public readonly int Rank;

        public Square(int file, int rank)
        {
            File = file;
            Rank = rank;
        }

        public bool IsValid => File >= 0 && File < 8 && Rank >= 0 && Rank < 8;

        public int Index => Rank * 8 + File;

        public static Square FromIndex(int index) => new Square(index % 8, index / 8);

        public static Square FromAlgebraic(string algebraic)
        {
            if (string.IsNullOrEmpty(algebraic) || algebraic.Length < 2)
                throw new ArgumentException("Invalid algebraic square: " + algebraic);

            int file = char.ToLowerInvariant(algebraic[0]) - 'a';
            int rank = algebraic[1] - '1';
            return new Square(file, rank);
        }

        public string ToAlgebraic() => $"{(char)('a' + File)}{Rank + 1}";

        public bool Equals(Square other) => File == other.File && Rank == other.Rank;
        public override bool Equals(object obj) => obj is Square other && Equals(other);
        public override int GetHashCode() => Index;
        public override string ToString() => ToAlgebraic();

        public static bool operator ==(Square a, Square b) => a.Equals(b);
        public static bool operator !=(Square a, Square b) => !a.Equals(b);
    }
}
