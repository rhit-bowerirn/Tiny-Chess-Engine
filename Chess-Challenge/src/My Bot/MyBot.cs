﻿using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChessChallenge.API;

public class MyBot : IChessBot
{

    Func<PieceType, int> Material = Type => new int[] { 0, 1, 3, 3, 5, 9, 10 }[(int) Type];

    // ScapeGoat takes the current board and puts a bishop on a given square since we can have 10 bishops but 8 pawns
    // This is used to find all the pieces that control a given square
    // En passant doesn't need to be accounted for here
    private Board ScapeGoat(string fen, Square square, bool pieceIsWhite)
    {
        string[] rows = fen.Split('/');
        StringBuilder newRow = new(Regex.Replace(rows[7 - square.Rank], @"\d+", match => new string('1', int.Parse(match.Value))));
        newRow[square.File] = pieceIsWhite ? 'B' : 'b';
        rows[7 - square.Rank] = Regex.Replace(newRow.ToString(), "1+", match => match.Value.Length.ToString());
        return Board.CreateBoardFromFEN(string.Join("/", rows));
    }

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        int[] moveEvals = new int[moves.Length];
        int maxValue = -10000;
        string currentFen = board.GetFenString();

        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];

            //we relinquish control of the square we move to and risk our Material (I added Material of capture piece and promotion)
            int value = -1 * Material(move.MovePieceType)
                        + 2 * Material(move.CapturePieceType)
                        + ((move.MovePieceType == PieceType.Pawn) ?
                            (board.IsWhiteToMove ? move.TargetSquare.Rank : 7 - move.TargetSquare.Rank)
                            : -10)
                        + ScapeGoat(currentFen, move.TargetSquare, !board.IsWhiteToMove).GetLegalMoves()
                            .Count(m => m.TargetSquare.Index == move.TargetSquare.Index) * 10;

            board.MakeMove(move);

            if (board.IsInCheckmate())
            {
                return move;
            }

            moveEvals[i] = value += CalculateOpponentResponse(board, move.TargetSquare) + CalculateFuturePlans(board, move.TargetSquare);
            maxValue = Math.Max(maxValue, value);
            board.UndoMove(move);
        }

        //pick a random top move
        return moves.Where((move, i) => moveEvals[i] == maxValue).OrderBy(_ => Guid.NewGuid()).First();
    }

    // ***Assumes the move has already been made on the board***
    // Calculates all the dangers the opponent can create on their turn
    private int CalculateOpponentResponse(Board board, Square currentSquare)
    {
        int opponentThreats = 0;
        //calculate opponent's control of the square
        foreach (Move move in board.GetLegalMoves())
        {
            //opponent piece puts pressure on the square
            if (move.TargetSquare == currentSquare)
            {
                opponentThreats += 10;
            }

            board.MakeMove(move);
            opponentThreats += board.IsInCheckmate() ? 1000 : 0;
            board.UndoMove(move);
        }

        return -opponentThreats;
    }

    // ***Assumes the move has already been made on the board***
    // This is where we skip our opponents next turn to see how our move affects us
    private int CalculateFuturePlans(Board board, Square currentSquare)
    {
        board.ForceSkipTurn();

        //2 symbols less to do it like this
        int totalConsequence = board.GetLegalMoves()
            .Where(threat => threat.StartSquare == currentSquare)
            .Sum(threat => Material(threat.CapturePieceType));

        //find all the pieces we defend now
        string hypotheticalFen = board.GetFenString();
        PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();

        foreach (PieceList pieceList in pieceLists)
        {
            foreach (Piece p in pieceList.Where(p => !p.IsKing && !(p.Square == currentSquare)))
            {
                foreach (Move defence in ScapeGoat(hypotheticalFen, p.Square, !board.IsWhiteToMove).GetLegalMoves())
                {
                    if (defence.StartSquare.Index == currentSquare.Index && defence.TargetSquare.Index == p.Square.Index)
                    {
                        totalConsequence += Material(p.PieceType);
                    }
                }
            }
        }

        board.UndoSkipTurn();

        return totalConsequence;
    }




}
