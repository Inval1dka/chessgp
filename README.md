# ChessGP

ChessGP is a C#/.NET research project for evolving chess evaluation parameters with a genetic algorithm. It contains a small chess engine core, a parametric evaluation function, supervised training on labeled chess positions, self-play evolution, checkpoint persistence, UCI support, and plotting scripts for training results.

The goal of the project is not to compete with modern chess engines, but to experiment with automatically tuning a handcrafted chess evaluation function.

## Features

* Parametric chess evaluation function
* Gray-code genome encoding of evaluation parameters
* Genetic algorithm with mutation, crossover, selection, and elitism
* Stage 1 supervised training on `(FEN, UCI move)` samples
* Stage 2 self-play evolution
* Alpha-beta search with fixed-depth, node-cap, and time-based modes
* Checkpoint and metrics persistence
* UCI mode for chess GUIs such as Arena
* Interactive console play against a checkpoint
* Plot generation from training metrics
* Unit and smoke tests

## Requirements

* .NET SDK
* Python 3, only for plotting results
* Python packages for plotting:

```powershell
python3 -m pip install pandas matplotlib
```

## Build and test

```powershell
dotnet restore ChessGP
dotnet build ChessGP
dotnet test ChessGP
```

Check the plotting script:

```powershell
python3 -m py_compile ChessGP/scripts/plot_evolution_results.py
```

## Basic usage

Print a configuration summary:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- path/to/config.json
```

Run Stage 1 training:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- run-stage1 path/to/config.json
```

Run Stage 2 training:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- run-stage2 path/to/config.json
```

Evaluate a checkpoint:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- eval-checkpoint path/to/config.json path/to/checkpoint.json --test-path path/to/test.uci.tsv --max 4000 --depth 1
```

Play against a checkpoint:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- play path/to/config.json path/to/checkpoint.json --human white --depth 2
```

Run as a UCI engine:

```powershell
dotnet run --project ChessGP/src/ChessGP.App -- uci path/to/config.json path/to/checkpoint.json --depth 4 --node-cap 500000
```

Generate plots:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --output plots
```

Generate both PNG and PDF plots:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --output plots --also-pdf
```

## Data and experiments

The repository includes the data files used for the experiments in `ChessGP/data/` and selected experiment artifacts in `experiments/`. The experiment artifacts include configuration files, metrics, and selected checkpoints used for the thesis results.

## Documentation

More detailed documentation is available in:

- [User guide](docs/user_guide.md)
- [Developer manual](docs/developer_manual.md)

The user guide explains how to run training, evaluate checkpoints, use UCI mode, and generate plots.

The developer manual explains the code structure, important classes, configuration system, genome encoding, evaluator, training pipeline, persistence, and testing approach.

## Academic context

This project was developed as part of my bachelor's thesis **Genetic Algorithms for Evolving Chess Evaluation Functions**, defended at Charles University, Faculty of Mathematics and Physics, in 2026.

The thesis discusses the motivation, design, implementation, experiments, and results, while this repository provides the source code, documentation, datasets, and selected experiment artifacts.

- **[Read the full bachelor's thesis (PDF)](docs/bachelor_thesis.pdf)**
- [Official thesis record – Charles University Digital Repository](https://dspace.cuni.cz/handle/20.500.11956/213680)
