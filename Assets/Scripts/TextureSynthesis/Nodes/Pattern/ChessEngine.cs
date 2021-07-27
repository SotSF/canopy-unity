using System;
using System.Collections.Generic;
using System.Linq;

namespace Chess
{

    public enum Color
    {
        Black,
        White
    }

    public enum PieceType
    {
        Pawn,
        Knight,
        Bishop,
        Rook,
        Queen,
        King
    }

    public struct Coord
    {
        public int r;
        public int c;
        public Coord(int r, int c) { this.r = r; this.c = c; }
        public Coord((int,int) rc) { r = rc.Item1; c = rc.Item2; }
        public bool onBoard()
        {
            return r >= 0 && r < 8 && c >= 0 && c < 8;
        }
        public static Coord operator +(Coord a, (int, int) b)
        {
            return new Coord(a.r + b.Item1, a.c + b.Item2);
        }
        public static implicit operator Coord((int,int) rc)
        {
            return new Coord(rc.Item1, rc.Item2);
        }
        public static bool operator ==(Coord a, Coord b)
        {
            return a.r == b.r && a.c == b.c;
        }
        public static bool operator !=(Coord a, Coord b)
        {
            return a.r != b.r || a.c != b.c;
        }
    }

    public struct Piece
    {
        public Color color;
        public PieceType type;
        public static bool operator ==(Piece a, Piece b)
        {
            return a.color == b.color && a.type == b.type;
        }
        public static bool operator !=(Piece a, Piece b)
        {
            return a.color != b.color || a.type != b.type;
        }
    }

    public struct BoardSpace
    {
        public int r;
        public int c;
        public Color color;
        public Piece? piece;
        public Coord coord => new Coord(r, c);
    }

    public class BoardState
    {
        private static PieceType[] backrowOrder = { 
            PieceType.Rook, 
            PieceType.Knight, 
            PieceType.Bishop, 
            PieceType.Queen, 
            PieceType.King, 
            PieceType.Bishop, 
            PieceType.Knight, 
            PieceType.Rook 
        };
        public BoardSpace[][] state;

        // Blank
        public BoardState()
        {
            state = new BoardSpace[8][];
            var color = Color.Black;
            // Initialize the board
            for (int r = 0; r < 8; r++)
            {
                var rank = new BoardSpace[8];
                for (int c = 0; c < 8; c++)
                {
                    var space = new BoardSpace() { r = r, c = c, color = color };
                    if (r == 0) // White back row
                        space.piece = new Piece() { color = Color.White, type = backrowOrder[c] };
                    if (r == 1) // White pawn row
                        space.piece = new Piece() { color = Color.White, type = PieceType.Pawn };
                    if (r == 6) // Black pawn row
                        space.piece = new Piece() { color = Color.Black, type = PieceType.Pawn };
                    if (r == 7) // Black back row
                        space.piece = new Piece() { color = Color.Black, type = backrowOrder[c] };
                    rank[c] = space;
                    color = color == Color.Black ? Color.White : Color.Black;
                }
                color = color == Color.Black ? Color.White : Color.Black;
                state[r] = rank;
            }
        }

        // Copy
        public BoardState(BoardState previous)
        {
            state = new BoardSpace[8][];
            for (int r = 0; r < 8; r++)
            {
                var rank = new BoardSpace[8];
                for (int c = 0; c < 8; c++)
                {
                    rank[c] = previous.state[r][c];
                }
                state[r] = rank;
            }
        }

        public void MovePiece(Coord source, Coord target)
        {
            state[target.r][target.c].piece = state[source.r][source.c].piece;
            state[source.r][source.c].piece = null;
        }

        public BoardSpace Space(int r, int c)
        {
            return state[r][c];
        }

        public BoardSpace Space(Coord coord)
        {
            return state[coord.r][coord.c];
        }

        public Piece? PieceAt(int r, int c)
        {
            return state[r][c].piece;
        }

        public Piece? PieceAt(Coord coord)
        {
            return Space(coord).piece;
        }

        public BoardSpace KingPosition(Color? color)
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (state[r][c].piece?.type == PieceType.King && state[r][c].piece?.color == color)
                    {
                        return state[r][c];
                    }
                }
            }
            // Should never happen
            return state[0][0];
        }
    }

    public class ChessEngine
    {
        private List<BoardState> history;
        public BoardState board
        {
            get 
            {
                return history.Last();
            }
        }

        private (int, int)[] knightMoves = { (1, 2), (2, 1), (1, -2), (2, -1), (-1, 2), (-2, 1), (-1, -2), (-2, -1) };
        private MoveType[] diagonalMoves;
        private MoveType[] straightMoves;
        private MoveType[] omniMoves;
        private Dictionary<PieceType?, MoveCandidateGenerator> moveCandidatesForPiece;
        public Color toMove;
        public ChessEngine()
        {
            history = new List<BoardState>();
            diagonalMoves = new MoveType[] { UpRight, UpLeft, DownRight, DownLeft };
            straightMoves = new MoveType[] { Up, Right, Left, Down };
            moveCandidatesForPiece = new Dictionary<PieceType?, MoveCandidateGenerator>()
            {
                {PieceType.Pawn, PawnCandidates },
                {PieceType.Knight, KnightCandidates },
                {PieceType.Bishop, BishopCandidates },
                {PieceType.Rook, RookCandidates },
                {PieceType.Queen, QueenCandidates },
                {PieceType.King, KingCandidates }
            };
            omniMoves = diagonalMoves.Concat(straightMoves).ToArray();
            history.Add(new BoardState());
            toMove = Color.White;
        }
        public void Reset()
        {
            history.Clear();
            history.Add(new BoardState());
            toMove = Color.White;
        }
        public bool IsWhite(Piece? piece)
        {
            return piece?.color == Color.White;
        }
        public Color EnemyColor(Color? color)
        {
            return color == Color.White ? Color.Black : Color.White;
        }

        public bool PieceHasMoved(BoardSpace target)
        {
            return history.Any(pastState => pastState.PieceAt(target.coord) != target.piece);
        }

        public IEnumerable<(BoardSpace, BoardState)> MovesForSpace(Coord coord)
        {
            var piece = board.PieceAt(coord);
            if (piece?.color != toMove)
                yield break;
            var boardSpace = board.Space(coord);
            foreach(var (space, result) in moveCandidatesForPiece[piece?.type](boardSpace)){
                if (!SpaceThreatenedBy(result.KingPosition(piece?.color), EnemyColor(piece?.color), result))
                    yield return (space, result);
            }
        }

        public bool TakeMove((BoardSpace, BoardState) move)
        {
            history.Add(move.Item2);
            toMove = EnemyColor(toMove);
            // Check for promotion here
            var promotion = false;
            return promotion;
        }

        /* Standard movement is done by yielding coordinates directionally such 
         * that iteration can be stopped upon hitting a blocking piece
         */
        private delegate IEnumerable<Coord> MoveType(int r, int c, int d=8);
        #region Straight movement
        public IEnumerable<Coord> Left(int r, int c, int distance=8)
        {
            for (int i = c-1, d = 0; i >= 0 && d < distance; i--, d++)
            {
                yield return new Coord(r, i);
            }
        }
        public IEnumerable<Coord> Right(int r, int c, int distance=8)
        {
            for (int i = c+1, d = 0; i < 8 && d < distance; i++, d++)
            {
                yield return new Coord(r, i);
            }
        }
        public IEnumerable<Coord> Up(int r, int c, int distance=8)
        {
            for (int i = r+1, d = 0; i < 8 && d < distance; i++, d++)
            {
                yield return new Coord(i, c);
            }
        }
        public IEnumerable<Coord> Down(int r, int c, int distance = 8)
        {
            for (int i = r-1, d = 0; i >= 0 && d < distance; i--, d++)
            {
                yield return new Coord(i, c);
            }
        }
        #endregion

        #region Diagonal movement
        public IEnumerable<Coord> UpLeft(int r, int c, int distance = 8)
        {
            for (int d = 1; d <= distance; d++)
            {
                Coord target = new Coord(r + d, c - d);
                if (!target.onBoard()) break;
                yield return target;
            }
        }
        public IEnumerable<Coord> UpRight(int r, int c, int distance = 8)
        {
            for (int d = 1; d <= distance; d++)
            {
                Coord target = new Coord(r + d, c + d);
                if (!target.onBoard()) break;
                yield return target;
            }
        }
        public IEnumerable<Coord> DownLeft(int r, int c, int distance = 8)
        {
            for (int d = 1; d <= distance; d++)
            {
                Coord target = new Coord(r - d, c - d);
                if (!target.onBoard()) break;
                yield return target;
            }
        }
        public IEnumerable<Coord> DownRight(int r, int c, int distance = 8)
        {
            for (int d = 1; d <= distance; d++)
            {
                Coord target = new Coord(r - d, c + d);
                if (!target.onBoard()) break;
                yield return target;
            }
        }
        #endregion

        /* Move candidates are potential moves as defined by the piece's movement type
         * with special rules for castling / en-passant / not moving the king into check. 
         * Movement types (the methods defined above) are aggregated into lists and each evaluated
         * 
         * Checking for whether a move results in the king being in check is done afterwards.
         */
        public delegate IEnumerable<(BoardSpace, BoardState)> MoveCandidateGenerator(BoardSpace source, BoardState state = null);
        public IEnumerable<(BoardSpace,BoardState)> KnightCandidates(BoardSpace source, BoardState state = null)
        {

            foreach (var offset in knightMoves)
            {
                var target = source.coord + offset;
                if (target.onBoard())
                {
                    BoardSpace targetSpace = board.Space(target);
                    if (targetSpace.piece?.color != source.piece?.color)
                    {
                        var result = new BoardState(board);
                        result.MovePiece(source.coord, target);
                        yield return (targetSpace, result);
                    }
                }
            }
        }
        public IEnumerable<(BoardSpace,BoardState)> PawnCandidates(BoardSpace source, BoardState state = null)
        {
            // Diagonal attack
            var isWhite = IsWhite(source.piece);
            var enemyColor = EnemyColor(source.piece?.color);
            var diagonals = isWhite ? new MoveType[] { UpRight, UpLeft } : new MoveType[] { DownRight, DownLeft };
            foreach (MoveType diagonalAttack in diagonals)
            {
                foreach (Coord targetCoord in diagonalAttack(source.r, source.c, 1))
                {
                    // Standard diagonal attack
                    var targetSpace = board.Space(targetCoord);
                    if (targetSpace.piece != null && targetSpace.piece?.color == enemyColor)
                    {
                        var result = new BoardState(board);
                        result.MovePiece(source.coord, targetCoord);
                        yield return (targetSpace, result);
                    }

                    // En-passant
                    var enPassantRank = isWhite ? 4 : 3;
                    if (source.r == enPassantRank && history.Count > 1 && targetSpace.piece == null)
                    {
                        var lastTurn = history[history.Count - 2];
                        var targetPawnOriginLocation = new Coord(isWhite ? 6 : 1, targetCoord.c);
                        var potentialOriginPawn = lastTurn.PieceAt(targetPawnOriginLocation);

                        var targetPawnCurrentLocation = new Coord(enPassantRank, targetCoord.c);
                        var potentialTargetPawn = board.PieceAt(targetPawnCurrentLocation);
                        // last turn there was a pawn on the starting rank and now there isn't, and there is a pawn beside us
                        if (potentialOriginPawn?.type == PieceType.Pawn && potentialOriginPawn?.color == enemyColor &&
                            potentialTargetPawn?.type == PieceType.Pawn && potentialTargetPawn?.color == enemyColor &&
                            board.PieceAt(targetPawnOriginLocation) == null)
                        {
                            var result = new BoardState(board);
                            result.MovePiece(source.coord, targetCoord);
                            result.state[targetPawnCurrentLocation.r][targetPawnCurrentLocation.c].piece = null;
                            yield return (targetSpace, result);
                        }
                    }
                }
            }
            // Straight move
            MoveType straightMove = isWhite ? (MoveType)Up : (MoveType)Down;
            var startingRank = isWhite ? source.r == 1 : source.r == 6;
            var straightDistance = startingRank ? 2 : 1;
            foreach (Coord targetCoord in straightMove(source.r, source.c, straightDistance))
            {
                var targetSpace = board.Space(targetCoord);
                if (targetSpace.piece == null)
                {
                    var result = new BoardState(board);
                    result.MovePiece(source.coord, targetCoord);
                    yield return (targetSpace, result);
                } else
                {
                    yield break;
                }
            }
        }
        private IEnumerable<(BoardSpace, BoardState)> EvaluateDirections(MoveType[] directions, BoardSpace source, int d = 8, BoardState state = null)
        {
            if (state == null)
                state = board;
            foreach (var direction in directions)
            {
                foreach (Coord targetCoord in direction(source.r, source.c, d))
                {
                    var targetSpace = state.Space(targetCoord);
                    // If I hit my own piece, stop iteration
                    if (targetSpace.piece?.color == source.piece?.color)
                        break;

                    // Otherwise, yield the next space
                    var result = new BoardState(state);
                    result.MovePiece(source.coord, targetCoord);
                    yield return (targetSpace, result);

                    // If I hit an opponent, stop iteration
                    if (targetSpace.piece != null)
                        break;
                }
            }
        }
        public IEnumerable<(BoardSpace, BoardState)> BishopCandidates(BoardSpace source, BoardState state = null)
        {
            foreach (var move in EvaluateDirections(diagonalMoves, source, 8, state)){
                yield return move;
            }
        }
        public IEnumerable<(BoardSpace, BoardState)> RookCandidates(BoardSpace source, BoardState state = null)
        {
            foreach (var move in EvaluateDirections(straightMoves, source, 8, state)){
                yield return move;
            }
        }
        public IEnumerable<(BoardSpace, BoardState)> QueenCandidates(BoardSpace source, BoardState state = null)
        {
            foreach (var move in EvaluateDirections(omniMoves, source)){
                yield return move;
            }
        }
        public IEnumerable<(BoardSpace, BoardState)> KingCandidates(BoardSpace source, BoardState state = null)
        {
            Color enemy = EnemyColor(source.piece?.color);
            foreach (var (space, result) in EvaluateDirections(omniMoves, source, 1))
            {
                if (!SpaceThreatenedBy(space, enemy))
                    yield return (space,result);
            }
            foreach (var (space, result) in CastlingCandidates(source))
            {
                if (!SpaceThreatenedBy(result.KingPosition(source.piece?.color), EnemyColor(source.piece?.color), result))
                    yield return (space, result);
            }
        }
        public IEnumerable<(BoardSpace, BoardState)> CastlingCandidates(BoardSpace king)
        {
            if (PieceHasMoved(king))
                yield break;
            Color enemyColor = EnemyColor(king.piece?.color);
            var rank = IsWhite(king.piece) ? 0 : 7;
            List<BoardSpace> qSidePath = new List<BoardSpace>() { board.Space(rank, 1), board.Space(rank, 2), board.Space(rank, 3) };
            List<BoardSpace> kSidePath = new List<BoardSpace>() { board.Space(rank, 5), board.Space(rank, 6) };
            Func<List<BoardSpace>, bool> pathClear = (castlePath) =>
            {
                foreach (BoardSpace pathSpace in castlePath)
                {
                    if (pathSpace.piece != null)
                        return false;
                    if (SpaceThreatenedBy(pathSpace, enemyColor))
                        return false;
                }
                return true;
            };
            BoardSpace qSideRook = board.Space(rank, 0);
            BoardSpace kSideRook = board.Space(rank, 7);
            if (pathClear(qSidePath) && !PieceHasMoved(qSideRook))
            {
                var result = new BoardState(board);
                result.MovePiece(qSideRook.coord, (rank, 2));
                result.MovePiece(king.coord, (rank, 3));
                yield return (qSideRook, result);
            }
            if (pathClear(kSidePath) && !PieceHasMoved(kSideRook))
            {
                var result = new BoardState(board);
                result.MovePiece(kSideRook.coord, (rank, 5));
                result.MovePiece(king.coord, (rank, 6));
                yield return (kSideRook, result);
            }
        }

        public bool SpaceThreatenedBy(BoardSpace source, Color threatColor, BoardState state = null)
        {
            if (state == null)
                state = board;
            // Special types which don't use "inverted" move candidates first
            // Knight threats
            foreach (var offset in knightMoves)
            {
                var coord = source.coord + offset;
                if (coord.onBoard())
                {
                    var potentialKnight = state.PieceAt(coord);
                    if(potentialKnight?.type == PieceType.Knight && potentialKnight?.color == threatColor)
                        return true;
                }
            }
            // King threats
            foreach (MoveType movement in omniMoves)
            {
                foreach (var space in movement(source.r, source.c, 1))
                {
                    var threat = state.PieceAt(space);
                    if (threat?.type == PieceType.King && threat?.color == threatColor)
                        return true;
                }
            }
            // Pawn threats
            if (threatColor == Color.White)
            {
                var dl = source.coord + (-1, -1);
                var dr = source.coord + (-1, 1);
                if (dl.onBoard())
                {
                    if (state.PieceAt(dl)?.type == PieceType.Pawn && state.PieceAt(dl)?.color == threatColor)
                        return true;
                }
                if (dr.onBoard())
                {
                    if (state.PieceAt(dr)?.type == PieceType.Pawn && state.PieceAt(dr)?.color == threatColor)
                        return true;
                }
            } 
            else
            {
                var ul = source.coord + (1, -1);
                var ur = source.coord + (1, 1);
                if (ul.onBoard())
                {
                    if (state.PieceAt(ul)?.type == PieceType.Pawn && state.PieceAt(ul)?.color == threatColor)
                        return true;
                }
                if (ur.onBoard())
                {
                    if (state.PieceAt(ur)?.type == PieceType.Pawn && state.PieceAt(ur)?.color == threatColor)
                        return true;
                }
            }

            // "Standard" threats for bishop / rook / queen - use a fake piece of the threatened color
            // and project it outwards to see if it could capture a real threatening piece of that movement type
            var fakePiece = new BoardSpace() { r = source.r, c = source.c, color = EnemyColor(threatColor), piece = new Piece() { type = PieceType.King, color = EnemyColor(threatColor) } };
            var bcands = BishopCandidates(fakePiece, state).ToList();
            foreach (var (space, _) in bcands)
            {
                if ((space.piece?.type == PieceType.Bishop || space.piece?.type == PieceType.Queen) &&
                    space.piece?.color == threatColor)
                    return true;
            }
            foreach (var (space, _) in RookCandidates(fakePiece, state))
            {
                if ((space.piece?.type == PieceType.Rook || space.piece?.type == PieceType.Queen) &&
                    space.piece?.color == threatColor)
                    return true;
            }
            return false;
        }
    }
}
