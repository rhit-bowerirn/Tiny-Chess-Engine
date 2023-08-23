﻿using ChessChallenge.API;
using System;

namespace ChessChallenge.Application
{
    public class ChessPlayer
    {
        public readonly ChallengeController.PlayerType PlayerType;
        public readonly IChessBot? Bot;

        double secondsElapsed;
        int incrementAddedMs;
        int baseTimeMs;

        public ChessPlayer(object instance, ChallengeController.PlayerType type, int baseTimeMs = int.MaxValue)
        {
            this.PlayerType = type;
            Bot = instance as IChessBot;
            this.baseTimeMs = baseTimeMs;

        }

        public void UpdateClock(double dt)
        {
            secondsElapsed += dt;
        }

        public void AddIncrement(int incrementMs)
        {
            incrementAddedMs += incrementMs;
        }

        public int TimeRemainingMs
        {
            get
            {
                if (baseTimeMs == int.MaxValue)
                {
                    return baseTimeMs;
                }
                return (int)Math.Ceiling(Math.Max(0, baseTimeMs - secondsElapsed * 1000.0 + incrementAddedMs));
            }
        }
    }
}
