using System;
// using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChessChallenge.API;

public class MyBot : IChessBot
{

    Func<PieceType, int> Material = Type => new int[] { 0, 1, 3, 3, 5, 9, 10 }[(int)Type];

    double[] w = new double[] {6.46966253, 4.28272843, 2.45141429, 5.43149072 , 3.40353213 , 2.67461549,
            0.59151037,  6.08796703, 6.80125667, 2.94047109, 6.05383843,
            8.1924716, 3.42285553, 8.0929267, 9.4478703, 3.37456005};

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        double[] moveEvals = moves.Select(move => negamax(board, move, 3, double.MinValue, double.MaxValue)).ToArray();
        Move[] topMoves = moves.Where((move, i) => moveEvals[i] >= moveEvals.Max()).ToArray();
        return topMoves[new Random().Next(0, topMoves.Length)];
    }

    public double negamax(Board board, Move m, int depth,  double a, double b)
    {
        board.MakeMove(m);
        Move[] next = bestMoves(board);
        //reverse to get most promising nodes first
        Array.Reverse(next);

        if (depth == 0 || next.Length == 0)
        {
            board.UndoMove(m);
            return evaluateMove(board, m);
        }
        
        // double val = next.Max(n => -negamax(b, n, depth - 1));
        double val = double.MinValue;
        foreach (Move n in next) {
            val = Math.Max(val, -negamax(board, n, depth - 1, b, a));
            a = Math.Max(a, val);
            if (a >= b)
                break;
        }
        board.UndoMove(m);
        return val;
    }

    private Move[] bestMoves(Board board)
    {
        Move[] moves = board.GetLegalMoves();
        // Console.WriteLine("all: {0}", moves.Length);
        double[] moveEvals = new double[moves.Length];
        double maxValue = double.MinValue;


        for (int i = 0; i < moves.Length; i++)
        {
            moveEvals[i] = evaluateMove(board, moves[i]);
            maxValue = Math.Max(maxValue, moveEvals[i]);
        }

        // needed bc alpha-beta pruning searches through most promising nodes first
        // we'll flip the order in the negamax function, since its hard to reverse both arrays at the same
        Array.Sort(moveEvals, moves);
        return moves.Where((move, i) => moveEvals[i] >= 0.95 * maxValue).ToArray();
    }

    private double evaluateMove(Board board, Move move)
    {
        //we relinquish control of the square we move to and risk our Material (I added Material of capture piece and promotion)
        double value = -w[12] * Material(move.MovePieceType) //subtract material risk - material cost
                    + w[13] * Material(move.CapturePieceType) //gain captured material, if there is any - material cost
                    + ((move.MovePieceType == PieceType.Pawn && !move.IsCapture) ?
                        w[14] * (board.IsWhiteToMove ? move.TargetSquare.Rank : 7 - move.TargetSquare.Rank) //encourage pawns to promote
                        : -w[11]) //subtract relinquished control
                    + w[0] * ScapeGoat(board, move.TargetSquare, !board.IsWhiteToMove)
                        .Count(m => m.TargetSquare.Index == move.TargetSquare.Index) //count the number of defenders - our control
                    - findDefendedPieces(board, move.StartSquare, w[6]);

        if (move.IsCastles)
            value += w[15];

        if (move.IsPromotion)
            value += Material(move.PromotionPieceType);

        foreach (Move m in board.GetLegalMoves())
            if (m.StartSquare == move.StartSquare)
                value -= w[9] + w[7] * Material(m.CapturePieceType);

        if (board.TrySkipTurn())
        {
            foreach (Move threat in board.GetLegalMoves())
            {

                //find how much pressure is currently on our piece
                if (threat.TargetSquare == move.StartSquare)
                    value += w[8];

                //find how much pressure our opponents have on all our pieces before we move
                value += w[10] * Material(threat.CapturePieceType);
            }
            board.UndoSkipTurn();
        }


        board.MakeMove(move);

        if (board.IsInCheckmate()) //found winning move
        {
            board.UndoMove(move);
            return double.MaxValue;
        }

        // value += CalculateFuture(board, move.TargetSquare);
        // Combined CalculateFuturePlans and CalculateOpponentResponse here to save tokens
        Square currentSquare = move.TargetSquare;

        // This is where we skip our opponents next turn to see how our move affects us
        board.ForceSkipTurn();
        //new threats we create
        foreach (Move m in board.GetLegalMoves())
        {
            if (m.StartSquare == currentSquare && m.IsCapture)
                value += w[3] * Material(m.CapturePieceType);

            value += w[4];
        }
        value += findDefendedPieces(board, currentSquare, w[2]);
        board.UndoSkipTurn();

        // Calculates all the dangers the opponent can create on their turn
        foreach (Move m in board.GetLegalMoves())
        {
            //opponent piece puts pressure on the square
            if (m.TargetSquare == currentSquare)
                value -= w[1];

            //find how much pressure our opponents have on all our pieces after we move
            value -= w[5] * Material(m.CapturePieceType);

            board.MakeMove(m);
            if (board.IsInCheckmate())
            { 
                value -= 1000;
                board.UndoMove(m);
                break;
            }
            board.UndoMove(m);
        }

        board.UndoMove(move);
        
        return value;
    }

    // ScapeGoat takes the current board and puts a bishop on a given square since we can have 10 bishops but 8 pawns
    // This is used to find all the pieces that control a given square
    // En passant doesn't need to be accounted for here
    private Move[] ScapeGoat(Board board, Square square, bool pieceIsWhite)
    {
        string[] rows = board.GetFenString().Split('/');
        StringBuilder newRow = new(Regex.Replace(rows[7 - square.Rank], @"\d+", match => new string('1', int.Parse(match.Value))));
        newRow[square.File] = pieceIsWhite ? 'B' : 'b';
        rows[7 - square.Rank] = Regex.Replace(newRow.ToString(), "1+", match => match.Value.Length.ToString());
        return Board.CreateBoardFromFEN(string.Join("/", rows)).GetLegalMoves();
    }

    private double findDefendedPieces(Board board, Square currentSquare, double multiplier)
    {
        double sum = 0;
        PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();

        foreach (PieceList pieceList in pieceLists)
            foreach (Piece p in pieceList)
                if (!(p.IsKing || p.Square == currentSquare))
                    foreach (Move defence in ScapeGoat(board, p.Square, !board.IsWhiteToMove))
                        if (defence.StartSquare.Index == currentSquare.Index && defence.TargetSquare.Index == p.Square.Index)
                            sum += multiplier * Material(p.PieceType);

        return sum;
    }
}
