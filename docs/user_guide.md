# User Guide

This guide explains how to build, run, and use ChessGP from the command line. It is intended for users who want to train evaluation parameters, evaluate checkpoints, play against a checkpoint, run the engine in a chess GUI, or generate plots from training results.

For implementation details, see [developer_manual.md](developer_manual.md).

## 1. Requirements

ChessGP requires:

* .NET SDK
* Python 3, only for plotting results
* Python packages `pandas` and `matplotlib`, only for plotting results

Check that .NET is installed:

```powershell
dotnet --version
```

Install Python plotting dependencies:

```powershell
python3 -m pip install pandas matplotlib
```

## 2. Build and test

Run these commands from the repository root:

```powershell
dotnet restore ChessGP
dotnet build ChessGP
dotnet test ChessGP
```

To check the plotting script:

```powershell
python3 -m py_compile ChessGP/scripts/plot_evolution_results.py
```

## 3. Configuration files

ChessGP is controlled through JSON configuration files.

A configuration file defines:

* input data
* Stage 1 training settings
* Stage 2 training settings
* genetic algorithm settings
* genome schema
* random seed
* output paths

The main sections are:

```text
Data
Stage1
Stage2
GA
Genome
Rng
Output
```

Paths inside the configuration are resolved relative to the directory where the configuration file is located.

For example, if the configuration file is:

```text
configs/stage1_config.json
```

and the data path inside the configuration is:

```text
../data/train.uci.tsv
```

then the data path is interpreted relative to the `configs/` directory.

The repository also contains selected thesis experiment artifacts in `experiments/` and the data files used for the experiments in `ChessGP/data/`.

## 4. Print configuration summary

To check that a configuration file loads correctly:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- path/to/config.json
```

This prints a short summary of the loaded configuration.

## 5. Stage 1 training

Stage 1 is supervised training. The engine compares its one-ply move choice against labeled training samples.

A training sample contains:

```text
FEN<TAB>UCI_MOVE
```

Example:

```text
7k/4r3/8/8/8/8/4Q3/K7 w - - 0 1	e2e7
```

Run Stage 1:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- run-stage1 path/to/stage1_config.json
```

Stage 1 writes:

* metrics CSV file
* checkpoint files
* optional test metrics CSV file

The output paths are defined in the `Output` section of the configuration file.

## 6. Stage 2 training

Stage 2 is self-play evolution. Genomes play games against each other, and their fitness is based on match results.

Run Stage 2:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- run-stage2 path/to/stage2_config.json
```

Stage 2 can use an opening book file. The opening book is a plain text file containing FEN positions.

Example:

```text
# comments are ignored
startpos
7k/5Q2/7K/8/8/8/8/8 b - - 0 1
```

The keyword `startpos` means the standard chess starting position.

Stage 2 writes:

* metrics CSV file
* checkpoint files

## 7. Evaluate a checkpoint

Use `eval-checkpoint` to test a saved genome checkpoint on a labeled test dataset.

Basic one-ply evaluation:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- eval-checkpoint path/to/config.json path/to/checkpoint.json --test-path path/to/test.uci.tsv --max 4000 --depth 1
```

Evaluation with deeper search:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- eval-checkpoint path/to/config.json path/to/checkpoint.json --test-path path/to/test.uci.tsv --max 4000 --depth 3
```

Useful options:

```text
--test-path PATH
--max N
--depth N
--node-cap N
```

Meaning:

* `--test-path` selects the test TSV file.
* `--max` limits the number of evaluated samples.
* `--depth` selects search depth.
* `--node-cap` optionally limits searched nodes per move.

Depth `1` uses one-ply evaluation. Higher depths use alpha-beta search.

## 8. Play against a checkpoint

Use `play` to start an interactive console game against a checkpoint.

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- play path/to/config.json path/to/checkpoint.json --human white --depth 2
```

Useful options:

```text
--human white|black
--depth N
--node-cap N
--fen FEN
```

Moves are entered in UCI format:

```text
e2e4
g1f3
e7e8q
```

Available console commands during play:

```text
help
board
fen
moves
quit
```

## 9. Run as a UCI engine

ChessGP can run as a UCI-compatible chess engine. This allows it to be used in chess GUIs such as Arena.

Run UCI mode manually:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- uci path/to/config.json path/to/checkpoint.json --depth 4 --node-cap 500000
```

Useful options:

```text
--depth N
--node-cap N
--max-depth N
--move-overhead-ms N
--estimated-moves-to-go N
--minimum-move-time-ms N
--draw-contempt-cp N
--avoid-draw-when-ahead-cp N
```

Meaning:

* `--depth` sets the default search depth.
* `--node-cap` limits nodes per move.
* `--max-depth` sets the maximum depth used for time-controlled searches.
* `--move-overhead-ms` reserves time to avoid losing on time.
* `--estimated-moves-to-go` estimates how many moves remain in the game.
* `--minimum-move-time-ms` sets the minimum thinking time.
* `--draw-contempt-cp` discourages unnecessary draws.
* `--avoid-draw-when-ahead-cp` discourages draws when the engine is ahead.

## 10. UCI settings file

Some chess GUIs do not conveniently pass long command-line arguments. For this case, ChessGP supports a `uci_settings.json` file.

If the program is started without command-line arguments and `uci_settings.json` exists in the application base directory, ChessGP automatically starts in UCI mode using that file.

Example:

```json
{
  "configPath": "configs/stage2_config.json",
  "checkpointPath": "checkpoints/best.json",
  "depth": 4,
  "nodeCap": 500000,
  "maxSearchDepth": 32,
  "moveOverheadMs": 50,
  "estimatedMovesToGo": 45,
  "minimumMoveTimeMs": 20,
  "drawContemptCp": 25,
  "avoidDrawWhenAheadCp": 500
}
```

Required fields:

```text
configPath
checkpointPath
```

Optional fields:

```text
depth
nodeCap
maxSearchDepth
moveOverheadMs
estimatedMovesToGo
minimumMoveTimeMs
drawContemptCp
avoidDrawWhenAheadCp
```

## 11. Generate plots

The plotting script scans a results directory recursively for `metrics.csv` files.

Generate plots:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --output plots
```

Generate only Stage 1 plots:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --stage stage1 --output plots_stage1
```

Generate only Stage 2 plots:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --stage stage2 --output plots_stage2
```

Generate both PNG and PDF plots:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --output plots --also-pdf
```

Typical output:

```text
plots/
  all_metrics.csv
  all_stage1_test_metrics.csv
  stage1/
    stage1_summary.csv
    *.png
    *.pdf
  stage2/
    stage2_summary.csv
    *.png
    *.pdf
```

PDF files are useful for thesis figures or printed documentation.

## 12. Common workflow

A typical workflow is:

```text
1. Prepare or choose a configuration file.
2. Run dotnet test ChessGP.
3. Run Stage 1 training.
4. Inspect Stage 1 metrics and checkpoints.
5. Run Stage 2 training using selected checkpoints or random initialization.
6. Evaluate selected checkpoints.
7. Optionally test a checkpoint in interactive or UCI mode.
8. Generate plots from the result folders.
```
