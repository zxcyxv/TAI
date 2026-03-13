#!/usr/bin/env python3
"""Legacy rendered-CoT analyzer for the older Unity-assembled packet format."""
import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Dict, Iterable, List


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Analyze rendered CoT JSONL.")
    parser.add_argument("input", type=Path, help="Rendered JSONL path")
    parser.add_argument(
        "--recent-episode",
        type=int,
        default=None,
        help="If set, also print stats for a specific episode_id",
    )
    return parser.parse_args()


def iter_jsonl(path: Path) -> Iterable[Dict[str, Any]]:
    with path.open("r", encoding="utf-8-sig") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            yield json.loads(line)


def summarize(rows: List[Dict[str, Any]]) -> Dict[str, Any]:
    total = len(rows)
    goal_counter = Counter()
    atom_counter = Counter()
    atom_combo_counter = Counter()
    decision_counter = Counter()
    safe_by_goal = Counter()
    blank_text = 0
    safe_count = 0
    safe_only_count = 0
    hold_count = 0
    chosen_hold_by_goal = Counter()

    for row in rows:
        goal = row.get("cot_goal", "UNKNOWN")
        atoms = row.get("cot_atoms") or []
        decision_mode = row.get("decision_mode")
        action_token = row.get("action_token", "")
        uses_hold = action_token.startswith("H")

        goal_counter[goal] += 1
        decision_counter[decision_mode] += 1
        atom_counter.update(atoms)
        atom_combo_counter[",".join(atoms)] += 1
        if "safe" in atoms:
            safe_count += 1
            safe_by_goal[goal] += 1
        if atoms == ["safe"]:
            safe_only_count += 1
        if not row.get("cot_text"):
            blank_text += 1
        if uses_hold:
            hold_count += 1
            chosen_hold_by_goal[goal] += 1

    return {
        "total": total,
        "blank_text": blank_text,
        "safe_count": safe_count,
        "safe_pct": pct(safe_count, total),
        "safe_only_count": safe_only_count,
        "safe_only_pct": pct(safe_only_count, total),
        "hold_count": hold_count,
        "hold_pct": pct(hold_count, total),
        "goal_counter": goal_counter,
        "atom_counter": atom_counter,
        "atom_combo_counter": atom_combo_counter,
        "decision_counter": decision_counter,
        "safe_by_goal": safe_by_goal,
        "chosen_hold_by_goal": chosen_hold_by_goal,
    }


def pct(value: int, total: int) -> float:
    if total == 0:
        return 0.0
    return round(100.0 * value / total, 2)


def print_summary(label: str, stats: Dict[str, Any]) -> None:
    print(f"[{label}]")
    print(f"total={stats['total']}")
    print(f"blank_text={stats['blank_text']}")
    print(f"safe_count={stats['safe_count']} ({stats['safe_pct']}%)")
    print(f"safe_only_count={stats['safe_only_count']} ({stats['safe_only_pct']}%)")
    print(f"hold_count={stats['hold_count']} ({stats['hold_pct']}%)")
    print("decision_modes=" + fmt_counter(stats["decision_counter"]))
    print("goals=" + fmt_counter(stats["goal_counter"]))
    print("top_atoms=" + fmt_counter(stats["atom_counter"], limit=10))
    print("safe_by_goal=" + fmt_counter(stats["safe_by_goal"]))
    print("hold_by_goal=" + fmt_counter(stats["chosen_hold_by_goal"]))
    print("top_atom_combos=" + fmt_counter(stats["atom_combo_counter"], limit=10))
    print()


def fmt_counter(counter: Counter, limit: int = None) -> str:
    items = counter.most_common(limit)
    return "; ".join(f"{key}:{value}" for key, value in items)


def main() -> None:
    args = parse_args()
    rows = list(iter_jsonl(args.input))
    print_summary("all", summarize(rows))

    if args.recent_episode is not None:
        filtered = [row for row in rows if row.get("episode_id") == args.recent_episode]
        print_summary(f"episode_{args.recent_episode}", summarize(filtered))


if __name__ == "__main__":
    main()
