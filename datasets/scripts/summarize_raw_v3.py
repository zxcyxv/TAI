#!/usr/bin/env python3
import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any, Dict, Iterable, Optional


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Summarize cot_raw_v3 JSONL packets.")
    parser.add_argument("input", type=Path, help="Input JSONL file")
    return parser.parse_args()


def iter_jsonl(path: Path) -> Iterable[Dict[str, Any]]:
    with path.open("r", encoding="utf-8-sig") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            yield json.loads(line)


def chosen_candidate(engine: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    candidates = engine.get("candidates") or []
    search = engine.get("search") or {}
    chosen_idx = search.get("chosen_idx")
    if not isinstance(chosen_idx, int):
        return None
    if chosen_idx < 0 or chosen_idx >= len(candidates):
        return None
    return candidates[chosen_idx]


def main() -> None:
    args = parse_args()

    decision_modes = Counter()
    compare_basis = Counter()
    candidate_counts = Counter()
    chosen_lines = Counter()
    chosen_placement_kind = Counter()
    chosen_pc = Counter()
    chosen_hold = Counter()
    chosen_future_depth = Counter()
    candidate_future_depth = Counter()

    total = 0
    cot_eligible = 0
    total_candidates = 0
    total_distinct_candidates = 0

    for packet in iter_jsonl(args.input):
        if packet.get("schema_version") != "cot_raw_v3":
            continue

        engine = packet.get("engine") or {}
        search = engine.get("search") or {}
        action = engine.get("chosen_action") or {}
        chosen = chosen_candidate(engine)
        candidates = engine.get("candidates") or []

        total += 1
        total_candidates += len(candidates)
        total_distinct_candidates += len({candidate.get("action_id") for candidate in candidates})
        decision_modes[search.get("decision_mode", "UNKNOWN")] += 1
        compare_basis[search.get("primary_compare_basis", "UNKNOWN")] += 1
        candidate_counts[len(candidates)] += 1
        if search.get("cot_eligible"):
            cot_eligible += 1

        chosen_hold["hold_used" if action.get("hold_used") else "no_hold"] += 1
        chosen_future_depth[len(engine.get("chosen_future_plan") or [])] += 1

        for candidate in candidates:
            candidate_future_depth[len(candidate.get("future_plan") or [])] += 1

        if chosen is not None:
            immediate = chosen.get("immediate") or {}
            chosen_lines[immediate.get("lines_cleared", 0)] += 1
            chosen_placement_kind[immediate.get("placement_kind", "UNKNOWN")] += 1
            chosen_pc["pc" if immediate.get("perfect_clear") else "not_pc"] += 1

    if total == 0:
        print("No cot_raw_v3 packets found.")
        return

    avg_candidates = total_candidates / total
    avg_distinct_candidates = total_distinct_candidates / total

    print(f"packets: {total}")
    print(f"candidate_count: min={min(candidate_counts)} max={max(candidate_counts)} avg={avg_candidates:.2f}")
    print(f"distinct_candidate_count_avg: {avg_distinct_candidates:.2f}")
    print(f"cot_eligible_fraction: {cot_eligible / total:.4f}")
    print()
    print("decision_mode_distribution:")
    for key, value in decision_modes.most_common():
        print(f"  {key}: {value}")
    print()
    print("compare_basis_distribution:")
    for key, value in compare_basis.most_common():
        print(f"  {key}: {value}")
    print()
    print("hold_usage_distribution:")
    for key, value in chosen_hold.most_common():
        print(f"  {key}: {value}")
    print()
    print("chosen_lines_cleared_distribution:")
    for key, value in sorted(chosen_lines.items()):
        print(f"  {key}: {value}")
    print()
    print("chosen_placement_kind_distribution:")
    for key, value in chosen_placement_kind.most_common():
        print(f"  {key}: {value}")
    print()
    print("perfect_clear_distribution:")
    for key, value in chosen_pc.most_common():
        print(f"  {key}: {value}")
    print()
    print("chosen_future_plan_depth_distribution:")
    for key, value in sorted(chosen_future_depth.items()):
        print(f"  {key}: {value}")
    print()
    print("candidate_future_plan_depth_distribution:")
    for key, value in sorted(candidate_future_depth.items()):
        print(f"  {key}: {value}")


if __name__ == "__main__":
    main()
