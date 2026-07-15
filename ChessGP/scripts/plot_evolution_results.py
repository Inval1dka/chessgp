#!/usr/bin/env python3
"""Create thesis-ready plots from ChessGP evolution metrics.

The script scans a results directory recursively. It understands the current
ChessGP output layout, for example:

runs/
  stage1_<jobid>/
    run.json
    artifacts_stage1_real_001/
      metrics.csv
      metrics_stage1_test.csv
  stage2_<jobid>/
    run.json
    artifacts_stage2_real_001/
      metrics.csv

Usage examples:
  python scripts/plot_evolution_results.py --input runs --output plots
  python scripts/plot_evolution_results.py --input export_stage1_results --output plots_stage1
  python scripts/plot_evolution_results.py --input runs --stage stage1 --output plots_stage1
  python scripts/plot_evolution_results.py --input runs --stage stage2 --output plots_stage2 --also-pdf
"""

from __future__ import annotations

import argparse
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import matplotlib

matplotlib.use("Agg")

import matplotlib.pyplot as plt
import pandas as pd


FIGURE_SIZE = (9, 5)
DEFAULT_DPI = 200
PREFERRED_STAGE2_ORDER = ["Depth 2", "Depth 3", "10s/game", "30s/game"]


@dataclass(frozen=True)
class MetricsRun:
    """Description of one metrics.csv file and its optional test metrics."""

    artifact_dir: Path
    run_dir: Path
    run_label: str
    stage: str
    metrics_path: Path
    test_metrics_path: Path | None
    run_json_path: Path | None
    seed: int | None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Create plots from ChessGP Stage 1 / Stage 2 metrics."
    )
    parser.add_argument(
        "--input",
        required=True,
        type=Path,
        help="Directory containing copied MetaCentrum runs, or an exported results directory.",
    )
    parser.add_argument(
        "--output",
        required=True,
        type=Path,
        help="Directory where plots and summary CSV files will be written.",
    )
    parser.add_argument(
        "--stage",
        choices=["all", "stage1", "stage2"],
        default="all",
        help="Which stage to plot. Default: all.",
    )
    parser.add_argument(
        "--dpi",
        type=int,
        default=DEFAULT_DPI,
        help=f"PNG resolution. Default: {DEFAULT_DPI}.",
    )
    parser.add_argument(
        "--also-pdf",
        action="store_true",
        help="Also save every plot as PDF for thesis use.",
    )
    return parser.parse_args()


def find_runs(input_dir: Path, stage_filter: str) -> list[MetricsRun]:
    """Find all metrics.csv files and describe them as MetricsRun objects."""
    runs: list[MetricsRun] = []

    for metrics_path in sorted(input_dir.rglob("metrics.csv")):
        artifact_dir = metrics_path.parent
        artifact_name = artifact_dir.name
        run_label = get_run_label(artifact_name)
        stage = detect_stage(metrics_path, artifact_name)

        if stage_filter != "all" and stage != stage_filter:
            continue

        run_dir = artifact_dir.parent
        run_json_path = run_dir / "run.json"
        if not run_json_path.exists():
            run_json_path = None

        test_metrics_path = artifact_dir / "metrics_stage1_test.csv"
        if not test_metrics_path.exists():
            test_metrics_path = None

        runs.append(
            MetricsRun(
                artifact_dir=artifact_dir,
                run_dir=run_dir,
                run_label=run_label,
                stage=stage,
                metrics_path=metrics_path,
                test_metrics_path=test_metrics_path,
                run_json_path=run_json_path,
                seed=read_seed(run_json_path),
            )
        )

    return runs


def get_run_label(artifact_name: str) -> str:
    if artifact_name.startswith("artifacts_"):
        return artifact_name.removeprefix("artifacts_")

    return artifact_name


def detect_stage(metrics_path: Path, artifact_name: str) -> str:
    metrics = pd.read_csv(metrics_path, nrows=1)

    if "stage" in metrics.columns and not metrics.empty:
        return str(metrics.loc[0, "stage"]).lower()

    if "stage1" in artifact_name.lower():
        return "stage1"

    if "stage2" in artifact_name.lower():
        return "stage2"

    return "unknown"


def read_seed(run_json_path: Path | None) -> int | None:
    """Read Rng.seed from run.json if present."""
    if run_json_path is None:
        return None

    try:
        with run_json_path.open("r", encoding="utf-8") as file:
            config = json.load(file)

        rng = config.get("Rng") or config.get("rng") or {}
        seed = rng.get("seed")

        return int(seed) if seed is not None else None
    except Exception:
        return None


def safe_label(label: str) -> str:
    """Shorten stage labels for plot legends."""
    lower_label = label.lower()

    if "stage2_drawfix_depth2" in lower_label or "drawfix_depth2" in lower_label:
        return "Depth 2"

    if "stage2_drawfix_depth3" in lower_label or "drawfix_depth3" in lower_label:
        return "Depth 3"

    if "stage2_drawfix_time10" in lower_label or "drawfix_time10" in lower_label:
        return "10s/game"

    if "stage2_drawfix_time30" in lower_label or "drawfix_time30" in lower_label:
        return "30s/game"

    match = re.search(r"(stage[12]_real_\d+|stage[12]_calibration_\d+)", label)
    if match:
        return match.group(1)

    return label


def load_metrics(runs: Iterable[MetricsRun]) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Load train/self-play metrics and optional Stage 1 test metrics."""
    metrics_frames: list[pd.DataFrame] = []
    test_frames: list[pd.DataFrame] = []

    for run in runs:
        metrics = pd.read_csv(run.metrics_path)
        add_run_metadata(metrics, run)
        metrics_frames.append(metrics)

        if run.test_metrics_path is not None:
            test_metrics = pd.read_csv(run.test_metrics_path)
            add_run_metadata(test_metrics, run)
            test_frames.append(test_metrics)

    metrics_all = pd.concat(metrics_frames, ignore_index=True) if metrics_frames else pd.DataFrame()
    test_all = pd.concat(test_frames, ignore_index=True) if test_frames else pd.DataFrame()

    return metrics_all, test_all


def add_run_metadata(dataframe: pd.DataFrame, run: MetricsRun) -> None:
    dataframe["sourceRun"] = run.run_label
    dataframe["sourceRunShort"] = safe_label(run.run_label)
    dataframe["artifactDir"] = str(run.artifact_dir)
    dataframe["seedFromConfig"] = run.seed


def save_plot(output_dir: Path, name: str, dpi: int, also_pdf: bool) -> None:
    """Save current matplotlib figure as PNG and optionally PDF."""
    output_dir.mkdir(parents=True, exist_ok=True)

    plt.tight_layout()
    plt.savefig(output_dir / f"{name}.png", dpi=dpi)

    if also_pdf:
        plt.savefig(output_dir / f"{name}.pdf")

    plt.close()


def format_axes(title: str, xlabel: str, ylabel: str) -> None:
    """Apply common labels and readable grid style."""
    plt.title(title)
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    plt.grid(True, alpha=0.3)


def plot_stage1(
    metrics: pd.DataFrame,
    test_metrics: pd.DataFrame,
    output_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    """Create Stage 1 plots."""
    stage1 = metrics[metrics["stage"] == "stage1"].copy()
    if stage1.empty:
        return

    stage1_dir = output_dir / "stage1"
    stage1_dir.mkdir(parents=True, exist_ok=True)

    plot_stage1_best_fitness_runs(stage1, stage1_dir, dpi, also_pdf)
    plot_stage1_average_training_fitness(stage1, stage1_dir, dpi, also_pdf)
    plot_stage1_best_fitness_mean_std(stage1, stage1_dir, dpi, also_pdf)
    plot_stage1_runtime(stage1, stage1_dir, dpi, also_pdf)
    plot_stage1_total_runtime(stage1, stage1_dir, dpi, also_pdf)
    plot_stage1_nodes(stage1, stage1_dir, dpi, also_pdf)

    if not test_metrics.empty:
        plot_stage1_test_metrics(stage1, test_metrics, stage1_dir, dpi, also_pdf)


def plot_stage1_best_fitness_runs(
    stage1: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    plt.figure(figsize=FIGURE_SIZE)

    for label, group in stage1.groupby("sourceRunShort"):
        group = group.sort_values("generation")
        plt.plot(group["generation"], group["bestFitness"], label=label, linewidth=1.3)

    format_axes("Stage 1: best training fitness per run", "Generation", "Best fitness")
    plt.legend(fontsize=8)
    save_plot(stage1_dir, "stage1_best_fitness_all_runs", dpi, also_pdf)


def plot_stage1_average_training_fitness(
    stage1: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    summary = (
        stage1.groupby("generation")[["bestFitness", "meanFitness", "medianFitness"]]
        .mean()
        .reset_index()
    )

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["bestFitness"], label="Average best fitness")
    plt.plot(summary["generation"], summary["meanFitness"], label="Average mean fitness")
    plt.plot(summary["generation"], summary["medianFitness"], label="Average median fitness")

    format_axes("Stage 1: average training fitness", "Generation", "Fitness")
    plt.legend()
    save_plot(stage1_dir, "stage1_average_training_fitness", dpi, also_pdf)


def plot_stage1_best_fitness_mean_std(
    stage1: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    summary = (
        stage1.groupby("generation")["bestFitness"]
        .agg(["mean", "std"])
        .reset_index()
    )
    summary["std"] = summary["std"].fillna(0)

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["mean"], label="Mean best fitness")
    plt.fill_between(
        summary["generation"],
        summary["mean"] - summary["std"],
        summary["mean"] + summary["std"],
        alpha=0.2,
        label="±1 standard deviation",
    )

    format_axes("Stage 1: mean best training fitness across runs", "Generation", "Best fitness")
    plt.legend()
    save_plot(stage1_dir, "stage1_best_fitness_mean_std", dpi, also_pdf)


def plot_stage1_runtime(
    stage1: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    summary = (
        stage1.assign(elapsedMinutes=stage1["elapsedMs"] / 60000.0)
        .groupby("generation")["elapsedMinutes"]
        .agg(["mean", "std"])
        .reset_index()
    )
    summary["std"] = summary["std"].fillna(0)

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["mean"], label="Mean elapsed time")
    plt.fill_between(
        summary["generation"],
        summary["mean"] - summary["std"],
        summary["mean"] + summary["std"],
        alpha=0.2,
        label="±1 standard deviation",
    )

    format_axes("Stage 1: runtime per generation", "Generation", "Minutes")
    plt.legend()
    save_plot(stage1_dir, "stage1_runtime_per_generation", dpi, also_pdf)


def plot_stage1_total_runtime(
    stage1: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    total_runtime = (
        stage1.assign(elapsedHours=stage1["elapsedMs"] / 3600000.0)
        .groupby("sourceRunShort")["elapsedHours"]
        .sum()
        .sort_index()
    )

    plt.figure(figsize=FIGURE_SIZE)
    total_runtime.plot(kind="bar")

    format_axes("Stage 1: total runtime per run", "Run", "Hours")
    plt.xticks(rotation=45, ha="right")
    save_plot(stage1_dir, "stage1_total_runtime_per_run", dpi, also_pdf)


def plot_stage1_nodes(
    stage1: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    summary = (
        stage1.groupby("generation")["nodesEvaluated"]
        .mean()
        .reset_index()
    )

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["nodesEvaluated"], label="Mean nodes evaluated")

    format_axes("Stage 1: evaluated nodes per generation", "Generation", "Nodes evaluated")
    plt.legend()
    save_plot(stage1_dir, "stage1_nodes_per_generation", dpi, also_pdf)


def plot_stage1_test_metrics(
    stage1: pd.DataFrame,
    test_metrics: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    test = test_metrics.copy()

    plot_stage1_test_accuracy_runs(test, stage1_dir, dpi, also_pdf)
    plot_stage1_test_accuracy_mean_std(test, stage1_dir, dpi, also_pdf)
    plot_stage1_final_test_accuracy(test, stage1_dir, dpi, also_pdf)

    best_test = plot_stage1_best_test_accuracy(test, stage1_dir, dpi, also_pdf)

    plot_stage1_normalized_train_vs_test(stage1, test, stage1_dir, dpi, also_pdf)
    save_stage1_summary(stage1, test, best_test, stage1_dir)


def plot_stage1_test_accuracy_runs(
    test: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    plt.figure(figsize=FIGURE_SIZE)

    for label, group in test.groupby("sourceRunShort"):
        group = group.sort_values("generation")
        plt.plot(group["generation"], group["testAccuracy"], label=label, linewidth=1.3)

    format_axes("Stage 1: test accuracy per run", "Generation", "Test accuracy")
    plt.legend(fontsize=8)
    save_plot(stage1_dir, "stage1_test_accuracy_all_runs", dpi, also_pdf)


def plot_stage1_test_accuracy_mean_std(
    test: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    summary = (
        test.groupby("generation")["testAccuracy"]
        .agg(["mean", "std"])
        .reset_index()
    )
    summary["std"] = summary["std"].fillna(0)

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["mean"], label="Mean test accuracy")
    plt.fill_between(
        summary["generation"],
        summary["mean"] - summary["std"],
        summary["mean"] + summary["std"],
        alpha=0.2,
        label="±1 standard deviation",
    )

    format_axes("Stage 1: mean test accuracy across runs", "Generation", "Test accuracy")
    plt.legend()
    save_plot(stage1_dir, "stage1_test_accuracy_mean_std", dpi, also_pdf)


def plot_stage1_final_test_accuracy(
    test: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    final_test = (
        test.sort_values("generation")
        .groupby("sourceRunShort")
        .tail(1)
        .sort_values("sourceRunShort")
    )

    plt.figure(figsize=FIGURE_SIZE)
    plt.bar(final_test["sourceRunShort"], final_test["testAccuracy"])

    format_axes("Stage 1: final test accuracy per run", "Run", "Final test accuracy")
    plt.xticks(rotation=45, ha="right")
    save_plot(stage1_dir, "stage1_final_test_accuracy_per_run", dpi, also_pdf)


def plot_stage1_best_test_accuracy(
    test: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> pd.Series:
    best_test = (
        test.groupby("sourceRunShort")["testAccuracy"]
        .max()
        .sort_index()
    )

    plt.figure(figsize=FIGURE_SIZE)
    best_test.plot(kind="bar")

    format_axes("Stage 1: best observed test accuracy per run", "Run", "Best test accuracy")
    plt.xticks(rotation=45, ha="right")
    save_plot(stage1_dir, "stage1_best_test_accuracy_per_run", dpi, also_pdf)

    return best_test


def plot_stage1_normalized_train_vs_test(
    stage1: pd.DataFrame,
    test: pd.DataFrame,
    stage1_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    joined = stage1[["sourceRunShort", "generation", "bestFitness"]].merge(
        test[["sourceRunShort", "generation", "testAccuracy"]],
        on=["sourceRunShort", "generation"],
        how="inner",
    )

    normalized_parts: list[pd.DataFrame] = []

    for _, group in joined.groupby("sourceRunShort"):
        group = group.sort_values("generation").copy()
        group["bestFitnessNorm"] = normalize_series(group["bestFitness"])
        group["testAccuracyNorm"] = normalize_series(group["testAccuracy"])
        normalized_parts.append(group)

    if not normalized_parts:
        return

    normalized = pd.concat(normalized_parts, ignore_index=True)
    summary = (
        normalized.groupby("generation")[["bestFitnessNorm", "testAccuracyNorm"]]
        .mean()
        .reset_index()
    )

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["bestFitnessNorm"], label="Normalized best training fitness")
    plt.plot(summary["generation"], summary["testAccuracyNorm"], label="Normalized test accuracy")

    format_axes("Stage 1: normalized train/test improvement", "Generation", "Normalized value")
    plt.legend()
    save_plot(stage1_dir, "stage1_normalized_train_vs_test", dpi, also_pdf)


def save_stage1_summary(
    stage1: pd.DataFrame,
    test: pd.DataFrame,
    best_test: pd.Series,
    stage1_dir: Path,
) -> None:
    final_test = (
        test.sort_values("generation")
        .groupby("sourceRunShort")
        .tail(1)
        .sort_values("sourceRunShort")
    )

    final_train = (
        stage1.sort_values("generation")
        .groupby("sourceRunShort")
        .tail(1)[
            [
                "sourceRunShort",
                "bestFitness",
                "meanFitness",
                "medianFitness",
                "elapsedMs",
                "seedFromConfig",
            ]
        ]
    )
    final_train = final_train.rename(columns={"elapsedMs": "finalGenerationElapsedMs"})

    summary = final_test[
        ["sourceRunShort", "generation", "testAccuracy", "testCorrect", "testTotal"]
    ].merge(
        final_train,
        on="sourceRunShort",
        how="left",
    )

    best_test_df = best_test.rename("bestObservedTestAccuracy").reset_index()
    summary = summary.merge(best_test_df, on="sourceRunShort", how="left")
    summary = summary.sort_values("sourceRunShort")

    summary.to_csv(stage1_dir / "stage1_summary.csv", index=False)


def normalize_series(series: pd.Series) -> pd.Series:
    """Normalize one series to [0, 1]; return zeros if it is constant."""
    low = float(series.min())
    high = float(series.max())

    if math.isclose(low, high):
        return pd.Series([0.0] * len(series), index=series.index)

    return (series - low) / (high - low)


def plot_stage2(metrics: pd.DataFrame, output_dir: Path, dpi: int, also_pdf: bool) -> None:
    """Create Stage 2 plots."""
    stage2 = metrics[metrics["stage"] == "stage2"].copy()
    if stage2.empty:
        return

    stage2_dir = output_dir / "stage2"
    stage2_dir.mkdir(parents=True, exist_ok=True)

    plot_stage2_best_fitness_runs(stage2, stage2_dir, dpi, also_pdf)
    plot_stage2_average_fitness(stage2, stage2_dir, dpi, also_pdf)
    plot_stage2_runtime(stage2, stage2_dir, dpi, also_pdf)
    plot_stage2_nodes(stage2, stage2_dir, dpi, also_pdf)
    save_stage2_summary(stage2, stage2_dir)


def plot_stage2_best_fitness_runs(
    stage2: pd.DataFrame,
    stage2_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    plt.figure(figsize=FIGURE_SIZE)

    plotted_labels: set[str] = set()

    for label in PREFERRED_STAGE2_ORDER:
        group = stage2[stage2["sourceRunShort"] == label]
        if group.empty:
            continue

        group = group.sort_values("generation")
        plt.plot(group["generation"], group["bestFitness"], label=label, linewidth=1.3)
        plotted_labels.add(label)

    for label, group in stage2.groupby("sourceRunShort"):
        if label in plotted_labels:
            continue

        group = group.sort_values("generation")
        plt.plot(group["generation"], group["bestFitness"], label=label, linewidth=1.3)

    format_axes("Stage 2: best self-play fitness per run", "Generation", "Best fitness")
    plt.legend(fontsize=8)
    save_plot(stage2_dir, "stage2_best_fitness_all_runs", dpi, also_pdf)


def plot_stage2_average_fitness(
    stage2: pd.DataFrame,
    stage2_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    summary = (
        stage2.groupby("generation")[["bestFitness", "meanFitness", "medianFitness"]]
        .mean()
        .reset_index()
    )

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["bestFitness"], label="Average best fitness")
    plt.plot(summary["generation"], summary["meanFitness"], label="Average mean fitness")
    plt.plot(summary["generation"], summary["medianFitness"], label="Average median fitness")

    format_axes("Stage 2: average self-play fitness", "Generation", "Fitness")
    plt.legend()
    save_plot(stage2_dir, "stage2_average_fitness", dpi, also_pdf)


def plot_stage2_runtime(
    stage2: pd.DataFrame,
    stage2_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    summary = (
        stage2.assign(elapsedMinutes=stage2["elapsedMs"] / 60000.0)
        .groupby("generation")["elapsedMinutes"]
        .agg(["mean", "std"])
        .reset_index()
    )
    summary["std"] = summary["std"].fillna(0)

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["mean"], label="Mean elapsed time")
    plt.fill_between(
        summary["generation"],
        summary["mean"] - summary["std"],
        summary["mean"] + summary["std"],
        alpha=0.2,
        label="±1 standard deviation",
    )

    format_axes("Stage 2: runtime per generation", "Generation", "Minutes")
    plt.legend()
    save_plot(stage2_dir, "stage2_runtime_per_generation", dpi, also_pdf)


def plot_stage2_nodes(
    stage2: pd.DataFrame,
    stage2_dir: Path,
    dpi: int,
    also_pdf: bool,
) -> None:
    summary = (
        stage2.groupby("generation")["nodesEvaluated"]
        .mean()
        .reset_index()
    )

    plt.figure(figsize=FIGURE_SIZE)
    plt.plot(summary["generation"], summary["nodesEvaluated"], label="Mean nodes evaluated")

    format_axes("Stage 2: evaluated nodes per generation", "Generation", "Nodes evaluated")
    plt.legend()
    save_plot(stage2_dir, "stage2_nodes_per_generation", dpi, also_pdf)


def save_stage2_summary(stage2: pd.DataFrame, stage2_dir: Path) -> None:
    final = (
        stage2.sort_values("generation")
        .groupby("sourceRunShort")
        .tail(1)[
            [
                "sourceRunShort",
                "generation",
                "bestFitness",
                "meanFitness",
                "medianFitness",
                "elapsedMs",
                "seedFromConfig",
            ]
        ]
        .sort_values("sourceRunShort")
    )

    best = (
        stage2.groupby("sourceRunShort")["bestFitness"]
        .max()
        .rename("bestObservedFitness")
        .reset_index()
    )

    summary = final.merge(best, on="sourceRunShort", how="left")
    summary.to_csv(stage2_dir / "stage2_summary.csv", index=False)


def main() -> int:
    args = parse_args()

    input_dir = args.input.resolve()
    output_dir = args.output.resolve()

    if not input_dir.exists():
        raise SystemExit(f"Input directory does not exist: {input_dir}")

    runs = find_runs(input_dir, args.stage)
    if not runs:
        raise SystemExit(f"No metrics.csv files found under: {input_dir}")

    metrics, test_metrics = load_metrics(runs)

    output_dir.mkdir(parents=True, exist_ok=True)

    metrics.to_csv(output_dir / "all_metrics.csv", index=False)

    if not test_metrics.empty:
        test_metrics.to_csv(output_dir / "all_stage1_test_metrics.csv", index=False)

    plot_stage1(metrics, test_metrics, output_dir, args.dpi, args.also_pdf)
    plot_stage2(metrics, output_dir, args.dpi, args.also_pdf)

    print(f"Found {len(runs)} metrics files.")
    print(f"Wrote plots and summary CSVs to: {output_dir}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
