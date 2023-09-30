using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ChessChallenge.API;


//MARCH Direct Implementation 
//From this paper: https://link.springer.com/chapter/10.1007/BFb0027053
public class March : IChessBot
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
        int maxValue = int.MinValue;
        string currentFen = board.GetFenString();

        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];

            //we relinquish control of the square we move to and risk our Material (I added Material of capture piece and promotion)
            int value = -1 * Material(move.MovePieceType) //subtract material risk - material cost
                        + 2 * Material(move.CapturePieceType) //gain captured material, if there is any - material cost
                        + ((move.MovePieceType == PieceType.Pawn) ?
                            (board.IsWhiteToMove ? move.TargetSquare.Rank : 7 - move.TargetSquare.Rank) //encourage pawns to promote
                            : -10) //subtract relinquished control
                        + ScapeGoat(currentFen, move.TargetSquare, !board.IsWhiteToMove).GetLegalMoves()
                            .Count(m => m.TargetSquare.Index == move.TargetSquare.Index) * 10;  //count the number of defenders - our control

            board.MakeMove(move);

            if (board.IsInCheckmate()) //found winning move
            {
                board.UndoMove(move);
                return move;
            }

            value -= CalculateOpponentResponse(board, move.TargetSquare); //find the number of attackers - opponent control
            value += CalculateFuturePlans(board, move.TargetSquare); //new picese we defend and attack

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
    private int CalculateFuturePlans(Board board, Square currentSquare)
    {
        board.ForceSkipTurn();

        //2 symbols less to do it like this
        //new threats we create
        int totalConsequence = board.GetLegalMoves()
            .Where(threat => threat.StartSquare == currentSquare)
            .Sum(threat => Material(threat.CapturePieceType));

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
                        totalConsequence += Material(p.PieceType);
                    }
                }
            }
        }

        board.UndoSkipTurn();

        return totalConsequence;
    }
}
