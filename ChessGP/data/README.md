# Data

This folder contains data files used for the ChessGP experiments.

## Files

* `CarlsenNakamura.pgn` — PGN database used as the original source data.
* `CarlsenNakamura.train.uci.tsv` — prepared Stage 1 training samples generated from the PGN file.
* `CarlsenNakamura.test.uci.tsv` — prepared Stage 1 test samples generated from the PGN file.
* `openings.fens` — opening positions used for Stage 2 self-play experiments.

The prepared TSV files are included so the experiment configurations can be inspected together with the exact data files used for the runs.

## License

`CarlsenNakamura.pgn` originates from a third-party project by eharris733 and is distributed under the MIT License.

The original license text is included in `LICENSE.eharris733.txt`.
