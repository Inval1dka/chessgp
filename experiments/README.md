# Experiments

This folder contains selected experiment artifacts used in the thesis.

## Structure

* `stage1/` — supervised Stage 1 runs based on labeled positions generated from the PGN data.
* `stage2/` — Stage 2 self-play experiments using the corrected draw-handling version.

Each experiment folder contains the relevant configuration, metrics, and selected checkpoints.

Typical contents:

```text
run.json
metrics.csv
metrics_stage1_test.csv
checkpoints/
```

The `run.json` files preserve the configurations used for the original MetaCentrum runs. When rerunning the experiments in a different environment, file paths inside the configuration files may need to be adjusted.

`metrics_stage1_test.csv` is present only for Stage 1 runs.

## Included artifacts

The repository includes selected artifacts needed to inspect the experiments described in the thesis:

* configuration files,
* training metrics,
* test metrics for Stage 1,
* selected best/final checkpoints.

Generated plots are not stored here because they can be recreated from the metrics using:

```powershell
python3 ChessGP/scripts/plot_evolution_results.py --input experiments --output plots
```
