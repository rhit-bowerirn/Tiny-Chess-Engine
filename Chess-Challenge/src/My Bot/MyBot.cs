using System;
using System.Collections.Generic;
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
    

    // // how much to multiply for each of our pieces defending the new square.
    // const double NEW_DEFENDERS = 64.6966253; // default is 10

    // // how much to multiply for each of their pieces attacking the new square.
    // const double NEW_ATTACKERS = 42.8272843; // default is 10

    // // multiplier weight for the material weight of pieces we start defending
    // const double NEW_DEFENSE = 2.45141429; // default is 1

    // // multiplier weight for the material weight of pieces we start attacking
    // const double NEW_ATTACKS = 5.43149072; // default is 1

    // // multiplier weight for the number of squares we can now move to
    // const double NEW_MOVES = 3.40353213; // default is 1

    // // how much we subtract for giving up control of a square
    // const double RELINQUISHED_CONTROL = 81.924716; // default is 10

    // // multiplier weight for the material weight of piece we are risking
    // const double RISK_WEIGHT = 3.42285553; // default is 1

    // // multiplier weight for the material weight of piece we are capturing
    // const double CAPTURE_WEIGHT = 8.0929267; // default is 1

    // // multiplier weight to encourage promotion
    // const double PROMOTION_WEIGHT = 9.4478703; // default is 1

    // // multiplier weight for the material weight of pieces we stop defending
    // const double OLD_DEFENSE = 0.59151037; // default is 1

    // // multiplier weight for the material weight of pieces we stop attacking
    // //NOT IMPLEMENTED YET
    // const double OLD_ATTACKS = 6.08796703; // default is 1

    // // how much we multiply for each of their pieces attacking the old square
    // const double OLD_ATTACKERS = 68.0125667; // default is 10

    // // how much we subtract for each of the squares we used to be able to move to
    // const double OLD_MOVES = 2.94047109; // default is 1

    // // multiplier for captures our opponent can currently make
    // const double OLD_THREATS = 6.05383843;

    // // multiplier for the captures our opponent will be able to make
    // const double NEW_THREATS = 2.67461549;

    // // weight to encourage castling
    // const double CASTLE_BONUS = 3.37456005; // default is 1;

    public Move Think(Board board, Timer timer)
    {
        Move[] moves = board.GetLegalMoves();
        double[] moveEvals = moves.Select(move => negamax(board, move, 3)).ToArray();
        Move[] topMoves = moves.Where((move, i) => moveEvals[i] >= moveEvals.Max()).ToArray();
        return topMoves[new Random().Next(0, topMoves.Length)];
    }

    public double negamax(Board b, Move m, int depth)
    {
        b.MakeMove(m);
        Move[] next = bestMoves(b);

        if (depth == 0 || next.Length == 0)
        {
            b.UndoMove(m);
            return evaluateMove(b, m);
        }
        
        double val = next.Max(n => -negamax(b, n, depth - 1));
        b.UndoMove(m);
        return val;
    }

    private Move[] bestMoves(Board board)
    {
        Move[] moves = board.GetLegalMoves();
        double[] moveEvals = new double[moves.Length];
        double maxValue = double.MinValue;


        for (int i = 0; i < moves.Length; i++)
        {
            moveEvals[i] = evaluateMove(board, moves[i]);
            maxValue = Math.Max(maxValue, moveEvals[i]);
        }

        //return moves
        return moves.Where((move, i) => moveEvals[i] >= 0.98 * maxValue).ToArray();
    }

    private double evaluateMove(Board board, Move move)
    {
        //we relinquish control of the square we move to and risk our Material (I added Material of capture piece and promotion)
        double value = -w[12] * Material(move.MovePieceType) //subtract material risk - material cost
                    + w[13] * Material(move.CapturePieceType) //gain captured material, if there is any - material cost
                    + ((move.MovePieceType == PieceType.Pawn && !move.IsCapture) ?
                        w[14] * (board.IsWhiteToMove ? move.TargetSquare.Rank : 7 - move.TargetSquare.Rank) //encourage pawns to promote
                        : -w[11]) //subtract relinquished control
                    + w[0] * ScapeGoat(board, move.TargetSquare, !board.IsWhiteToMove).GetLegalMoves()
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

        value += CalculateFuturePlans(board, move.TargetSquare) - CalculateOpponentResponse(board, move.TargetSquare);

        board.UndoMove(move);
        
        return value;
    }

    // ScapeGoat takes the current board and puts a bishop on a given square since we can have 10 bishops but 8 pawns
    // This is used to find all the pieces that control a given square
    // En passant doesn't need to be accounted for here
    private Board ScapeGoat(Board board, Square square, bool pieceIsWhite)
    {
        string[] rows = board.GetFenString().Split('/');
        StringBuilder newRow = new(Regex.Replace(rows[7 - square.Rank], @"\d+", match => new string('1', int.Parse(match.Value))));
        newRow[square.File] = pieceIsWhite ? 'B' : 'b';
        rows[7 - square.Rank] = Regex.Replace(newRow.ToString(), "1+", match => match.Value.Length.ToString());
        return Board.CreateBoardFromFEN(string.Join("/", rows));
    }

    // ***Assumes the move has already been made on the board***
    // Calculates all the dangers the opponent can create on their turn
    private double CalculateOpponentResponse(Board board, Square currentSquare)
    {
        double opponentThreats = 0;
        //calculate opponent's control of the square
        foreach (Move move in board.GetLegalMoves())
        {
            //opponent piece puts pressure on the square
            if (move.TargetSquare == currentSquare)
                opponentThreats += w[1];

            //find how much pressure our opponents have on all our pieces after we move
            opponentThreats += w[5] * Material(move.CapturePieceType);

            board.MakeMove(move);
            if (board.IsInCheckmate())
            { 
                opponentThreats += 1000;
                board.UndoMove(move);
                break;
            }
            board.UndoMove(move);
        }

        return opponentThreats;
    }

    // ***Assumes the move has already been made on the board***
    // This is where we skip our opponents next turn to see how our move affects us
    private double CalculateFuturePlans(Board board, Square currentSquare)
    {
        board.ForceSkipTurn();
        double totalConsequence = 0;

        //new threats we create
        foreach (Move move in board.GetLegalMoves())
        {
            if (move.StartSquare == currentSquare && move.IsCapture)
                totalConsequence += w[3] * Material(move.CapturePieceType);

            totalConsequence += w[4];
        }

        //find new pieces we defend
        // string fen = board.GetFenString();
        // PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();

        totalConsequence += findDefendedPieces(board, currentSquare, w[2]);

        board.UndoSkipTurn();

        return totalConsequence;
    }

    private double findDefendedPieces(Board board, Square currentSquare, double multiplier)
    {
        double sum = 0;
        PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();

        foreach (PieceList pieceList in pieceLists)
            foreach (Piece p in pieceList)
                if (!(p.IsKing || p.Square == currentSquare))
                    foreach (Move defence in ScapeGoat(board, p.Square, !board.IsWhiteToMove).GetLegalMoves())
                        if (defence.StartSquare.Index == currentSquare.Index && defence.TargetSquare.Index == p.Square.Index)
                            sum += multiplier * Material(p.PieceType);

        return sum;
    }
}
