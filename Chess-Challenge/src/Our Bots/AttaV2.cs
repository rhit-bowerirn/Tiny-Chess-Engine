using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChessChallenge.API;


//PrincessAtta V2
//Atta V1 but taking more factors into consideration
public class AttaV2 : IChessBot
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

    // how much we subtract for each of the squares we used to be able to move to
    const double OLD_MOVES = 1.0; // default is 1

    //multiplier for captures our opponent can currently make
    const double OLD_THREATS = 1.0;

    //multiplier for the captures our opponent will be able to make
    const double NEW_THREATS = 1.0;

    //weight to encourage castling
    const double CASTLE_BONUS = 5.0; //defaiult is 1;

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
                        + NEW_DEFENDERS * ScapeGoat(currentFen, move.TargetSquare, !board.IsWhiteToMove).GetLegalMoves()
                            .Count(m => m.TargetSquare.Index == move.TargetSquare.Index) //count the number of defenders - our control
                        - findDefendedPieces(board, move.StartSquare, OLD_DEFENSE);                            

            if(move.IsCastles) {
                value += CASTLE_BONUS;
            }

            if(move.IsPromotion) {
                value += Material(move.PromotionPieceType);
            }

            foreach (Move m in moves) {
                if(m.StartSquare == move.StartSquare) {
                    value -= OLD_MOVES;
                    value -= OLD_ATTACKS * Material(m.CapturePieceType);
                }
            }

            if (board.TrySkipTurn())
            {
                foreach (Move threat in board.GetLegalMoves()) {

                    //find how much pressure is currently on our piece
                    if(threat.TargetSquare == move.StartSquare) {
                        value += OLD_ATTACKERS;
                    }

                    //find how much pressure our opponents have on all our pieces before we move
                    value += OLD_THREATS * Material(threat.CapturePieceType);
                }
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

            //find how much pressure our opponents have on all our pieces after we move
            opponentThreats += NEW_THREATS * Material(move.CapturePieceType);

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
            {
                totalConsequence += NEW_ATTACKS * Material(move.CapturePieceType);
            }

            totalConsequence += NEW_MOVES;
        }

        //find new pieces we defend
        string fen = board.GetFenString();
        PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();

        totalConsequence += findDefendedPieces(board, currentSquare, NEW_DEFENSE);

        board.UndoSkipTurn();

        return totalConsequence;
    }

    private double findDefendedPieces(Board board, Square currentSquare, double multiplier) {
        double sum = 0;
        string fen = board.GetFenString();
        PieceList[] pieceLists = board.GetAllPieceLists().Where(pl => pl.IsWhitePieceList == board.IsWhiteToMove).ToArray();

        foreach (PieceList pieceList in pieceLists)
        {
            foreach (Piece p in pieceList)
            {
                if (!p.IsKing && !(p.Square == currentSquare))
                {
                    foreach (Move defence in ScapeGoat(fen, p.Square, !board.IsWhiteToMove).GetLegalMoves())
                    {
                        if (defence.StartSquare.Index == currentSquare.Index && defence.TargetSquare.Index == p.Square.Index)
                        {
                            sum += multiplier * Material(p.PieceType);
                        }
                    }
                }
            }
        }

        return sum;
    }
}
