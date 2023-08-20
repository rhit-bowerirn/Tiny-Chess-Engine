using System;
using System.Collections;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        int[] material = { 0, 1, 3, 3, 5, 9, 100 };
        Move[] moves = board.GetLegalMoves();
        int[] moveEvals = new int[moves.Length];
        int maxValue = -1000;


        for (int i = 0; i < moves.Length; i++) {
            Move move = moves[i];
            board.MakeMove(move);
            if(board.IsInCheckmate()) {
                return move;
            }

            //we relinquish control of the square we move to and risk our material (I added material of capture piece and promotion)
            int value = -1 * material[(int)move.MovePieceType] + material[(int)move.CapturePieceType] + material[(int)move.PromotionPieceType] - 10;
            
            //calculate danger
            Move[] opponentMoves = board.GetLegalMoves();
            foreach (Move opponentMove in opponentMoves) {
                if (opponentMove.TargetSquare.Index == move.TargetSquare.Index) {
                    //opponent piece puts pressure on the square
                    value -= 10;
                }
            }
            board.UndoMove(move);
            moveEvals[i] = value;
            if (value > maxValue) {
                maxValue = value;
            }
        }
        
        List<Move> viableMoves = new();
        for(int i = 0; i < moves.Length; i++) {
            if (moveEvals[i] == maxValue) {
                viableMoves.Add(moves[i]);
            }
        }

        return viableMoves[new Random().Next(0, viableMoves.Count)];
    }

}
