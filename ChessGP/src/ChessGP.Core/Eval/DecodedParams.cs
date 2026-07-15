namespace ChessGP.Core.Eval;

/// <summary>
/// Decoded evaluation parameters used by the chess evaluator.
/// </summary>
/// <remarks>
/// Values are expressed in centipawns unless their name indicates otherwise.
/// Pawn value is fixed at 100 centipawns and is therefore not part of the genome.
/// </remarks>
public readonly record struct DecodedParams(
    // Piece values.
    int KnightValue,
    int BishopValue,
    int RookValue,
    int QueenValue,

    // General positional bonuses.
    int BishopPairBonus,
    int TempoBonus,

    // Pawn structure.
    int DoubledPawnPenalty,
    int IsolatedPawnPenalty,
    int BackwardPawnPenalty,
    int WeakSquarePenalty,
    int PassedPawnBonus,
    int ProtectedPassedPawnBonus,
    int ConnectedPassedBonus,
    int PassedPawnEnemyKingDistBonus,

    // King safety.
    int PawnShieldBonus,
    int OpenFileToKingPenalty,
    int SemiOpenFileToKingPenalty,
    int KingAttackUnit,

    // Mobility.
    int BishopMobility,
    int RookMobility,
    int QueenMobility,

    // Rook features.
    int RookOpenFileBonus,
    int RookSemiOpenFileBonus,
    int RookSeventhRankBonus,
    int RookConnectedBonus,
    int RookBehindPassedPawnBonus,
    int RookAttackKingFileBonus,
    int RookAttackKingAdjFileBonus,
    int RookAttackWeakPawnOpenFileBonus,

    // Knight features.
    int KnightSquareMult,
    int KnightOutpostBonus)
{
    /// <summary>
    /// Hand-written baseline values used when no evolved genome is supplied.
    /// </summary>
    public static DecodedParams ReasonableDefaults => new(
        KnightValue: 320,
        BishopValue: 330,
        RookValue: 500,
        QueenValue: 900,

        BishopPairBonus: 30,
        TempoBonus: 10,

        DoubledPawnPenalty: 12,
        IsolatedPawnPenalty: 8,
        BackwardPawnPenalty: 10,
        WeakSquarePenalty: 8,
        PassedPawnBonus: 25,
        ProtectedPassedPawnBonus: 30,
        ConnectedPassedBonus: 35,
        PassedPawnEnemyKingDistBonus: 8,

        PawnShieldBonus: 10,
        OpenFileToKingPenalty: 18,
        SemiOpenFileToKingPenalty: 10,
        KingAttackUnit: 6,

        BishopMobility: 1,
        RookMobility: 2,
        QueenMobility: 3,

        RookOpenFileBonus: 12,
        RookSemiOpenFileBonus: 6,
        RookSeventhRankBonus: 18,
        RookConnectedBonus: 10,
        RookBehindPassedPawnBonus: 20,
        RookAttackKingFileBonus: 30,
        RookAttackKingAdjFileBonus: 18,
        RookAttackWeakPawnOpenFileBonus: 18,

        KnightSquareMult: 2,
        KnightOutpostBonus: 20);
}
