# 1. Project overview

**Goal:** Implement a system that **evolves chess evaluation functions** in two stages:

1. **Supervised learning** from a suitable dataset (chess GM games)
2. **Unsupervised refinement** via **mutual gameplay (coevolution)**

**Language/stack:** C# (.NET 8), console app + one class library.  
**Config:** a small JSON file loaded once at startup.  
**Logging:** simple console logging (CSV/JSON files for metrics).  
**Search:** Move choice is **1-ply** in Stage 1; **basic alpha-beta** in Stage 2.

# 2. System architecture (modules)

~10 modules; each has a concrete class (interfaces optional). All wiring is done explicitly in `Program.cs` using simple constructors and passing options.

## 2.1 Engine Core (Move Selector & Searcher)

**Responsibility**

* Provide **two modes** used by the stages:

  * **Stage 1 mode – 1-ply selector:** generate all legal moves, apply each, evaluate the child position via the candidate evaluator, choose the move with the highest evaluation.
  * **Stage 2 mode – basic alpha-beta search:** fixed-depth alpha-beta with iterative deepening permitted, no selective pruning/heuristics (no null-move, futility, razoring, LMR, etc.) and no quiescence. Evaluation is only the candidate evaluator.
* Use a **deterministic tie-break rule** (“first in generated order”) whenever multiple moves share the same score.

**Main API**

* `SelectBestMove1Ply(position, evaluator)` → returns (bestMove, bestScore, nodesEvaluated).
* `SearchAlphaBeta(position, evaluator, depth, timeControl)` → returns (bestMove, bestScore, nodesEvaluated); depth is mandatory; `timeControl` is only used if Stage-2 `searchMode = FixedTimePerMove`.
* `GenerateLegalMoves(position)` and `Make/Unmake` are part of the engine core.

**Notes**

* Exactly the same evaluator instance (per organism) is used by both modes.
* Stage 1 always calls the **1-ply** method. Stage 2 always calls the **alpha-beta** method with a fixed depth and otherwise identical settings for all organisms.

## 2.2 Evaluator Runtime

**Responsibility**

* Host the **candidate** evaluation function (parametric v1) and return a tapered centipawn score for a position.

**Main API**

* `Evaluate(position)` → `int` (centipawns; tapered by phase).

**Data model — Binary Gray-coded genome**

* **Genome representation:** a **bitstring** of total length **L** using **Gray coding** per gene.

  * A **GeneSchema** partitions the bitstring into named genes that match the **paper’s evaluation parameters** (e.g., piece values mid/end; pawn structure bonuses/penalties such as doubled/isolated/backward; passed-pawn bonuses by rank/connectedness; king-safety terms like pawn shield and open lines near king; mobility/activity terms by piece; threat/attack/pressure terms; tempo/bishop pair).
  * Each gene has: `bitWidth`, numeric **range** `[min, max]` (centipawns or scale units), and optional **constraint flags** (e.g., “passed-pawn bonus must not decrease with rank”).
* **Decode once per organism per evaluation pass:** slice → Gray→unsigned integer → linear map `[min,max]` → cached **DecodedParams** (strongly-typed struct with all scalar parameters).

**Constraints**

* Per-gene min/max, optional simple constraints.

## 2.3 Evolution Driver

**Responsibility**

* Run the GA loop for both stages: selection, crossover, mutation, elitism; produce generation summaries and persist checkpoints.

**Main API**

* `RunStage1(config, trainer, population)` → `GenerationSummary`.
* `RunStage2(config, arena, population)` → `GenerationSummary`.

**Configurable strategy objects (simple, concrete classes)**

* Selection (tournament, `k` configurable).
* **Crossover (uniform (per gene) on the bitstring).**
* **Mutation (bit-flip on the bitstring).**
* Elitism count (1).

**Determinism**

* One fixed RNG seed per generation.
* Stage-1 uses the **same sampled batch** for all organisms in a generation.

**Storage note**

* Each **Individual** stores its **bitstring genome** and an **invalidatable decoded cache** (re-decoded after crossover/mutation).

## 2.4 Stage 1 Trainer (Supervised)

**Responsibility**

* Compute fitness as the **square of the correctly matched Human GM moves** over a sampled batch.
* For each labeled position: use **1-ply** engine mode with the candidate evaluator to pick a move, compare to the GM move, and count correctness.

**Main API**

* `Evaluate(genome, batch, config)` → `Fitness` plus per-genome stats.

**Fitness (exact rule)**

* For a batch of `N` positions: if predicted move equals GM move, `correct += 1`.
* **Fitness = (correct)^2**.

**Batching**

* **Default:** `BatchSize = 5000` positions per generation per organism.
* Fairness: all organisms see the **same batch** within a generation (fixed seed).
* Validation set is optional and for reporting only.

**Reported stats**

* `Correct`, `Total`, `NodesEvaluated` (for logging), plus count of ties encountered (optional).

**Tie-break**

* **Default:** “first in generated order.”

## 2.5 Stage 2 Arena (Coevolution)

**Responsibility**

* Run a **mini-tournament** among organisms where every game is played using the **Stage-2 alpha-beta** mode with the organism’s evaluator.
* Use a small balanced opening set; apply color symmetry.
* Compute **relative scores** from match results and use that as fitness.

**Main API**

* `PlayMiniMatches(genomes, config)` → per-organism **RelativeScore** and logs.

**Match format**

* **Default:** **4 games per pairing** (mini-match size = 4), colors alternated.
* Scoring: standard chess scoring per game (1/0.5/0).
* **Fitness:** `relativeScore = (total points earned) / (total possible points)` → in `[0,1]`.

**Search settings (Stage 2)**

* **Basic alpha-beta** at fixed depth (set in config); `searchMode` defaults to **FixedDepth**; `FixedTimePerMove` is optional and off by default.
* No selective pruning/heuristics (no null-move, futility, razoring, LMR, etc.) and no quiescence.
* Identical depth and time control for all organisms.
* Optional **per-move node cap** (if set in config) is enforced as a hard stop in search, in either mode.

**Stability aids**

* Hall-of-Fame (optional): include a subset of past best genomes; their games count into relative scores.
* Opening rotation: pick a subset per generation (deterministic seed).
* Adjudication: 50-move rule and threefold repetition.

## 2.6 Dataset Module

**Responsibility**

* Load PGN/FEN/CSV; normalize to internal **labeled positions** with exactly one reference (GM) move per position for Stage-1.
* Provide simple filters (e.g., midgame only, minimum legal moves) and deduplication.

**Main API**

* `ReadGames(sourceSpec)` → iterable of game records (optional).
* `StreamPositions(query)` → iterable of `LabeledPosition` (position + reference GM move).

**Parameters**

* `DataSourceSpec` (path and format: PGN, FEN, CSV).

## 2.7 Fitness Module

**Responsibility**

* Encapsulate fitness computation for both stages; provide terms for logging; optionally project a full genome to a fixed chromosome size if needed.

**Main API**

* `ComputeStage1Fitness(stats)` → `Fitness` with `Value = (Correct)^2`.
* `ComputeStage2Fitness(relativeScore)` → `Fitness` with `Value = relativeScore`.

**Optional**

* `ProjectGenome(fullGenome, desiredGeneCount)` if a fixed chromosome size is required.

## 2.8 Metrics & Checkpoints

**Responsibility**

* Save best genomes, generation summaries, and CSV/JSON metrics.
* Load prior checkpoints to resume or compare.

**Main API**

* `SaveBest(genomeBitstring, schemaVersion, path)` / `Load(path)`
* `AppendMetrics(file, row)` (CSV/JSON)

**Recorded fields**

* Stage, generation, best fitness, mean/median fitness, `Correct/Total` (Stage-1), relative score distribution (Stage-2), nodes evaluated, RNG seed, time per generation.
* **Genome persistence:** store the **bitstring** and the **GeneSchema** to decode later.

## 2.9 Config (simple JSON)

**Responsibility**

* Load once at program start; supply paths and knobs.

**Default fields (suggested)**

* **Data:** `{ path, format, filters }`
* **Stage1:** `{ population, crossoverRate, mutationRate, generations, batchSize: 5000, ply: 1 }`
* **Stage2:** `{ population, crossoverRate, mutationRate, generations, gamesPerPair: 4, openingBookPath, useHallOfFame, hallOfFameSize, searchDepth, searchMode, timePerMoveMs, perMoveNodeCap }`
* **GA:** `{ selectionType, tournamentK, bitFlipRate, crossoverType, elitism: 1 }`
* **Genome:** `{ schemaId, genes: [ { name, bitWidth, min, max, constraintFlags } ... ] }`  ← **matches the paper’s parameter list**
* **Rng:** `{ seed }`
* **Output:** `{ checkpointsDir, metricsFile }`  

**Validation**
* If `searchMode = "FixedTimePerMove"`, require `timePerMoveMs > 0`; if `searchMode = "FixedDepth"`, ignore `timePerMoveMs`. `perMoveNodeCap` is optional and applies to either mode as a hard stop.

## 2.10 Unit Testing & Quality Module

**Responsibility**  
- Provide **automated unit tests** (plus a few invariants/property checks) for critical parts of the system. Tests are run locally and in CI; they do **not** influence fitness or evolution, but they guard correctness and reproducibility.

**Scope & example test sets**  
1. **Move generation & legality (Engine Core)**
2. **Evaluator decoding & constraints (Evaluator Runtime)**
3. **Stage 1 Trainer correctness**
4. **Stage 2 Arena accounting**
5. **GA operators on the bitstring genome**

# 3. Evaluation function design

**Parametric Evaluator (v1)**

* **Genome = binary Gray-coded bitstring** whose **genes correspond 1:1** to the evaluation parameters reported in the paper (examples include, but are not limited to):

  * **Material & phase:** piece values midgame/endgame; phase interpolation/scaling.
  * **Pawn structure:** doubled, isolated, backward, candidate passed, connected passed, passed-pawn rank bonuses, pawn majorities/minorities.
  * **King safety:** pawn shield values, open/semi-open files against king, attacker counts/weights, king exposure penalties.
  * **Mobility / activity:** per-piece mobility weights; rook on open file/seventh rank; bishop pair bonus; trapped piece penalties.
  * **Threats & pressure:** attacked/undefended piece penalties, piece attack weights, tempo.
  * **Global scalers:** endgame scaling factors, drawish endgame scalings when appropriate.
* **Decoding:** Gray→integer→linear map to `[min,max]`.

**Constraints**

* Per-gene bounds and logical relationships matching the paper’s intent (e.g., reasonable centipawn ranges for each term).

# 4. Fitness design

## Stage 1 — Supervised (move-match only)

* **Per-position rule:** **1-ply**: evaluate each child position with the candidate evaluator; choose argmax (tie-break: first in generated order).
* **Correctness:** predicted move equals the GM reference move → count 1, else 0.
* **Fitness:** `fitness = (number of correct over batch)^2`.
* **Batching:** **5000** positions per generation; same batch for all organisms (fixed seed).
* **Validation:** optional; for reporting only.

## Stage 2 — Coevolution (mini-matches; relative score)

* **Search:** **basic alpha-beta** at fixed depth; iterative deepening allowed. Identical settings for all organisms.
* **Format:** round-robin with **4 games per pairing**, colors alternated; starts from a balanced opening set.
* **Fitness:** `relativeScore = points earned / points possible` across all games for the organism in that generation (value in `[0, 1]`).
* **Stability:** optional Hall-of-Fame; opening rotation; standard adjudication.

# 5. Genetic algorithm setup — Binary/Gray

* **Representation:** **bitstring** genome with **Gray-coded** genes defined by a **GeneSchema**.
* **Initialization:** random bits per gene (uniform in Gray space); optionally seed one elite from a hand-set baseline encoded into bits.
* **Population & operators:**

  * Size 100; **tournament selection (k=4)**; **elitism = 1**.
  * **Crossover:** uniform (per gene) on the **bitstring**.
  * **Mutation:** **bit-flip** with per-bit rate `bitFlipRate`.
* **Decode/repair (per fitness eval):** slice → Gray→int → linear map → optional repair → use **DecodedParams** in evaluator.
* **Schedules:** Stage-1 until plateau → seed Stage-2 with top-10 genomes.
* **Determinism:** fixed RNG seed; identical Stage-1 batch; fixed tie-break.

# 6. Feature set (parametric track)

* **Material & phase values; phase interpolation.**
* **Pawn structure terms** (doubled/isolated/backward, candidate/connected/passed pawns with rank-based components, majorities).
* **King safety terms** (pawn shield, open/semi-open files towards king, attackers/weights, king exposure).
* **Mobility & activity** (per-piece mobility weights; rook on open/seventh; bishop pair; trapped/poor piece penalties).
* **Threats/pressure & tempo.**
* **Global scalers** and **endgame-specific scalings** (e.g., opposite bishops, drawish endgames).

# 7. Data & assets

* **Training positions:** extracted from PGN/FEN with deduplication and filtering (e.g., minimum mobility; optional midgame bias).
* **Labels:** exactly **one GM move per position** for Stage-1.
* **Opening sets (Stage 2):** small, balanced collections; rotate subsets per generation (deterministic seed).
* **Test suites (optional):** ECM/STS/LCTII at fixed node caps for monitoring.
* **Storage:** versioned CSV/JSON with manifests; train/val/test splits; saved genomes (bitstrings + schema id) and run summaries.

# 8. Communication & data flow

* **Startup:** load JSON config → construct modules explicitly → seed population.
* **Stage-1 loop:** sample **5000** positions (fixed seed) → decode/repair genome → evaluator built → trainer computes fitness via **1-ply** move matching → GA operators → checkpoint + metrics.
* **Stage-2 loop:** schedule **4-game** mini-matches for each pairing → decode/repair genome → play using **basic alpha-beta** at fixed depth → compute relative scores → GA operators → checkpoint + metrics.
* **Artifacts:** best genome per stage (bitstring + schema id), generation logs, RNG seeds, and settings saved for reproducibility.
