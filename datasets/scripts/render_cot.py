#!/usr/bin/env python3
"""Legacy v2/v2.5 CoT renderer.

This script targets the older Unity-assembled raw packet shape and is not the
canonical renderer for `cot_raw_v3`.
"""
import argparse
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, Iterable, List, Optional, Tuple


DECISION_MODE = {
    0: "Book",
    1: "Normal",
    2: "SurviveFilter",
    3: "SpikeBackup",
}

PLACEMENT_KIND = {
    0: "None",
    1: "Clear1",
    2: "Clear2",
    3: "Clear3",
    4: "Clear4",
    5: "MiniTspin",
    6: "MiniTspin1",
    7: "MiniTspin2",
    8: "Tspin",
    9: "Tspin1",
    10: "Tspin2",
    11: "Tspin3",
}


@dataclass(frozen=True)
class Atom:
    key: str
    score: int
    text: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Render offline CoT sentences from raw decision packet JSONL."
    )
    parser.add_argument("input", type=Path, help="Input raw decision packet JSONL")
    parser.add_argument(
        "--output",
        type=Path,
        default=None,
        help="Output rendered JSONL path. Defaults to <input>.rendered.jsonl",
    )
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
                raise ValueError(f"Invalid JSON on line {line_no}: {exc}") from exc


def choose_candidates(packet: Dict[str, Any]) -> Tuple[Optional[Dict[str, Any]], Optional[Dict[str, Any]]]:
    candidates = packet.get("candidates") or []
    chosen_idx = packet.get("chosen_idx", 0)
    chosen = candidates[chosen_idx] if 0 <= chosen_idx < len(candidates) else None
    if not chosen:
        return None, None

    others = [c for i, c in enumerate(candidates) if i != chosen_idx]
    others.sort(
        key=lambda c: (
            c.get("backed_up_value", -10**9),
            c.get("backed_up_spike", -10**9),
        ),
        reverse=True,
    )

    runner_up = None
    for candidate in others:
        if candidate.get("placement_token") != chosen.get("placement_token"):
            runner_up = candidate
            break
    if runner_up is None and others:
        runner_up = others[0]
    return chosen, runner_up


def infer_goal(packet: Dict[str, Any], chosen: Dict[str, Any]) -> str:
    decision_mode = DECISION_MODE.get(packet.get("decision_mode"), "Normal")
    placement_kind = PLACEMENT_KIND.get(chosen.get("placement_kind", 0), "None")
    lines_cleared = chosen.get("lines_cleared", 0)
    garbage_sent = chosen.get("garbage_sent", 0)
    combo_after = chosen.get("combo_after", -1)

    if decision_mode in {"SurviveFilter", "SpikeBackup"}:
        return "SURVIVAL"
    if "Tspin" in placement_kind or lines_cleared >= 2 or garbage_sent >= 4:
        return "ATTACK_EXEC"
    if combo_after >= 2 or chosen.get("b2b"):
        return "PRESSURE_CONTINUE"
    if traces_favor_downstack(chosen):
        return "DOWNSTACK"
    if chosen.get("hold"):
        return "PIECE_MANAGEMENT"
    return "STACK_SHAPE"


def traces_favor_downstack(chosen: Dict[str, Any]) -> bool:
    trace = chosen.get("local_trace") or {}
    height_penalty = abs(trace.get("height_penalty", 0))
    hole_penalty = abs(trace.get("hole_penalty", 0))
    covered_penalty = abs(trace.get("covered_penalty", 0))
    return height_penalty + hole_penalty + covered_penalty >= 220


def extract_atoms(chosen: Dict[str, Any], runner_up: Optional[Dict[str, Any]]) -> List[Atom]:
    if not runner_up:
        return fallback_atoms(chosen)

    c_trace = chosen.get("local_trace") or {}
    r_trace = runner_up.get("local_trace") or {}
    atoms: List[Atom] = []

    add_atom(
        atoms,
        "attack_now",
        chosen.get("backed_up_spike", 0) - runner_up.get("backed_up_spike", 0),
        "it keeps more immediate attack pressure",
    )
    add_atom(
        atoms,
        "clear_value",
        (c_trace.get("clear_score", 0) + c_trace.get("tspin_score", 0))
        - (r_trace.get("clear_score", 0) + r_trace.get("tspin_score", 0)),
        "it converts the position into a stronger clear",
    )
    add_atom(
        atoms,
        "height",
        r_trace.get("height_penalty", 0) - c_trace.get("height_penalty", 0),
        "it keeps the stack lower",
    )
    add_atom(
        atoms,
        "holes",
        r_trace.get("hole_penalty", 0) - c_trace.get("hole_penalty", 0),
        "it avoids extra holes",
    )
    add_atom(
        atoms,
        "coverage",
        r_trace.get("covered_penalty", 0) - c_trace.get("covered_penalty", 0),
        "it reduces buried cells",
    )
    add_atom(
        atoms,
        "jeopardy",
        r_trace.get("jeopardy_penalty", 0) - c_trace.get("jeopardy_penalty", 0),
        "it is safer against top-out pressure",
    )
    add_atom(
        atoms,
        "well",
        c_trace.get("well_score", 0) - r_trace.get("well_score", 0),
        "it preserves a stronger well structure",
    )
    add_atom(
        atoms,
        "bumpiness",
        r_trace.get("bumpiness_penalty", 0) - c_trace.get("bumpiness_penalty", 0),
        "it leaves a cleaner surface",
    )
    add_atom(
        atoms,
        "tslot",
        sum(c_trace.get("tslot_score", []) or [])
        - sum(r_trace.get("tslot_score", []) or []),
        "it keeps better T-slot potential",
    )

    atoms = [atom for atom in atoms if atom.score > 0]
    atoms.sort(key=lambda atom: (atom.score, atom.key), reverse=True)
    return atoms[:3] if atoms else fallback_atoms(chosen)


def add_atom(atoms: List[Atom], key: str, score: int, text: str) -> None:
    atoms.append(Atom(key=key, score=score, text=text))


def fallback_atoms(chosen: Dict[str, Any]) -> List[Atom]:
    placement_kind = PLACEMENT_KIND.get(chosen.get("placement_kind", 0), "None")
    atoms: List[Atom] = []
    if "Tspin" in placement_kind:
        atoms.append(Atom("tspin", 100, "it cashes in a T-Spin"))
    if chosen.get("lines_cleared", 0) > 0:
        atoms.append(Atom("clear", 80, "it takes an immediate line clear"))
    if chosen.get("hold"):
        atoms.append(Atom("hold", 60, "it improves piece sequencing through hold"))
    if chosen.get("survival_pass"):
        atoms.append(Atom("safe", 40, "it stays inside the survival filter"))
    if not atoms:
        atoms.append(Atom("value", 1, "it has the best backed-up value"))
    return atoms[:3]


def render_sentence(goal: str, atoms: List[Atom]) -> str:
    goal_text = {
        "SURVIVAL": "The move prioritizes survival",
        "ATTACK_EXEC": "The move prioritizes immediate attack",
        "PRESSURE_CONTINUE": "The move prioritizes keeping pressure",
        "DOWNSTACK": "The move prioritizes downstacking",
        "PIECE_MANAGEMENT": "The move prioritizes piece management",
        "STACK_SHAPE": "The move prioritizes board shape",
    }.get(goal, "The move prioritizes overall value")

    if not atoms:
        return goal_text + "."

    reason_text = join_atoms([atom.text for atom in atoms[:3]])
    return f"{goal_text} because {reason_text}."


def join_atoms(texts: List[str]) -> str:
    if len(texts) == 1:
        return texts[0]
    if len(texts) == 2:
        return f"{texts[0]} and {texts[1]}"
    return f"{texts[0]}, {texts[1]}, and {texts[2]}"


def render_packet(packet: Dict[str, Any]) -> Dict[str, Any]:
    chosen, runner_up = choose_candidates(packet)
    if not chosen:
        return {
            **packet,
            "cot_goal": "UNKNOWN",
            "cot_atoms": [],
            "cot_text": "",
        }

    goal = infer_goal(packet, chosen)
    atoms = extract_atoms(chosen, runner_up)
    return {
        **packet,
        "cot_goal": goal,
        "cot_atoms": [atom.key for atom in atoms],
        "cot_text": render_sentence(goal, atoms),
        "runner_up_token": runner_up.get("placement_token") if runner_up else None,
    }


def main() -> None:
    args = parse_args()
    input_path = args.input
    output_path = args.output or input_path.with_suffix(input_path.suffix + ".rendered")

    output_path.parent.mkdir(parents=True, exist_ok=True)

    with output_path.open("w", encoding="utf-8") as handle:
        for packet in iter_jsonl(input_path):
            rendered = render_packet(packet)
            handle.write(json.dumps(rendered, ensure_ascii=False) + "\n")

    print(f"Rendered CoT written to {output_path}")


if __name__ == "__main__":
    main()
