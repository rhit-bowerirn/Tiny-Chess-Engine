using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChessChallenge.API;


public class MyBot : IChessBot
{

    Func<PieceType, int> Material = Type => new int[] { 0, 1, 3, 3, 5, 9, 10 }[(int)Type];

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
        bool isWhite = board.IsWhiteToMove;


        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];

            //we relinquish control of the square we move to and risk our Material (I added Material of capture piece and promotion)
            int value = -1 * Material(move.MovePieceType) + 2 * Material(move.CapturePieceType);
            if (move.MovePieceType == PieceType.Pawn)
            {
                if (isWhite)
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
            Board attackChecker = ScapeGoat(currentFen, move.TargetSquare, !isWhite);
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

            value += CalculateFuturePlans(board, move);

            board.UndoMove(move);
            moveEvals[i] = value;
            if (value > maxValue)
            {
                maxValue = value;
            }
        }

        //pick a random top move
        return moves.Where((move, i) => moveEvals[i] == maxValue).OrderBy(_ => Guid.NewGuid()).First();
    }




    // ***Assumes the move has already been made on the board***
    // This is where we skip our opponents next turn to see how our move affects us
    private int CalculateFuturePlans(Board board, Move move)
    {
        Square currentSquare = move.TargetSquare;
        board.ForceSkipTurn();

        int totalConsequence = board.GetLegalMoves()
            .Where(threat => threat.StartSquare == currentSquare)
            .Sum(threat => Material(threat.CapturePieceType));

        //find all the pieces we defend now
        //TODO: clean up
        string hypotheticalFen = board.GetFenString();
        PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();
        foreach (PieceList pieceList in pieceLists)
        {
            for (int i = 0; i < pieceList.Count; i++)
            {
                Piece p = pieceList.GetPiece(i);
                if (!p.IsKing && !(p.Square == currentSquare))
                {
                    Board defenseChecker = ScapeGoat(hypotheticalFen, p.Square, !p.IsWhite);
                    foreach (Move defence in defenseChecker.GetLegalMoves())
                    {
                        if (defence.StartSquare.Index == currentSquare.Index && defence.TargetSquare.Index == p.Square.Index)
                        {
                            totalConsequence += Material(p.PieceType);
                        }
                    }
                }
            }
        }

        board.UndoSkipTurn();

        return totalConsequence;
    }




}
