using ChessGP.Core.Genome;

namespace ChessGP.Core.Eval;

/// <summary>
/// Decodes genome bit strings into concrete evaluator parameters.
/// </summary>
public static class GenomeToParamsDecoder
{
    /// <summary>
    /// Decodes a genome using the default baseline parameters.
    /// </summary>
    public static DecodedParams Decode(string genomeBits, GeneSchema schema)
    {
        return Decode(genomeBits, schema, DecodedParams.ReasonableDefaults);
    }

    /// <summary>
    /// Decodes a genome using the supplied baseline for parameters not present in the schema.
    /// </summary>
    public static DecodedParams Decode(string genomeBits, GeneSchema schema, DecodedParams baseline)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(genomeBits);

        if (genomeBits.Length != schema.TotalBits)
        {
            throw new ArgumentException(
                $"Genome bitstring length {genomeBits.Length} does not match schema.TotalBits {schema.TotalBits}.");
        }

        var bits = ToBits(genomeBits);
        var builder = new ParameterBuilder(baseline);

        foreach (var (gene, offset, width) in schema.Enumerate())
        {
            var geneBits = new ReadOnlySpan<bool>(bits, offset, width);
            var decodedValue = GrayCodec.Decode(geneBits, gene);
            var intValue = (int)Math.Round(decodedValue, MidpointRounding.AwayFromZero);

            ApplyGene(ref builder, gene.Name, intValue);
        }

        return builder.Build();
    }

    private static bool[] ToBits(string genomeBits)
    {
        var bits = new bool[genomeBits.Length];

        for (var index = 0; index < genomeBits.Length; index++)
        {
            bits[index] = genomeBits[index] switch
            {
                '0' => false,
                '1' => true,
                _ => throw new ArgumentException("Genome bitstring must contain only '0' and '1'.")
            };
        }

        return bits;
    }

    private static void ApplyGene(ref ParameterBuilder builder, string name, int value)
    {
        var key = Normalize(name);

        switch (key)
        {
            // Piece values.
            case "knight":
            case "knightvalue":
            case "materialknight":
            case "knightmg":
            case "knighteg":
            case "materialknightmg":
            case "materialknighteg":
                builder.KnightValue = value;
                return;

            case "bishop":
            case "bishopvalue":
            case "materialbishop":
            case "bishopmg":
            case "bishopeg":
            case "materialbishopmg":
            case "materialbishopeg":
                builder.BishopValue = value;
                return;

            case "rook":
            case "rookvalue":
            case "materialrook":
            case "rookmg":
            case "rookeg":
            case "materialrookmg":
            case "materialrookeg":
                builder.RookValue = value;
                return;

            case "queen":
            case "queenvalue":
            case "materialqueen":
            case "queenmg":
            case "queeneg":
            case "materialqueenmg":
            case "materialqueeneg":
                builder.QueenValue = value;
                return;

            // General positional bonuses.
            case "bishoppair":
            case "bishoppairbonus":
            case "bishoppairmg":
            case "bishoppaireg":
                builder.BishopPairBonus = value;
                return;

            case "tempo":
            case "tempobonus":
            case "tempomg":
            case "tempoeg":
                builder.TempoBonus = value;
                return;

            // Pawn structure.
            case "doubledpawnpenalty":
                builder.DoubledPawnPenalty = value;
                return;

            case "isolatedpawnpenalty":
                builder.IsolatedPawnPenalty = value;
                return;

            case "backwardpawnpenalty":
                builder.BackwardPawnPenalty = value;
                return;

            case "weaksquarepenalty":
                builder.WeakSquarePenalty = value;
                return;

            case "passedpawnbonus":
                builder.PassedPawnBonus = value;
                return;

            case "protectedpassedpawnbonus":
            case "protectedpassedbonus":
                builder.ProtectedPassedPawnBonus = value;
                return;

            case "connectedpassedbonus":
                builder.ConnectedPassedBonus = value;
                return;

            case "passedpawnenemykingdist":
            case "passedpawnenemykingdistbonus":
                builder.PassedPawnEnemyKingDistBonus = value;
                return;

            // King safety.
            case "pawnshieldbonus":
            case "pawnshieldbonusmg":
            case "pawnshieldbonuseg":
                builder.PawnShieldBonus = value;
                return;

            case "openfiletokingpenalty":
            case "openfiletokingpenaltymg":
            case "openfiletokingpenaltyeg":
                builder.OpenFileToKingPenalty = value;
                return;

            case "semiopenfiletokingpenalty":
            case "semiopenfiletokingpenaltymg":
            case "semiopenfiletokingpenaltyeg":
                builder.SemiOpenFileToKingPenalty = value;
                return;

            case "kingattackunit":
            case "kingattackunitmg":
            case "kingattackuniteg":
                builder.KingAttackUnit = value;
                return;

            // Mobility.
            case "bishopmobility":
                builder.BishopMobility = value;
                return;

            case "rookmobility":
                builder.RookMobility = value;
                return;

            case "queenmobility":
                builder.QueenMobility = value;
                return;

            // Rook features.
            case "rookopenfilebonus":
            case "rookopenfile":
                builder.RookOpenFileBonus = value;
                return;

            case "rooksemiopenfilebonus":
            case "rooksemiopenfile":
                builder.RookSemiOpenFileBonus = value;
                return;

            case "rookseventhrankbonus":
            case "rook7thrank":
            case "rookseventhrank":
                builder.RookSeventhRankBonus = value;
                return;

            case "rookconnectedbonus":
            case "rookconnected":
                builder.RookConnectedBonus = value;
                return;

            case "rookbehindpassedpawnbonus":
            case "rookbehindpassedpawn":
                builder.RookBehindPassedPawnBonus = value;
                return;

            case "rookattackkingfilebonus":
            case "rookattackkingfile":
                builder.RookAttackKingFileBonus = value;
                return;

            case "rookattackkingadjfilebonus":
            case "rookattackkingadjfile":
                builder.RookAttackKingAdjFileBonus = value;
                return;

            case "rookatckweakpawnopencolumn":
            case "rookattackweakpawnopenfilebonus":
            case "rookattackweakpawnopenfile":
                builder.RookAttackWeakPawnOpenFileBonus = value;
                return;

            // Knight features.
            case "knightsqmult":
            case "knightsquaremult":
            case "knightsquaremultiplier":
                builder.KnightSquareMult = value;
                return;

            case "knightoutpostmult":
            case "knightoutpostbonus":
            case "knightoutpost":
                builder.KnightOutpostBonus = value;
                return;

            default:
                return;
        }
    }

    private static string Normalize(string value)
    {
        value = (value ?? string.Empty).Trim().ToLowerInvariant();

        var characters = new char[value.Length];
        var writeIndex = 0;

        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                characters[writeIndex++] = character;
            }
        }

        return new string(characters, 0, writeIndex);
    }

    private struct ParameterBuilder
    {
        public int KnightValue;
        public int BishopValue;
        public int RookValue;
        public int QueenValue;

        public int BishopPairBonus;
        public int TempoBonus;

        public int DoubledPawnPenalty;
        public int IsolatedPawnPenalty;
        public int BackwardPawnPenalty;
        public int WeakSquarePenalty;
        public int PassedPawnBonus;
        public int ProtectedPassedPawnBonus;
        public int ConnectedPassedBonus;
        public int PassedPawnEnemyKingDistBonus;

        public int PawnShieldBonus;
        public int OpenFileToKingPenalty;
        public int SemiOpenFileToKingPenalty;
        public int KingAttackUnit;

        public int BishopMobility;
        public int RookMobility;
        public int QueenMobility;

        public int RookOpenFileBonus;
        public int RookSemiOpenFileBonus;
        public int RookSeventhRankBonus;
        public int RookConnectedBonus;
        public int RookBehindPassedPawnBonus;
        public int RookAttackKingFileBonus;
        public int RookAttackKingAdjFileBonus;
        public int RookAttackWeakPawnOpenFileBonus;

        public int KnightSquareMult;
        public int KnightOutpostBonus;

        public ParameterBuilder(DecodedParams parameters)
        {
            KnightValue = parameters.KnightValue;
            BishopValue = parameters.BishopValue;
            RookValue = parameters.RookValue;
            QueenValue = parameters.QueenValue;

            BishopPairBonus = parameters.BishopPairBonus;
            TempoBonus = parameters.TempoBonus;

            DoubledPawnPenalty = parameters.DoubledPawnPenalty;
            IsolatedPawnPenalty = parameters.IsolatedPawnPenalty;
            BackwardPawnPenalty = parameters.BackwardPawnPenalty;
            WeakSquarePenalty = parameters.WeakSquarePenalty;
            PassedPawnBonus = parameters.PassedPawnBonus;
            ProtectedPassedPawnBonus = parameters.ProtectedPassedPawnBonus;
            ConnectedPassedBonus = parameters.ConnectedPassedBonus;
            PassedPawnEnemyKingDistBonus = parameters.PassedPawnEnemyKingDistBonus;

            PawnShieldBonus = parameters.PawnShieldBonus;
            OpenFileToKingPenalty = parameters.OpenFileToKingPenalty;
            SemiOpenFileToKingPenalty = parameters.SemiOpenFileToKingPenalty;
            KingAttackUnit = parameters.KingAttackUnit;

            BishopMobility = parameters.BishopMobility;
            RookMobility = parameters.RookMobility;
            QueenMobility = parameters.QueenMobility;

            RookOpenFileBonus = parameters.RookOpenFileBonus;
            RookSemiOpenFileBonus = parameters.RookSemiOpenFileBonus;
            RookSeventhRankBonus = parameters.RookSeventhRankBonus;
            RookConnectedBonus = parameters.RookConnectedBonus;
            RookBehindPassedPawnBonus = parameters.RookBehindPassedPawnBonus;
            RookAttackKingFileBonus = parameters.RookAttackKingFileBonus;
            RookAttackKingAdjFileBonus = parameters.RookAttackKingAdjFileBonus;
            RookAttackWeakPawnOpenFileBonus = parameters.RookAttackWeakPawnOpenFileBonus;

            KnightSquareMult = parameters.KnightSquareMult;
            KnightOutpostBonus = parameters.KnightOutpostBonus;
        }

        public DecodedParams Build()
        {
            return new DecodedParams(
                KnightValue,
                BishopValue,
                RookValue,
                QueenValue,

                BishopPairBonus,
                TempoBonus,

                DoubledPawnPenalty,
                IsolatedPawnPenalty,
                BackwardPawnPenalty,
                WeakSquarePenalty,
                PassedPawnBonus,
                ProtectedPassedPawnBonus,
                ConnectedPassedBonus,
                PassedPawnEnemyKingDistBonus,

                PawnShieldBonus,
                OpenFileToKingPenalty,
                SemiOpenFileToKingPenalty,
                KingAttackUnit,

                BishopMobility,
                RookMobility,
                QueenMobility,

                RookOpenFileBonus,
                RookSemiOpenFileBonus,
                RookSeventhRankBonus,
                RookConnectedBonus,
                RookBehindPassedPawnBonus,
                RookAttackKingFileBonus,
                RookAttackKingAdjFileBonus,
                RookAttackWeakPawnOpenFileBonus,

                KnightSquareMult,
                KnightOutpostBonus);
        }
    }
}
