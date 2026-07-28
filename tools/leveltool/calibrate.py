"""Sunny Stop — one-off calibration of the difficulty sigmoid.

The raw difficulty signal (solver.raw_score) is unbounded and its useful range
depends on which mechanics exist. This script samples what the generator can
actually produce at each point on the ladder, then fits the logistic constants
so the reachable band maps onto the target curve.

Run it once after changing the generator, the mechanic set, or the raw weights,
then paste the printed constants into solver.py.

    python3 calibrate.py [--samples 30]
"""

from __future__ import annotations

import argparse
import math
import random

from curve import params_for, target_mds
from generator import GenerationFailure, generate_candidate
from solver import analyse

SAMPLE_LEVELS = [1, 5, 10, 20, 35, 50, 70, 90, 110, 130, 150, 170, 200]


def sample_raw(n: int, samples: int, rng: random.Random) -> list[float]:
    p = params_for(n)
    out = []
    for _ in range(samples):
        try:
            level = generate_candidate(rng, p)
            a = analyse(level)
        except (GenerationFailure, RuntimeError):
            continue
        if a.solvable:
            out.append(a.raw)
    return out


def percentile(values: list[float], q: float) -> float:
    if not values:
        return 0.0
    s = sorted(values)
    i = min(len(s) - 1, max(0, int(round(q * (len(s) - 1)))))
    return s[i]


def fit(points: list[tuple[float, float]]) -> tuple[float, float, float]:
    """Least-squares fit of centre/slope so 100*sigmoid(raw) tracks target."""
    best = None
    for center in [x / 100 for x in range(40, 200)]:
        for slope in [x / 200 for x in range(20, 160)]:
            err = 0.0
            for raw, target in points:
                mds = 100.0 / (1.0 + math.exp(-(raw - center) / slope))
                err += (mds - target) ** 2
            if best is None or err < best[0]:
                best = (err, center, slope)
    assert best is not None
    return best[1], best[2], math.sqrt(best[0] / len(points))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--samples", type=int, default=30)
    args = ap.parse_args()

    rng = random.Random(4242)
    points: list[tuple[float, float]] = []
    print(f"{'lvl':>4} {'target':>7} {'raw p50':>8} {'raw p90':>8} {'n':>4}")
    print("-" * 36)
    for n in SAMPLE_LEVELS:
        raws = sample_raw(n, args.samples, rng)
        if not raws:
            print(f"{n:>4}   no candidates")
            continue
        # A curated level uses a hard-ish candidate, not the median one.
        p90 = percentile(raws, 0.90)
        print(f"{n:>4} {target_mds(n):>7.1f} {percentile(raws, 0.5):>8.3f} "
              f"{p90:>8.3f} {len(raws):>4}")
        points.append((p90, target_mds(n)))

    center, slope, rmse = fit(points)
    print(f"\nfitted:  SIGMOID_CENTER = {center:.3f}   SIGMOID_SLOPE = {slope:.3f}")
    print(f"rmse against target curve: {rmse:.1f} MDS points\n")
    print("resulting mapping:")
    for raw, target in points:
        mds = 100.0 / (1.0 + math.exp(-(raw - center) / slope))
        print(f"  raw {raw:>6.3f} -> MDS {mds:>5.1f}   (target {target:>5.1f})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
