using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        int[] material = { 0, 1, 3, 3, 5, 9, 100 };
        Move[] moves = board.GetLegalMoves();
        int[] moveEvals = new int[moves.Length];
        int maxValue = -1000;

        string currentFen = board.GetFenString();


        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];

            //we relinquish control of the square we move to and risk our material (I added material of capture piece and promotion)
            int value = -1 * material[(int)move.MovePieceType] + material[(int)move.CapturePieceType] + material[(int)move.PromotionPieceType] - 10;
            

            //Calculate our control of the square
            Board attackChecker = scapeGoat(currentFen, move.TargetSquare, !board.IsWhiteToMove);
            Console.WriteLine(attackChecker.CreateDiagram(true, false, false));

            foreach(Move m in attackChecker.GetLegalMoves()) {
                if (m.TargetSquare.Equals(move.TargetSquare)) {
                    value += 10;
                }
            }

            board.MakeMove(move);

            if (board.IsInCheckmate())
            {
                return move;
            }
            

            //calculate opponent's control of the square
            Move[] opponentMoves = board.GetLegalMoves();
            foreach (Move opponentMove in opponentMoves)
            {
                if (opponentMove.TargetSquare.Index == move.TargetSquare.Index)
                {
                    //opponent piece puts pressure on the square
                    value -= 10;
                }
            }

            //TODO calculate new threats
            //TODO calculate new defence

            board.UndoMove(move);
            moveEvals[i] = value;
            if (value > maxValue)
            {
                maxValue = value;
            }
        }

        List<Move> viableMoves = new();
        for (int i = 0; i < moves.Length; i++)
        {
            if (moveEvals[i] == maxValue)
            {
                viableMoves.Add(moves[i]);
            }
        }

        return viableMoves[new Random().Next(0, viableMoves.Count)];
    }

    //scapeGoat takes the current board and puts a bishop on a given square
    //this is used to find all attackers of a given square
    //TODO: account for en passant
    public Board scapeGoat(string currentFen, Square square, bool isWhite)
    {
        string[] rows = currentFen.Split('/');
        StringBuilder newRow = new(Regex.Replace(rows[7 - square.Rank], @"\d+", match => new string('1', int.Parse(match.Value))));
        newRow[square.File] = isWhite ? 'B' : 'b';
        rows[7 - square.Rank] = Regex.Replace(newRow.ToString(), "1+", match => match.Value.Length.ToString());
        
        return Board.CreateBoardFromFEN(string.Join("/", rows));
    }


}
