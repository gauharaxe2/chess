using System;

namespace ChessMobile.Firebase
{
    [Serializable]
    public sealed class ProfileStats
    {
        public int checkmatesScored;
        public int matchesWon;
        public int matchesLost;
        public int gamesPlayed;
        public int draws;
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public string uid;
        public string displayName;
        public string username;
        public string email;
        public string photoURL;
        public string provider;
        public ProfileStats stats = new ProfileStats();
    }
}
