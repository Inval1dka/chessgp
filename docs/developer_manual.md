# Developer Manual

This document gives a practical overview of the ChessGP codebase. It describes the main projects, modules, training flow, outputs, plotting script, and tests.

For usage instructions, see [user_guide.md](user_guide.md).

## 1. Project overview

ChessGP is divided into two main C# projects:

```text
ChessGP/
  src/
    ChessGP.Core/
    ChessGP.App/
```

`ChessGP.Core` contains the core implementation: configuration, data loading, chess engine logic, evaluation, genome encoding, the genetic algorithm, persistence, and training drivers.

`ChessGP.App` contains the command-line application. It connects the core library to user-facing commands such as Stage 1 training, Stage 2 training, checkpoint evaluation, interactive play, and UCI mode.

Tests are stored in:

```text
ChessGP/
  tests/
    ChessGP.Tests/
```

The plotting script is stored in:

```text
ChessGP/
  scripts/
    plot_evolution_results.py
```

## 2. Main modules

### `Config`

The `Config` module defines configuration records and loading logic.

Important files:

```text
Options.cs
ConfigLoader.cs
```

`Options.cs` defines the root `AppOptions` record and nested option records such as `DataOptions`, `Stage1Options`, `Stage2Options`, `GaOptions`, `GenomeOptions`, `RngOptions`, and `OutputOptions`.

`ConfigLoader.cs` loads JSON configuration files and validates important constraints, especially search mode and Stage 2 timing settings.

Configuration paths are resolved by the application relative to the configuration file directory.

### `Data`

The `Data` module handles PGN ingestion and TSV training samples.

Important files:

```text
PgnGame.cs
PgnGameReader.cs
PgnIngestor.cs
PgnSanTokenizer.cs
TrainingSample.cs
TrainingSampleTsv.cs
```

Stage 1 training uses samples in this format:

```text
FEN<TAB>UCI_MOVE
```

`PgnIngestor` can build train/test TSV files from PGN input. The ingestion logic labels moves according to the configured data options.

### `Engine`

The `Engine` module contains the board abstraction and search code.

Important files:

```text
IBoardAdapter.cs
ChessDotNetBoardAdapter.cs
EngineCore.cs
IEvaluator.cs
Move.cs
```

`IBoardAdapter` defines the minimal board interface used by the engine. This keeps the search logic independent from the concrete chess library.

`ChessDotNetBoardAdapter` adapts the underlying chess library to `IBoardAdapter`.

`UciMove` represents chess moves in UCI format, for example:

```text
e2e4
g1f3
e7e8q
```

`EngineCore` implements one-ply move selection and alpha-beta search. The search supports fixed depth, node cap, and time-limited modes through `SearchLimits`.

Evaluators implement `IEvaluator`, which returns a position score from White’s perspective.

### `Eval`

The `Eval` module contains the parametric chess evaluation function.

Important files:

```text
DecodedParams.cs
GenomeToParamsDecoder.cs
ParametricEvaluator.cs
```

`DecodedParams` stores concrete evaluation parameters in centipawns. Pawn value is fixed at 100 and is not evolved.

`GenomeToParamsDecoder` converts a genome bit string into `DecodedParams`.

`ParametricEvaluator` evaluates a board using material, pawn structure, king safety, mobility, rook features, knight features, weak squares, and related terms.

The evaluator returns a score from White’s perspective. Positive values are better for White, and negative values are better for Black.

### `Genome`

The `Genome` module defines the genome schema and Gray-code encoding.

Important files:

```text
ConstraintFlags.cs
GeneSchema.cs
GeneSchemaBuilder.cs
GeneSpec.cs
GrayCodec.cs
```

A genome is a bit string split into gene segments. Each gene has:

```text
name
bit width
minimum value
maximum value
optional constraints
```

`GrayCodec` encodes and decodes numeric values using Gray code. This makes neighboring encoded values more likely to differ by only one bit.

`GeneSchemaBuilder` builds a runtime `GeneSchema` from the configuration file.

### `GA`

The `GA` module contains the genetic algorithm.

Important files:

```text
GaEngine.cs
Individual.cs
```

`Individual` stores a genome and its evaluated fitness.

`GaEngine` implements:

```text
random population creation
bit-flip mutation
uniform per-gene crossover
selection
elitism
next generation creation
```

Stage 1 and Stage 2 both use the same genetic algorithm infrastructure, but compute fitness differently.

### `Persistence`

The `Persistence` module handles checkpoints and metrics.

Important files:

```text
BestGenomeCheckpoint.cs
Checkpointer.cs
MetricsWriter.cs
```

`Checkpointer` saves and loads best-genome checkpoints as JSON files.

`MetricsWriter` appends generation-level training metrics to CSV files.

Metrics are later used by the plotting script.

### `Training`

The `Training` module contains Stage 1 and Stage 2 training logic.

Important files:

```text
Stage1Trainer.cs
Stage1Driver.cs
GameRunner.cs
MatchRunner.cs
OpeningBook.cs
Stage2Driver.cs
```

`Stage1Trainer` evaluates a genome against labeled training samples. It uses one-ply search and compares the selected move with the target UCI move. Fitness is computed as:

```text
correct²
```

`Stage1Driver` runs complete Stage 1 evolution, writes metrics and checkpoints, and optionally evaluates the best genome on a test set.

`GameRunner` plays one self-play game between two evaluators.

`MatchRunner` plays several games between two evaluators and returns the score from the first evaluator’s perspective.

`OpeningBook` loads starting FENs for Stage 2 self-play.

`Stage2Driver` runs complete Stage 2 self-play evolution. It evaluates individuals through round-robin matches, computes relative fitness, writes metrics, writes checkpoints, and optionally uses a Hall of Fame.

## 3. Application commands

`ChessGP.App` contains the command-line interface.

Important files:

```text
Program.cs
EvaluateCheckpointCommand.cs
InteractivePlayCommand.cs
UciCommand.cs
```

`Program.cs` loads configuration, resolves the command, and dispatches to the appropriate command handler.

Supported commands:

```text
run-stage1
run-stage2
eval-checkpoint
play
uci
```

`EvaluateCheckpointCommand` evaluates a saved genome checkpoint on a labeled test TSV file.

`InteractivePlayCommand` allows a user to play manually against a saved checkpoint in the console.

`UciCommand` runs ChessGP as a UCI-compatible engine for chess GUIs.

## 4. Training flow

### Stage 1

Stage 1 is supervised. The engine compares its one-ply move choice against labeled training samples.

The flow is:

```text
configuration
  -> load or build train/test TSV data
  -> create initial population
  -> decode each genome
  -> evaluate on training samples
  -> save best checkpoint
  -> write metrics
  -> create next generation
```

Stage 1 is used to give the population an initial evaluation function that imitates selected moves from real games.

### Stage 2

Stage 2 is based on self-play. Genomes play games against each other, and their fitness is based on match results.

The flow is:

```text
configuration
  -> load opening positions
  -> create or warm-start population
  -> decode genomes into evaluators
  -> play round-robin matches
  -> compute relative fitness
  -> save best checkpoint
  -> write metrics
  -> create next generation
```

Stage 2 can use fixed-depth search, node-limited search, per-move time, or per-game time controls depending on the configuration.

## 5. Checkpoints and metrics

A checkpoint stores the best genome from a generation. It contains metadata such as:

```text
run ID
stage
generation
schema ID
genome bits
timestamp
```

The schema ID and genome length are important. A checkpoint should only be used with a compatible genome schema.

Metrics are written as CSV files and contain generation-level information such as:

```text
best fitness
mean fitness
median fitness
nodes evaluated
seed
elapsed time
best checkpoint path
average completed depth
average search time
time losses
```

Not every column is relevant for every stage.

## 6. Plotting script

The plotting script is:

```text
ChessGP/scripts/plot_evolution_results.py
```

It scans a result directory recursively for `metrics.csv` files and generates plots and summary CSV files.

Basic usage:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --output plots
```

Generate PDF files as well:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --output plots --also-pdf
```

The script is intentionally separate from the C# projects. It only consumes generated metrics files.

## 7. Tests

The test project is:

```text
ChessGP/
  tests/
    ChessGP.Tests/
```

The tests cover the main areas of the project:

```text
Data
Engine
Eval
GA
Genome
Persistence
Training
```

Run all tests from the repository root:

```powershell
dotnet test ChessGP
```
