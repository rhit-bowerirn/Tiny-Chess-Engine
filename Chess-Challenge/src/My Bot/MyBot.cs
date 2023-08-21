using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    public Move Think(Board board, Timer timer)
    {
        int[] material = { 0, 1, 3, 3, 5, 9, 10 };
        Move[] moves = board.GetLegalMoves();
        int[] moveEvals = new int[moves.Length];
        int maxValue = -10000;

        string currentFen = board.GetFenString();


        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];

            //we relinquish control of the square we move to and risk our material (I added material of capture piece and promotion)
            int value = -1 * material[(int)move.MovePieceType] + 2 * material[(int)move.CapturePieceType];
            if (move.MovePieceType == PieceType.Pawn)
            {
                if (board.IsWhiteToMove)
                {
                    value += move.TargetSquare.Rank;
                }
                else value += 8 - move.TargetSquare.Rank;
            }
            else
            {
                value -= 10;
            }

            //Calculate our control of the square
            Board attackChecker = ScapeGoat(currentFen, move.TargetSquare, !board.IsWhiteToMove);
            foreach (Move m in attackChecker.GetLegalMoves())
            {
                if (m.TargetSquare.Equals(move.TargetSquare))
                {
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
                board.MakeMove(opponentMove);
                if (board.IsInCheckmate())
                {
                    value -= 1000;
                }
                board.UndoMove(opponentMove);
            }

            board.ForceSkipTurn();

            //find new threats we create
            foreach (Move newMove in board.GetLegalMoves())
            {
                if (newMove.StartSquare == move.TargetSquare && newMove.IsCapture)
                {
                    value += material[(int)newMove.CapturePieceType];
                }
            }

            //find all the pieces we defend now
            string hypotheticalFen = board.GetFenString();
            PieceList[] pieces = board.GetAllPieceLists();
            foreach (PieceList pl in pieces)
            {
                if (pl.IsWhitePieceList == board.IsWhiteToMove)
                {
                    for (int index = 0; index < pl.Count; index++)
                    {
                        Piece p = pl.GetPiece(index);
                        if (!p.IsKing && !(p.Square == move.TargetSquare))
                        {
                            Board defenseChecker = ScapeGoat(hypotheticalFen, p.Square, !p.IsWhite);
                            foreach (Move defence in defenseChecker.GetLegalMoves())
                            {
                                if (defence.StartSquare.Index == move.TargetSquare.Index && defence.TargetSquare.Index == p.Square.Index)
                                {
                                    value += material[(int)p.PieceType];
                                }
                            }
                        }
                    }
                }
            }

            board.UndoSkipTurn();

            board.UndoMove(move);
            moveEvals[i] = value;
            if (value > maxValue)
            {
                maxValue = value;
            }
        }

        //pick a random move
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

    // ScapeGoat takes the current board and puts a bishop on a given square
    // This is used to find all the pieces that control a given square
    // En passant doesn't need to be accounted for here
    public Board ScapeGoat(string fen, Square square, bool isWhite)
    {
        string[] rows = fen.Split('/');
        StringBuilder newRow = new(Regex.Replace(rows[7 - square.Rank], @"\d+", match => new string('1', int.Parse(match.Value))));
        newRow[square.File] = isWhite ? 'B' : 'b';
        rows[7 - square.Rank] = Regex.Replace(newRow.ToString(), "1+", match => match.Value.Length.ToString());

        return Board.CreateBoardFromFEN(string.Join("/", rows));
    }


}
