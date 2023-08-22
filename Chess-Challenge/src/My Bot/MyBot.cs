using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChessChallenge.API;

public class MyBot : IChessBot
{

    Func<PieceType, int> Material = Type => new int[] { 0, 1, 3, 3, 5, 9, 10 }[(int)Type];

    // how much to add for each of our pieces defending the new square.
    const double NEW_DEFENDERS = 10.0; // default is 10

    // how much to subtract for each of their pieces attacking the new square.
    const double NEW_ATTACKERS = 10.0; // default is 10

    // multiplier weight for the material weight of pieces we start defending
    const double NEW_DEFENSE = 1.0; // default is 1

    // multiplier weight for the material weight of pieces we start attacking
    const double NEW_ATTACKS = 2.0; // default is 1

    // multiplier weight for the number of squares we can now move to
    const double NEW_MOVES = 1.0; // default is 1

    // how much we subtract for giving up control of a square
    const double RELINQUISHED_CONTROL = 10.0; // default is 10

    // multiplier weight for the material weight of piece we are risking
    const double RISK_WEIGHT = 1.0; // default is 1

    // multiplier weight for the material weight of piece we are capturing
    const double CAPTURE_WEIGHT = 3.0; // default is 1

    // multiplier weight to encourage promotion
    const double PROMOTION_WEIGHT = 1.0; // default is 1

    // multiplier weight for the material weight of pieces we stop defending
    const double OLD_DEFENSE = 1.0; // default is 1

    // multiplier weight for the material weight of pieces we stop attacking
    //NOT IMPLEMENTED YET
    const double OLD_ATTACKS = 1.0; // default is 1

    // how much we add for each of their pieces attacking the old square
    const double OLD_ATTACKERS = 10.0; // default is 10

    // how much we subtract for each of our pieces defending the old square
    //NOT IMPLEMENTED YET
    const double OLD_DEFENDERS = 10.0; // default is 10

    // how much we subtract for each of the squares we used to be able to move to
    //NOT IMPLEMENTED YET
    const double OLD_MOVES = 0.0; // default is 1


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
        double[] moveEvals = new double[moves.Length];
        double maxValue = double.MinValue;
        string currentFen = board.GetFenString();

        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];

            //we relinquish control of the square we move to and risk our Material (I added Material of capture piece and promotion)
            double value = -RISK_WEIGHT * Material(move.MovePieceType) //subtract material risk - material cost
                        + CAPTURE_WEIGHT * Material(move.CapturePieceType) //gain captured material, if there is any - material cost
                        + ((move.MovePieceType == PieceType.Pawn && !move.IsCapture) ?
                            PROMOTION_WEIGHT * (board.IsWhiteToMove ? move.TargetSquare.Rank : 7 - move.TargetSquare.Rank) //encourage pawns to promote
                            : -RELINQUISHED_CONTROL) //subtract relinquished control
                        + NEW_DEFENDERS *  ScapeGoat(currentFen, move.TargetSquare, !board.IsWhiteToMove).GetLegalMoves()
                            .Count(m => m.TargetSquare.Index == move.TargetSquare.Index);  //count the number of defenders - our control



            //Subtract all the material we would stop defending
            PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();
            foreach (PieceList pieceList in pieceLists)
            {
                foreach (Piece p in pieceList.Where(p => !p.IsKing && !(p.Square == move.StartSquare)))
                {
                    foreach (Move defence in ScapeGoat(currentFen, p.Square, !board.IsWhiteToMove).GetLegalMoves())
                    {
                        if (defence.StartSquare.Index == move.StartSquare.Index && defence.TargetSquare.Index == p.Square.Index)
                        {
                            value -= OLD_DEFENSE * Material(p.PieceType);
                        }
                    }
                }
            }
            
            //find how bad it would be to stay in the same square
            if(board.TrySkipTurn()) {
                value +=  OLD_ATTACKERS * board.GetLegalMoves().Where(opponentMove => opponentMove.TargetSquare == move.StartSquare).Count();
                board.UndoSkipTurn();
            }


            board.MakeMove(move);

            if (board.IsInCheckmate()) //found winning move
            {
                board.UndoMove(move);
                return move;
            }

            value -= CalculateOpponentResponse(board, move.TargetSquare); //find the number of attackers - opponent control
            value += CalculateFuturePlans(board, move.TargetSquare); //new pieces we defend and attack

            board.UndoMove(move);

            moveEvals[i] = value;
            maxValue = Math.Max(maxValue, value);
        }

        //pick a random top move
        Move[] topMoves = moves.Where((move, i) => moveEvals[i] == maxValue).ToArray();
        return topMoves[new Random().Next(0, topMoves.Length)];
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
            {
                opponentThreats += NEW_ATTACKERS;
            }

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

        //2 symbols less to do it like this
        //new threats we create
        foreach (Move move in board.GetLegalMoves().Where(threat => threat.StartSquare == currentSquare))
        {
            if (move.IsCapture)
            {
                totalConsequence += NEW_ATTACKS * Material(move.CapturePieceType);
            }
            else totalConsequence += NEW_MOVES;
        }

        //find new pieces we defend
        string fen = board.GetFenString();
        PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();

        foreach (PieceList pieceList in pieceLists)
        {
            foreach (Piece p in pieceList.Where(p => !p.IsKing && !(p.Square == currentSquare)))
            {
                foreach (Move defence in ScapeGoat(fen, p.Square, !board.IsWhiteToMove).GetLegalMoves())
                {
                    if (defence.StartSquare.Index == currentSquare.Index && defence.TargetSquare.Index == p.Square.Index)
                    {
                        totalConsequence += NEW_DEFENSE * Material(p.PieceType);
                    }
                }
            }
        }

        board.UndoSkipTurn();

        return totalConsequence;
    }
}
