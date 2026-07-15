using ChessGP.Core.Engine;

namespace ChessGP.Core.Eval;

/// <summary>
/// Evaluates chess positions using decoded, tunable evaluation parameters.
/// </summary>
/// <remarks>
/// The returned score is from White's perspective: positive values are better for White,
/// negative values are better for Black.
/// </remarks>
public sealed class ParametricEvaluator : IEvaluator
{
    private const int PawnValue = 100;

    private readonly DecodedParams _parameters;

    /// <summary>
    /// Creates an evaluator from decoded evaluation parameters.
    /// </summary>
    public ParametricEvaluator(DecodedParams parameters)
    {
        _parameters = parameters;
    }

    /// <summary>
    /// Evaluates the current board position in centipawns from White's perspective.
    /// </summary>
    public int Evaluate(IBoardAdapter board)
    {
        var fen = board.ToFen();
        var parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var boardPart = parts[0];
        var sideToMove = parts.Length > 1 ? parts[1] : "w";

        ParseFenBoard(
            boardPart,
            out var grid,
            out var whitePawns,
            out var blackPawns,
            out var whiteBishops,
            out var blackBishops,
            out var whiteKnights,
            out var blackKnights,
            out var whiteRooks,
            out var blackRooks,
            out var whiteQueens,
            out var blackQueens,
            out var whiteKing,
            out var blackKing,
            out var whitePieces,
            out var blackPieces);

        var material =
            (whitePawns.Count - blackPawns.Count) * PawnValue +
            (whiteKnights - blackKnights) * _parameters.KnightValue +
            (whiteBishops - blackBishops) * _parameters.BishopValue +
            (whiteRooks - blackRooks) * _parameters.RookValue +
            (whiteQueens - blackQueens) * _parameters.QueenValue;

        var bishopPair =
            (whiteBishops >= 2 ? _parameters.BishopPairBonus : 0) -
            (blackBishops >= 2 ? _parameters.BishopPairBonus : 0);

        var tempo = sideToMove == "w"
            ? _parameters.TempoBonus
            : -_parameters.TempoBonus;

        var pawnStructure = EvaluatePawnStructure(grid, whitePawns, blackPawns, whiteKing, blackKing);
        var weakSquares = EvaluateWeakSquares(grid, whitePawns, blackPawns);
        var kingSafety = EvaluateKingSafety(grid, whitePawns, blackPawns, whiteKing, blackKing, whitePieces, blackPieces);
        var mobility = EvaluateMobility(grid, whitePieces, blackPieces);
        var rookFeatures = EvaluateRookFeatures(grid, whitePieces, blackPieces, whitePawns, blackPawns, whiteKing, blackKing);
        var knightFeatures = EvaluateKnightFeatures(grid, whitePieces, blackPieces, whitePawns, blackPawns);

        return material +
               bishopPair +
               tempo +
               pawnStructure +
               weakSquares +
               kingSafety +
               mobility +
               rookFeatures +
               knightFeatures;
    }

    // Pawn structure

    private int EvaluatePawnStructure(
        char[,] grid,
        List<(int file, int rank)> whitePawns,
        List<(int file, int rank)> blackPawns,
        (int file, int rank) whiteKing,
        (int file, int rank) blackKing)
    {
        var whiteScore = EvaluatePawnsOneSide(
            grid,
            whitePawns,
            blackPawns,
            isWhite: true,
            enemyKing: blackKing);

        var blackScore = EvaluatePawnsOneSide(
            grid,
            blackPawns,
            whitePawns,
            isWhite: false,
            enemyKing: whiteKing);

        return whiteScore - blackScore;
    }

    private int EvaluatePawnsOneSide(
        char[,] grid,
        List<(int file, int rank)> myPawns,
        List<(int file, int rank)> opponentPawns,
        bool isWhite,
        (int file, int rank) enemyKing)
    {
        var fileCounts = new int[9];

        foreach (var pawn in myPawns)
        {
            fileCounts[pawn.file]++;
        }

        var isPassed = new bool[myPawns.Count];
        for (var index = 0; index < myPawns.Count; index++)
        {
            isPassed[index] = IsPassedPawn(myPawns[index], opponentPawns, isWhite);
        }

        var score = 0;

        for (var file = 1; file <= 8; file++)
        {
            var extraPawns = fileCounts[file] - 1;
            if (extraPawns > 0)
            {
                score -= _parameters.DoubledPawnPenalty * extraPawns;
            }
        }

        for (var index = 0; index < myPawns.Count; index++)
        {
            var pawn = myPawns[index];

            if (IsIsolated(pawn, fileCounts))
            {
                score -= _parameters.IsolatedPawnPenalty;
            }

            if (IsBackwardPawn(grid, pawn, myPawns, opponentPawns, isWhite))
            {
                score -= _parameters.BackwardPawnPenalty;
            }

            if (!isPassed[index])
            {
                continue;
            }

            var advancement = isWhite
                ? pawn.rank - 2
                : 7 - pawn.rank;

            advancement = Math.Clamp(advancement, 0, 5);

            var passedBonus = (_parameters.PassedPawnBonus * (advancement + 1)) / 6;
            score += passedBonus;

            if (IsProtectedByPawn(myPawns, pawn, isWhite))
            {
                score += _parameters.ProtectedPassedPawnBonus;
            }

            if (HasConnectedPassedPawn(myPawns, isPassed, index))
            {
                score += _parameters.ConnectedPassedBonus;
            }

            var promotionRank = isWhite ? 8 : 1;
            var enemyKingDistance = Math.Max(
                Math.Abs(enemyKing.file - pawn.file),
                Math.Abs(enemyKing.rank - promotionRank));

            enemyKingDistance = Math.Clamp(enemyKingDistance, 0, 7);

            var kingDistanceBonus =
                (_parameters.PassedPawnEnemyKingDistBonus * enemyKingDistance) / 7;

            score += kingDistanceBonus;
        }

        return score;
    }

    private static bool IsIsolated((int file, int rank) pawn, int[] fileCounts)
    {
        var file = pawn.file;
        var hasLeftPawn = file > 1 && fileCounts[file - 1] > 0;
        var hasRightPawn = file < 8 && fileCounts[file + 1] > 0;

        return !(hasLeftPawn || hasRightPawn);
    }

    private static bool HasConnectedPassedPawn(
        List<(int file, int rank)> myPawns,
        bool[] isPassed,
        int index)
    {
        var pawn = myPawns[index];

        for (var otherIndex = 0; otherIndex < myPawns.Count; otherIndex++)
        {
            if (otherIndex == index || !isPassed[otherIndex])
            {
                continue;
            }

            var otherPawn = myPawns[otherIndex];

            if (Math.Abs(otherPawn.file - pawn.file) == 1 &&
                Math.Abs(otherPawn.rank - pawn.rank) <= 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPassedPawn(
        (int file, int rank) pawn,
        List<(int file, int rank)> opponentPawns,
        bool isWhite)
    {
        foreach (var opponentPawn in opponentPawns)
        {
            if (Math.Abs(opponentPawn.file - pawn.file) > 1)
            {
                continue;
            }

            if (isWhite && opponentPawn.rank > pawn.rank)
            {
                return false;
            }

            if (!isWhite && opponentPawn.rank < pawn.rank)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBackwardPawn(
        char[,] grid,
        (int file, int rank) pawn,
        List<(int file, int rank)> myPawns,
        List<(int file, int rank)> opponentPawns,
        bool isWhite)
    {
        var hasAdjacentPawn = false;
        var allAdjacentPawnsAreFurtherAdvanced = true;

        foreach (var otherPawn in myPawns)
        {
            if (Math.Abs(otherPawn.file - pawn.file) != 1)
            {
                continue;
            }

            hasAdjacentPawn = true;

            if (isWhite && otherPawn.rank <= pawn.rank)
            {
                allAdjacentPawnsAreFurtherAdvanced = false;
                break;
            }

            if (!isWhite && otherPawn.rank >= pawn.rank)
            {
                allAdjacentPawnsAreFurtherAdvanced = false;
                break;
            }
        }

        if (!hasAdjacentPawn || !allAdjacentPawnsAreFurtherAdvanced)
        {
            return false;
        }

        var frontRank = isWhite
            ? pawn.rank + 1
            : pawn.rank - 1;

        if (frontRank < 1 || frontRank > 8)
        {
            return false;
        }

        if (grid[pawn.file, frontRank] != '\0')
        {
            return false;
        }

        return IsSquareAttackedByEnemyPawn(pawn.file, frontRank, opponentPawns, isWhite);
    }

    private static bool PawnCanReachRankOnFileByPushIgnoringPieces(
        char[,] grid,
        List<(int file, int rank)> pawns,
        int file,
        int targetRank,
        bool pawnIsWhite)
    {
        if (file < 1 || file > 8 || targetRank < 1 || targetRank > 8)
        {
            return false;
        }

        foreach (var pawn in pawns)
        {
            if (pawn.file != file)
            {
                continue;
            }

            if (pawn.rank == targetRank)
            {
                return true;
            }

            if (pawnIsWhite)
            {
                if (pawn.rank > targetRank)
                {
                    continue;
                }

                if (!PathHasPawnBlocker(grid, file, fromRank: pawn.rank + 1, toRank: targetRank))
                {
                    return true;
                }
            }
            else
            {
                if (pawn.rank < targetRank)
                {
                    continue;
                }

                if (!PathHasPawnBlocker(grid, file, fromRank: targetRank, toRank: pawn.rank - 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSquareAttackedByEnemyPawn(
        int file,
        int rank,
        List<(int file, int rank)> enemyPawns,
        bool whiteIsOurSide)
    {
        foreach (var enemyPawn in enemyPawns)
        {
            var attackingRank = whiteIsOurSide
                ? rank + 1
                : rank - 1;

            if (enemyPawn.rank != attackingRank)
            {
                continue;
            }

            if (enemyPawn.file == file - 1 || enemyPawn.file == file + 1)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProtectedByPawn(
        List<(int file, int rank)> myPawns,
        (int file, int rank) pawn,
        bool isWhite)
    {
        return isWhite
            ? HasPawnAt(myPawns, pawn.file - 1, pawn.rank - 1) ||
              HasPawnAt(myPawns, pawn.file + 1, pawn.rank - 1)
            : HasPawnAt(myPawns, pawn.file - 1, pawn.rank + 1) ||
              HasPawnAt(myPawns, pawn.file + 1, pawn.rank + 1);
    }

    // Weak squares

    private int EvaluateWeakSquares(
        char[,] grid,
        List<(int file, int rank)> whitePawns,
        List<(int file, int rank)> blackPawns)
    {
        var whiteWeakSquares = CountWeakSquaresForSide(grid, whitePawns, isWhite: true);
        var blackWeakSquares = CountWeakSquaresForSide(grid, blackPawns, isWhite: false);

        return _parameters.WeakSquarePenalty * (blackWeakSquares - whiteWeakSquares);
    }

    private static int CountWeakSquaresForSide(
        char[,] grid,
        List<(int file, int rank)> myPawns,
        bool isWhite)
    {
        var count = 0;
        var fromRank = isWhite ? 2 : 5;
        var toRank = isWhite ? 4 : 7;

        for (var file = 2; file <= 7; file++)
        {
            for (var rank = fromRank; rank <= toRank; rank++)
            {
                if (IsWeakSquare(grid, myPawns, isWhite, file, rank))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool IsWeakSquare(
        char[,] grid,
        List<(int file, int rank)> myPawns,
        bool isWhite,
        int squareFile,
        int squareRank)
    {
        var attackerRank = isWhite
            ? squareRank - 1
            : squareRank + 1;

        if (attackerRank < 1 || attackerRank > 8)
        {
            return true;
        }

        return !CanEverHavePawnOn(grid, myPawns, squareFile - 1, attackerRank, isWhite) &&
               !CanEverHavePawnOn(grid, myPawns, squareFile + 1, attackerRank, isWhite);
    }

    private static bool CanEverHavePawnOn(
        char[,] grid,
        List<(int file, int rank)> myPawns,
        int file,
        int rank,
        bool pawnIsWhite)
    {
        if (file < 1 || file > 8 || rank < 1 || rank > 8)
        {
            return false;
        }

        return PawnCanReachRankOnFileByPushIgnoringPieces(grid, myPawns, file, rank, pawnIsWhite);
    }

    // King safety

    private int EvaluateKingSafety(
        char[,] grid,
        List<(int file, int rank)> whitePawns,
        List<(int file, int rank)> blackPawns,
        (int file, int rank) whiteKing,
        (int file, int rank) blackKing,
        List<(char piece, int file, int rank)> whitePieces,
        List<(char piece, int file, int rank)> blackPieces)
    {
        var whiteScore = KingSafetyOneSide(
            grid,
            isWhite: true,
            whiteKing,
            whitePawns,
            blackPawns,
            blackPieces);

        var blackScore = KingSafetyOneSide(
            grid,
            isWhite: false,
            blackKing,
            blackPawns,
            whitePawns,
            whitePieces);

        return whiteScore - blackScore;
    }

    private int KingSafetyOneSide(
        char[,] grid,
        bool isWhite,
        (int file, int rank) king,
        List<(int file, int rank)> myPawns,
        List<(int file, int rank)> opponentPawns,
        List<(char piece, int file, int rank)> opponentPieces)
    {
        var score = 0;

        var shieldRank = isWhite
            ? king.rank + 1
            : king.rank - 1;

        if (shieldRank >= 1 && shieldRank <= 8)
        {
            var shieldPawns = 0;

            for (var fileDelta = -1; fileDelta <= 1; fileDelta++)
            {
                var file = king.file + fileDelta;

                if (file < 1 || file > 8)
                {
                    continue;
                }

                if (grid[file, shieldRank] == (isWhite ? 'P' : 'p'))
                {
                    shieldPawns++;
                }
            }

            score += shieldPawns * _parameters.PawnShieldBonus;
        }

        var hasOwnPawnOnKingFile = HasPawnOnFile(myPawns, king.file);
        var hasOpponentPawnOnKingFile = HasPawnOnFile(opponentPawns, king.file);

        if (!hasOwnPawnOnKingFile && !hasOpponentPawnOnKingFile)
        {
            score -= _parameters.OpenFileToKingPenalty;
        }
        else if (!hasOwnPawnOnKingFile && hasOpponentPawnOnKingFile)
        {
            score -= _parameters.SemiOpenFileToKingPenalty;
        }

        var attackers = 0;

        foreach (var (piece, file, rank) in opponentPieces)
        {
            if (piece is 'P' or 'p')
            {
                continue;
            }

            var fileDistance = Math.Abs(file - king.file);
            var rankDistance = Math.Abs(rank - king.rank);

            if (Math.Max(fileDistance, rankDistance) <= 2)
            {
                attackers++;
            }
        }

        score -= attackers * _parameters.KingAttackUnit;

        return score;
    }

    private static bool HasPawnOnFile(List<(int file, int rank)> pawns, int file)
    {
        foreach (var pawn in pawns)
        {
            if (pawn.file == file)
            {
                return true;
            }
        }

        return false;
    }

    // Mobility

    private int EvaluateMobility(
        char[,] grid,
        List<(char piece, int file, int rank)> whitePieces,
        List<(char piece, int file, int rank)> blackPieces)
    {
        var whiteScore = MobilityOneSide(grid, whitePieces, isWhite: true);
        var blackScore = MobilityOneSide(grid, blackPieces, isWhite: false);

        return whiteScore - blackScore;
    }

    private int MobilityOneSide(
        char[,] grid,
        List<(char piece, int file, int rank)> pieces,
        bool isWhite)
    {
        var score = 0;

        foreach (var (piece, file, rank) in pieces)
        {
            switch (piece)
            {
                case 'B':
                case 'b':
                    score += _parameters.BishopMobility * CountBishopMobility(grid, file, rank, isWhite);
                    break;

                case 'R':
                case 'r':
                    score += _parameters.RookMobility * CountRookMobility(grid, file, rank, isWhite);
                    break;

                case 'Q':
                case 'q':
                    score += _parameters.QueenMobility * CountQueenMobility(grid, file, rank, isWhite);
                    break;
            }
        }

        return score;
    }

    private static int CountBishopMobility(char[,] grid, int file, int rank, bool isWhite)
    {
        return CountSliding(
            grid,
            file,
            rank,
            isWhite,
            fileDelta1: 1,
            rankDelta1: 1,
            fileDelta2: 1,
            rankDelta2: -1,
            fileDelta3: -1,
            rankDelta3: 1,
            fileDelta4: -1,
            rankDelta4: -1);
    }

    private static int CountRookMobility(char[,] grid, int file, int rank, bool isWhite)
    {
        return CountSliding(
            grid,
            file,
            rank,
            isWhite,
            fileDelta1: 1,
            rankDelta1: 0,
            fileDelta2: -1,
            rankDelta2: 0,
            fileDelta3: 0,
            rankDelta3: 1,
            fileDelta4: 0,
            rankDelta4: -1);
    }

    private static int CountQueenMobility(char[,] grid, int file, int rank, bool isWhite)
    {
        return CountRookMobility(grid, file, rank, isWhite) +
               CountBishopMobility(grid, file, rank, isWhite);
    }

    private static int CountSliding(
        char[,] grid,
        int file,
        int rank,
        bool isWhite,
        int fileDelta1,
        int rankDelta1,
        int fileDelta2,
        int rankDelta2,
        int fileDelta3,
        int rankDelta3,
        int fileDelta4,
        int rankDelta4)
    {
        return Ray(grid, file, rank, isWhite, fileDelta1, rankDelta1) +
               Ray(grid, file, rank, isWhite, fileDelta2, rankDelta2) +
               Ray(grid, file, rank, isWhite, fileDelta3, rankDelta3) +
               Ray(grid, file, rank, isWhite, fileDelta4, rankDelta4);
    }

    private static int Ray(char[,] grid, int file, int rank, bool isWhite, int fileDelta, int rankDelta)
    {
        var count = 0;
        var nextFile = file + fileDelta;
        var nextRank = rank + rankDelta;

        while (nextFile >= 1 && nextFile <= 8 && nextRank >= 1 && nextRank <= 8)
        {
            var occupant = grid[nextFile, nextRank];

            if (occupant == '\0')
            {
                count++;
            }
            else
            {
                if (IsEnemy(occupant, isWhite))
                {
                    count++;
                }

                break;
            }

            nextFile += fileDelta;
            nextRank += rankDelta;
        }

        return count;
    }

    private static bool IsEnemy(char piece, bool isWhite)
    {
        if (piece == '\0')
        {
            return false;
        }

        return isWhite
            ? char.IsLower(piece)
            : char.IsUpper(piece);
    }

    // Rook features

    private int EvaluateRookFeatures(
        char[,] grid,
        List<(char piece, int file, int rank)> whitePieces,
        List<(char piece, int file, int rank)> blackPieces,
        List<(int file, int rank)> whitePawns,
        List<(int file, int rank)> blackPawns,
        (int file, int rank) whiteKing,
        (int file, int rank) blackKing)
    {
        var whiteScore = RookFeaturesOneSide(
            grid,
            whitePieces,
            whitePawns,
            blackPawns,
            isWhite: true,
            enemyKing: blackKing);

        var blackScore = RookFeaturesOneSide(
            grid,
            blackPieces,
            blackPawns,
            whitePawns,
            isWhite: false,
            enemyKing: whiteKing);

        return whiteScore - blackScore;
    }

    private int RookFeaturesOneSide(
        char[,] grid,
        List<(char piece, int file, int rank)> pieces,
        List<(int file, int rank)> myPawns,
        List<(int file, int rank)> opponentPawns,
        bool isWhite,
        (int file, int rank) enemyKing)
    {
        var rooks = new List<(int file, int rank)>(capacity: 2);

        foreach (var (piece, file, rank) in pieces)
        {
            if (piece == (isWhite ? 'R' : 'r'))
            {
                rooks.Add((file, rank));
            }
        }

        if (rooks.Count == 0)
        {
            return 0;
        }

        var score = 0;
        var enemyIsWhite = !isWhite;
        var opponentFileCounts = new int[9];

        foreach (var pawn in opponentPawns)
        {
            opponentFileCounts[pawn.file]++;
        }

        var opponentWeakPawns = new bool[opponentPawns.Count];

        for (var index = 0; index < opponentPawns.Count; index++)
        {
            var pawn = opponentPawns[index];

            var doubled = opponentFileCounts[pawn.file] > 1;
            var isolated = IsIsolated(pawn, opponentFileCounts);
            var backward = IsBackwardPawn(grid, pawn, opponentPawns, myPawns, enemyIsWhite);

            opponentWeakPawns[index] = doubled || isolated || backward;
        }

        foreach (var rook in rooks)
        {
            var hasOwnPawnOnFile = HasPawnOnFile(myPawns, rook.file);
            var hasOpponentPawnOnFile = HasPawnOnFile(opponentPawns, rook.file);

            if (!hasOwnPawnOnFile && !hasOpponentPawnOnFile)
            {
                score += _parameters.RookOpenFileBonus;
            }
            else if (!hasOwnPawnOnFile && hasOpponentPawnOnFile)
            {
                score += _parameters.RookSemiOpenFileBonus;
            }

            var isOpenOrSemiOpenForRook = !hasOwnPawnOnFile;

            if ((isWhite && rook.rank == 7) || (!isWhite && rook.rank == 2))
            {
                score += _parameters.RookSeventhRankBonus;
            }

            if (rook.file == enemyKing.file &&
                ClearFileLine(grid, rook.file, rook.rank, enemyKing.rank))
            {
                score += _parameters.RookAttackKingFileBonus;
            }

            if (isOpenOrSemiOpenForRook &&
                Math.Abs(rook.file - enemyKing.file) == 1 &&
                HasLineToKingZoneOnFile(grid, rook, enemyKing))
            {
                score += _parameters.RookAttackKingAdjFileBonus;
            }

            if (isOpenOrSemiOpenForRook &&
                AttacksWeakPawnOnFile(grid, rook, opponentPawns, opponentWeakPawns))
            {
                score += _parameters.RookAttackWeakPawnOpenFileBonus;
            }

            if (IsBehindAnyPassedPawnOnFile(grid, rook, myPawns, opponentPawns, isWhite))
            {
                score += _parameters.RookBehindPassedPawnBonus;
            }
        }

        if (rooks.Count >= 2)
        {
            for (var first = 0; first < rooks.Count - 1; first++)
            {
                for (var second = first + 1; second < rooks.Count; second++)
                {
                    if (AreRooksConnected(grid, rooks[first], rooks[second]))
                    {
                        score += _parameters.RookConnectedBonus;
                    }
                }
            }
        }

        return score;
    }

    private static bool AttacksWeakPawnOnFile(
        char[,] grid,
        (int file, int rank) rook,
        List<(int file, int rank)> opponentPawns,
        bool[] opponentWeakPawns)
    {
        for (var index = 0; index < opponentPawns.Count; index++)
        {
            if (!opponentWeakPawns[index])
            {
                continue;
            }

            var pawn = opponentPawns[index];

            if (pawn.file == rook.file &&
                ClearFileLine(grid, rook.file, rook.rank, pawn.rank))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreRooksConnected(
        char[,] grid,
        (int file, int rank) first,
        (int file, int rank) second)
    {
        if (first.file == second.file)
        {
            return ClearFileLine(grid, first.file, first.rank, second.rank);
        }

        if (first.rank == second.rank)
        {
            return ClearRankLine(grid, first.rank, first.file, second.file);
        }

        return false;
    }

    private static bool ClearFileLine(char[,] grid, int file, int firstRank, int secondRank)
    {
        var lowRank = Math.Min(firstRank, secondRank) + 1;
        var highRank = Math.Max(firstRank, secondRank) - 1;

        for (var rank = lowRank; rank <= highRank; rank++)
        {
            if (grid[file, rank] != '\0')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ClearRankLine(char[,] grid, int rank, int firstFile, int secondFile)
    {
        var lowFile = Math.Min(firstFile, secondFile) + 1;
        var highFile = Math.Max(firstFile, secondFile) - 1;

        for (var file = lowFile; file <= highFile; file++)
        {
            if (grid[file, rank] != '\0')
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasLineToKingZoneOnFile(
        char[,] grid,
        (int file, int rank) rook,
        (int file, int rank) enemyKing)
    {
        var fromRank = Math.Max(1, enemyKing.rank - 2);
        var toRank = Math.Min(8, enemyKing.rank + 2);

        for (var rank = fromRank; rank <= toRank; rank++)
        {
            if (ClearFileLine(grid, rook.file, rook.rank, rank))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBehindAnyPassedPawnOnFile(
        char[,] grid,
        (int file, int rank) rook,
        List<(int file, int rank)> myPawns,
        List<(int file, int rank)> opponentPawns,
        bool isWhite)
    {
        foreach (var pawn in myPawns)
        {
            if (pawn.file != rook.file)
            {
                continue;
            }

            if (!IsPassedPawn(pawn, opponentPawns, isWhite))
            {
                continue;
            }

            var isBehind = isWhite
                ? rook.rank < pawn.rank
                : rook.rank > pawn.rank;

            if (isBehind && ClearFileLine(grid, rook.file, rook.rank, pawn.rank))
            {
                return true;
            }
        }

        return false;
    }

    // Knight features

    private int EvaluateKnightFeatures(
        char[,] grid,
        List<(char piece, int file, int rank)> whitePieces,
        List<(char piece, int file, int rank)> blackPieces,
        List<(int file, int rank)> whitePawns,
        List<(int file, int rank)> blackPawns)
    {
        var whiteScore = KnightFeaturesOneSide(
            grid,
            whitePieces,
            whitePawns,
            blackPawns,
            isWhite: true);

        var blackScore = KnightFeaturesOneSide(
            grid,
            blackPieces,
            blackPawns,
            whitePawns,
            isWhite: false);

        return whiteScore - blackScore;
    }

    private int KnightFeaturesOneSide(
        char[,] grid,
        List<(char piece, int file, int rank)> pieces,
        List<(int file, int rank)> myPawns,
        List<(int file, int rank)> opponentPawns,
        bool isWhite)
    {
        var score = 0;

        foreach (var (piece, file, rank) in pieces)
        {
            if (piece != (isWhite ? 'N' : 'n'))
            {
                continue;
            }

            var fileCentrality = Math.Min(file - 1, 8 - file);
            var rankCentrality = Math.Min(rank - 1, 8 - rank);
            var centrality = fileCentrality + rankCentrality;

            score += _parameters.KnightSquareMult * centrality;

            if (IsKnightOutpost(grid, file, rank, myPawns, opponentPawns, isWhite))
            {
                score += _parameters.KnightOutpostBonus;
            }
        }

        return score;
    }

    private static bool IsKnightOutpost(
        char[,] grid,
        int file,
        int rank,
        List<(int file, int rank)> myPawns,
        List<(int file, int rank)> opponentPawns,
        bool isWhite)
    {
        if (isWhite && rank < 5)
        {
            return false;
        }

        if (!isWhite && rank > 4)
        {
            return false;
        }

        var supported = isWhite
            ? HasPawnAt(myPawns, file - 1, rank - 1) ||
              HasPawnAt(myPawns, file + 1, rank - 1)
            : HasPawnAt(myPawns, file - 1, rank + 1) ||
              HasPawnAt(myPawns, file + 1, rank + 1);

        if (!supported)
        {
            return false;
        }

        var attackedNow = isWhite
            ? HasPawnAt(opponentPawns, file - 1, rank + 1) ||
              HasPawnAt(opponentPawns, file + 1, rank + 1)
            : HasPawnAt(opponentPawns, file - 1, rank - 1) ||
              HasPawnAt(opponentPawns, file + 1, rank - 1);

        if (attackedNow)
        {
            return false;
        }

        var targetPawnRank = isWhite
            ? rank + 1
            : rank - 1;

        if (targetPawnRank < 1 || targetPawnRank > 8)
        {
            return false;
        }

        var enemyIsWhite = !isWhite;

        if (EnemyPawnCanReachRankOnFileByPush(grid, opponentPawns, file - 1, targetPawnRank, enemyIsWhite))
        {
            return false;
        }

        if (EnemyPawnCanReachRankOnFileByPush(grid, opponentPawns, file + 1, targetPawnRank, enemyIsWhite))
        {
            return false;
        }

        return true;
    }

    private static bool EnemyPawnCanReachRankOnFileByPush(
        char[,] grid,
        List<(int file, int rank)> enemyPawns,
        int file,
        int targetRank,
        bool enemyIsWhite)
    {
        if (file < 1 || file > 8)
        {
            return false;
        }

        foreach (var pawn in enemyPawns)
        {
            if (pawn.file != file)
            {
                continue;
            }

            if (pawn.rank == targetRank)
            {
                return true;
            }

            if (enemyIsWhite)
            {
                if (pawn.rank > targetRank)
                {
                    continue;
                }

                if (!PathHasPawnBlocker(grid, file, fromRank: pawn.rank + 1, toRank: targetRank))
                {
                    return true;
                }
            }
            else
            {
                if (pawn.rank < targetRank)
                {
                    continue;
                }

                if (!PathHasPawnBlocker(grid, file, fromRank: targetRank, toRank: pawn.rank - 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool PathHasPawnBlocker(char[,] grid, int file, int fromRank, int toRank)
    {
        for (var rank = fromRank; rank <= toRank; rank++)
        {
            var piece = grid[file, rank];

            if (piece is 'P' or 'p')
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPawnAt(List<(int file, int rank)> pawns, int file, int rank)
    {
        if (file < 1 || file > 8 || rank < 1 || rank > 8)
        {
            return false;
        }

        foreach (var pawn in pawns)
        {
            if (pawn.file == file && pawn.rank == rank)
            {
                return true;
            }
        }

        return false;
    }

    // FEN parsing

    private static void ParseFenBoard(
        string boardPart,
        out char[,] grid,
        out List<(int file, int rank)> whitePawns,
        out List<(int file, int rank)> blackPawns,
        out int whiteBishops,
        out int blackBishops,
        out int whiteKnights,
        out int blackKnights,
        out int whiteRooks,
        out int blackRooks,
        out int whiteQueens,
        out int blackQueens,
        out (int file, int rank) whiteKing,
        out (int file, int rank) blackKing,
        out List<(char piece, int file, int rank)> whitePieces,
        out List<(char piece, int file, int rank)> blackPieces)
    {
        grid = new char[9, 9];

        whitePawns = new List<(int file, int rank)>(capacity: 16);
        blackPawns = new List<(int file, int rank)>(capacity: 16);
        whitePieces = new List<(char piece, int file, int rank)>(capacity: 16);
        blackPieces = new List<(char piece, int file, int rank)>(capacity: 16);

        whiteBishops = 0;
        blackBishops = 0;
        whiteKnights = 0;
        blackKnights = 0;
        whiteRooks = 0;
        blackRooks = 0;
        whiteQueens = 0;
        blackQueens = 0;

        whiteKing = (0, 0);
        blackKing = (0, 0);

        var ranks = boardPart.Split('/');
        if (ranks.Length != 8)
        {
            throw new ArgumentException("Invalid FEN board part.", nameof(boardPart));
        }

        for (var rankIndex = 0; rankIndex < 8; rankIndex++)
        {
            var rank = 8 - rankIndex;
            var file = 1;

            foreach (var piece in ranks[rankIndex])
            {
                if (char.IsDigit(piece))
                {
                    file += piece - '0';
                    continue;
                }

                grid[file, rank] = piece;

                if (char.IsUpper(piece))
                {
                    whitePieces.Add((piece, file, rank));
                }
                else
                {
                    blackPieces.Add((piece, file, rank));
                }

                switch (piece)
                {
                    case 'P':
                        whitePawns.Add((file, rank));
                        break;

                    case 'p':
                        blackPawns.Add((file, rank));
                        break;

                    case 'B':
                        whiteBishops++;
                        break;

                    case 'b':
                        blackBishops++;
                        break;

                    case 'N':
                        whiteKnights++;
                        break;

                    case 'n':
                        blackKnights++;
                        break;

                    case 'R':
                        whiteRooks++;
                        break;

                    case 'r':
                        blackRooks++;
                        break;

                    case 'Q':
                        whiteQueens++;
                        break;

                    case 'q':
                        blackQueens++;
                        break;

                    case 'K':
                        whiteKing = (file, rank);
                        break;

                    case 'k':
                        blackKing = (file, rank);
                        break;
                }

                file++;
            }

            if (file != 9)
            {
                throw new ArgumentException(
                    "Invalid FEN board layout; rank does not contain exactly eight files.",
                    nameof(boardPart));
            }
        }
    }
}
