#!/usr/bin/env python3
import argparse
import json
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional


VALID_PIECES = {"I", "O", "T", "L", "J", "S", "Z"}
VALID_DECISION_MODES = {"Book", "Normal", "SurviveFilter", "SpikeBackup"}
VALID_COMPARE_BASIS = {
    "NONE",
    "BEST_OTHER_BY_RANK",
    "BEST_FILTERED_OUT_BY_SURVIVAL",
    "SECOND_BEST_SPIKE",
    "BOOK",
    "UNKNOWN",
}
REQUIRED_META_FIELDS = {
    "run_id": str,
    "episode_id": int,
    "step_id": int,
    "game_complete": bool,
    "collector_seed": int,
    "engine_commit": str,
    "weights_hash": str,
    "options_hash": str,
    "book_enabled": bool,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate cot_raw_v3 JSONL packets.")
    parser.add_argument("input", type=Path, help="Input JSONL file")
    return parser.parse_args()


def iter_jsonl(path: Path) -> Iterable[Dict[str, Any]]:
    with path.open("r", encoding="utf-8-sig") as handle:
        for line_no, line in enumerate(handle, start=1):
            line = line.strip()
            if not line:
                continue
            try:
                yield json.loads(line)
            except json.JSONDecodeError as exc:
                raise ValueError(f"line {line_no}: invalid JSON: {exc}") from exc


def add_error(errors: List[str], line_no: int, message: str) -> None:
    errors.append(f"line {line_no}: {message}")


def validate_board(errors: List[str], line_no: int, board: Any, rows: int, cols: int, name: str) -> None:
    if not isinstance(board, list) or len(board) != rows:
        add_error(errors, line_no, f"{name} must be [{rows}][{cols}]")
        return
    for row in board:
        if not isinstance(row, list) or len(row) != cols:
            add_error(errors, line_no, f"{name} must be [{rows}][{cols}]")
            return


def get_chosen_candidate(engine: Dict[str, Any]) -> Optional[Dict[str, Any]]:
    candidates = engine.get("candidates") or []
    search = engine.get("search") or {}
    chosen_idx = search.get("chosen_idx")
    if not isinstance(chosen_idx, int):
        return None
    if chosen_idx < 0 or chosen_idx >= len(candidates):
        return None
    return candidates[chosen_idx]


def distinct_non_chosen_indices(candidates: List[Dict[str, Any]], chosen_action_id: str) -> List[int]:
    seen = set()
    indices: List[int] = []
    for idx, candidate in enumerate(candidates):
        action_id = candidate.get("action_id")
        if action_id == chosen_action_id:
            continue
        if action_id not in seen:
            seen.add(action_id)
            indices.append(idx)
    return indices


def validate_packet(packet: Dict[str, Any], line_no: int, errors: List[str]) -> None:
    if packet.get("schema_version") != "cot_raw_v3":
        add_error(errors, line_no, "schema_version must be cot_raw_v3")

    engine = packet.get("engine")
    meta = packet.get("collector_meta")
    if not isinstance(engine, dict):
        add_error(errors, line_no, "engine must be an object")
        return
    if not isinstance(meta, dict):
        add_error(errors, line_no, "collector_meta must be an object")
    else:
        for key, expected_type in REQUIRED_META_FIELDS.items():
            value = meta.get(key)
            if not isinstance(value, expected_type):
                add_error(errors, line_no, f"collector_meta.{key} must be {expected_type.__name__}")

    obs = engine.get("obs") or {}
    search = engine.get("search") or {}
    chosen_action = engine.get("chosen_action") or {}
    chosen_future_plan = engine.get("chosen_future_plan") or []
    candidates = engine.get("candidates") or []

    hold_piece = obs.get("hold_piece")
    if hold_piece is not None and hold_piece not in VALID_PIECES:
        add_error(errors, line_no, f"obs.hold_piece must be null or a valid piece, got {hold_piece!r}")
    if not isinstance(obs.get("hold_enabled"), bool):
        add_error(errors, line_no, "obs.hold_enabled must be a bool")

    validate_board(errors, line_no, obs.get("board_visible"), 20, 10, "engine.obs.board_visible")
    validate_board(errors, line_no, obs.get("board_hidden"), 2, 10, "engine.obs.board_hidden")

    decision_mode = search.get("decision_mode")
    if decision_mode not in VALID_DECISION_MODES:
        add_error(errors, line_no, f"invalid decision_mode {decision_mode!r}")

    compare_basis = search.get("primary_compare_basis")
    if compare_basis not in VALID_COMPARE_BASIS:
        add_error(errors, line_no, f"invalid primary_compare_basis {compare_basis!r}")

    chosen_idx = search.get("chosen_idx")
    if not isinstance(chosen_idx, int) or chosen_idx < 0 or chosen_idx >= len(candidates):
        add_error(errors, line_no, "search.chosen_idx must point into candidates")
        return

    chosen_candidate = candidates[chosen_idx]
    if chosen_action.get("action_id") != chosen_candidate.get("action_id"):
        add_error(errors, line_no, "chosen_action.action_id must match candidates[chosen_idx].action_id")

    chosen_action_id = chosen_candidate.get("action_id")
    distinct_non_chosen = distinct_non_chosen_indices(candidates, chosen_action_id)

    primary_compare_idx = search.get("primary_compare_idx")
    if primary_compare_idx is not None:
        if not isinstance(primary_compare_idx, int) or primary_compare_idx < 0 or primary_compare_idx >= len(candidates):
            add_error(errors, line_no, "primary_compare_idx must be null or point into candidates")
        elif candidates[primary_compare_idx].get("action_id") == chosen_candidate.get("action_id"):
            add_error(errors, line_no, "primary_compare_idx must point to a distinct action_id")

    candidate_count = search.get("candidate_count")
    candidate_count_exported = search.get("candidate_count_exported")
    if candidate_count != len(candidates) or candidate_count_exported != len(candidates):
        add_error(errors, line_no, "candidate_count and candidate_count_exported must match len(candidates)")

    root_action_id = chosen_action.get("action_id")
    for step in chosen_future_plan:
        if step.get("action_id") == root_action_id:
            add_error(errors, line_no, "chosen_future_plan must not include the chosen root action")
            break

    for index, candidate in enumerate(candidates):
        future_plan = candidate.get("future_plan") or []
        candidate_action_id = candidate.get("action_id")
        for step in future_plan:
            if step.get("action_id") == candidate_action_id:
                add_error(errors, line_no, f"candidate.future_plan[{index}] must not include the candidate root action")
                break

        validate_board(errors, line_no, candidate.get("board_after_visible"), 20, 10, f"candidate[{index}].board_after_visible")
        validate_board(errors, line_no, candidate.get("board_after_hidden"), 2, 10, f"candidate[{index}].board_after_hidden")

    if decision_mode == "Book":
        if compare_basis != "BOOK":
            add_error(errors, line_no, "Book decision_mode should use BOOK compare basis")
        if primary_compare_idx is not None:
            add_error(errors, line_no, "Book decision_mode must not have a primary_compare_idx")
        if search.get("cot_eligible") is not False:
            add_error(errors, line_no, "Book decision_mode must have cot_eligible=false")
    if decision_mode == "Normal":
        expected_primary = distinct_non_chosen[0] if distinct_non_chosen else None
        if compare_basis != "BEST_OTHER_BY_RANK":
            add_error(errors, line_no, "Normal decision_mode should use BEST_OTHER_BY_RANK")
        if primary_compare_idx != expected_primary:
            add_error(errors, line_no, "Normal primary_compare_idx must be the first distinct non-chosen candidate by rank")
    if decision_mode == "SurviveFilter":
        expected_primary = None
        for idx in range(chosen_idx):
            candidate = candidates[idx]
            if candidate.get("action_id") != chosen_action_id and candidate.get("survival_pass") is False:
                expected_primary = idx
                break
        if compare_basis != "BEST_FILTERED_OUT_BY_SURVIVAL":
            add_error(errors, line_no, "SurviveFilter decision_mode should use BEST_FILTERED_OUT_BY_SURVIVAL")
        if primary_compare_idx != expected_primary:
            add_error(errors, line_no, "SurviveFilter primary_compare_idx must be the first earlier-ranked distinct candidate filtered out by survival")
    if decision_mode == "SpikeBackup":
        ranked = sorted(
            distinct_non_chosen,
            key=lambda i: (candidates[i].get("backed_up_spike", 0), candidates[i].get("backed_up_value", 0), -i),
            reverse=True,
        )
        expected_primary = ranked[0] if ranked else None
        if compare_basis != "SECOND_BEST_SPIKE":
            add_error(errors, line_no, "SpikeBackup decision_mode should use SECOND_BEST_SPIKE")
        if primary_compare_idx != expected_primary:
            add_error(errors, line_no, "SpikeBackup primary_compare_idx must be the best distinct non-chosen spike alternative")
        if search.get("best_spike_alt_idx") != expected_primary:
            add_error(errors, line_no, "SpikeBackup best_spike_alt_idx must match primary_compare_idx")
    if decision_mode != "Book" and compare_basis == "BOOK":
        add_error(errors, line_no, "Non-Book decision_mode must not use BOOK compare basis")


def main() -> None:
    args = parse_args()
    errors: List[str] = []

    for line_no, packet in enumerate(iter_jsonl(args.input), start=1):
        validate_packet(packet, line_no, errors)

    if errors:
        print(f"FAILED: {len(errors)} validation error(s)")
        for error in errors[:200]:
            print(error)
        raise SystemExit(1)

    print("OK: validation passed with 0 errors")


if __name__ == "__main__":
    main()
