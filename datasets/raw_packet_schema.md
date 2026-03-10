# Raw Decision Packet Schema

Step-per-line JSONL. Each line is one `DecisionPacket` — a single AI decision step.

## Top-Level Structure

```json
{
  "episode_id": 42,
  "step_id": 7,
  "game_complete": true,
  "obs": { ... },
  "search": { ... },
  "action": { ... },
  "chosen_plan": [ ... ],
  "candidates": [ ... ]
}
```

## `obs` — Observation at decision time

| Field | Type | Description |
|-------|------|-------------|
| `board_visible` | `int[20][10]` | Visible board rows, row 0 = bottom, 0=empty 1=occupied |
| `board_hidden` | `int[2][10]` | Hidden rows above visible (rows 20-21) |
| `current_piece` | `string` | Active piece name: `"I"`, `"O"`, `"T"`, `"L"`, `"J"`, `"S"`, `"Z"` |
| `hold_piece` | `string\|null` | Held piece name or `null` if empty |
| `next_visible` | `string[]` | Preview queue piece names |
| `can_hold` | `bool` | Whether hold is available this turn |
| `combo` | `int` | Current combo count |
| `b2b_chain` | `int` | Current back-to-back chain |
| `incoming` | `uint` | Pending garbage lines (garbage pressure context) |

## `search` — Search decision metadata

| Field | Type | Description |
|-------|------|-------------|
| `nodes` | `uint` | MCTS nodes evaluated |
| `depth` | `uint` | Search depth (generations) |
| `decision_mode` | `string` | `"Book"`, `"Normal"`, `"SurviveFilter"`, `"SpikeBackup"` |
| `chosen_idx` | `int` | Index into `candidates[]` of the chosen move |
| `primary_compare_idx` | `int\|null` | Index of the primary comparison candidate |
| `primary_compare_basis` | `string` | Why that candidate was chosen for comparison (see below) |
| `best_value_alt_idx` | `int\|null` | Best alternative by eval score |
| `best_survival_alt_idx` | `int\|null` | Best surviving alternative |
| `best_spike_alt_idx` | `int\|null` | Best alternative by spike score |
| `margin_value_vs_primary` | `int\|null` | `chosen.eval - primary.eval` |
| `margin_spike_vs_primary` | `int\|null` | `chosen.spike - primary.spike` |
| `first_survival_pass` | `bool` | Whether the top-ranked candidate passes survival |
| `any_survival_pass` | `bool` | Whether any candidate passes survival |
| `cot_eligible` | `bool` | Whether this step has enough contrast for offline CoT |

### Decision Modes

| Mode | Semantics |
|------|-----------|
| `Book` | Opening book move. No search comparison available. |
| `Normal` | Top-ranked candidate passes survival filter. Standard best-move selection. |
| `SurviveFilter` | Top-ranked fails survival; chosen is first surviving candidate in rank order. |
| `SpikeBackup` | No candidates survive; chosen is highest spike score (desperation mode). |

### Compare Basis

| Value | Meaning |
|-------|---------|
| `NONE` | No comparison candidate available |
| `BEST_OTHER_BY_RANK` | Normal mode: next-best candidate by rank |
| `BEST_FILTERED_OUT_BY_SURVIVAL` | SurviveFilter: best candidate that failed survival |
| `SECOND_BEST_SPIKE` | SpikeBackup: second-highest spike candidate |
| `BOOK` | Book mode: no search-based comparison |

## `action` — Chosen placement

| Field | Type | Description |
|-------|------|-------------|
| `hold` | `bool` | Whether hold was used |
| `placement_token` | `string` | Compact placement identifier, e.g. `"TR_3"`, `"HI0_1"` |
| `action_token` | `string` | Same as `placement_token` (unified token) |
| `cells_x` | `byte[4]` | X coordinates of the 4 cells of the placed piece |
| `cells_y` | `byte[4]` | Y coordinates of the 4 cells of the placed piece |

### Placement Token Format

`[H]{piece}{rotation}_{column}`

- `H` prefix if hold was used
- Piece: `I`, `O`, `T`, `L`, `J`, `S`, `Z`
- Rotation: `0` (north), `R` (east/CW), `2` (south), `L` (west/CCW)
- Column: leftmost column of the normalized piece shape

Example: `"HT2_3"` = hold T-piece, rotation 2 (south), column 3.

## `chosen_plan` — AI's planned future moves

Array of `PlanStepData` objects representing the AI's intended continuation.

| Field | Type | Description |
|-------|------|-------------|
| `piece` | `string` | Piece name |
| `placement_kind` | `int` | Placement type (see encoding below) |
| `lines_cleared` | `int` | Number of lines cleared |
| `garbage_sent` | `int` | Garbage lines sent |
| `b2b_after` | `bool` | B2B status after this step |
| `combo_after` | `int` | Combo count after (-1 = None/reset) |
| `cells_x` | `byte[4]` | Placement X coordinates |
| `cells_y` | `byte[4]` | Placement Y coordinates |

## `candidates` — All evaluated candidates

Array of `CandidateData` objects, ordered by search rank (best first).

| Field | Type | Description |
|-------|------|-------------|
| `idx` | `int` | Position in array |
| `piece` | `string` | Piece name |
| `hold` | `bool` | Whether this candidate uses hold |
| `cells_x` | `byte[4]` | Placement X coordinates |
| `cells_y` | `byte[4]` | Placement Y coordinates |
| `original_rank` | `int` | Rank from MCTS evaluation |
| `survival_pass` | `bool` | Whether this candidate passes the survival filter |
| `backed_up_value` | `int` | Backed-up eval score from MCTS |
| `backed_up_spike` | `int` | Backed-up spike score from MCTS |
| `lines_cleared` | `int` | Lines cleared by this placement |
| `placement_kind` | `int` | Placement type encoding |
| `b2b` | `bool` | B2B status after placement |
| `perfect_clear` | `bool` | Whether this achieves a perfect clear |
| `combo_after` | `int` | Combo count after (-1 = None/reset) |
| `garbage_sent` | `int` | Garbage sent by this placement |
| `cleared_lines` | `int[4]` | Row indices of cleared lines (-1 = unused) |
| `has_trace` | `bool` | Whether eval trace is available |
| `local_trace` | `TraceData\|null` | Eval component breakdown |
| `placement_token` | `string` | Compact placement token |
| `board_after_visible` | `int[20][10]` | Board state after placement (visible rows) |
| `board_after_hidden` | `int[2][10]` | Board state after placement (hidden rows) |
| `pv_len` | `int` | Number of PV steps (0-5) |
| `pv_steps` | `PlanStepData[]` | Principal variation (best continuation from this candidate) |

### `local_trace` — Evaluation component breakdown

| Field | Type | Description |
|-------|------|-------------|
| `clear_score` | `int` | Score from line clears |
| `tspin_score` | `int` | Score from T-spins |
| `pc_score` | `int` | Perfect clear score |
| `b2b_score` | `int` | Back-to-back bonus |
| `combo_score` | `int` | Combo bonus |
| `wasted_t` | `int` | Penalty for wasted T-pieces |
| `height_penalty` | `int` | Stack height penalty |
| `jeopardy_penalty` | `int` | Jeopardy/danger penalty |
| `well_score` | `int` | Well column score |
| `tslot_score` | `int[4]` | T-slot scores by type |
| `bumpiness_penalty` | `int` | Surface bumpiness penalty |
| `hole_penalty` | `int` | Hole count penalty |
| `covered_penalty` | `int` | Covered cells penalty |
| `row_transitions` | `int` | Row transition penalty |

## Placement Kind Encoding

| Value | Meaning |
|-------|---------|
| 0 | None (no clear) |
| 1 | Clear1 (single) |
| 2 | Clear2 (double) |
| 3 | Clear3 (triple) |
| 4 | Clear4 (tetris) |
| 5 | MiniTspin |
| 6 | MiniTspin1 |
| 7 | MiniTspin2 |
| 8 | Tspin (no clear) |
| 9 | Tspin1 |
| 10 | Tspin2 |
| 11 | Tspin3 |

## Board Encoding

- Row-major: `board[row][col]`
- Row 0 = bottom visible row, Row 19 = top visible row
- Hidden rows 20-21 are above the visible playfield
- Cell values: `0` = empty, `1` = occupied
- Origin `(0, 0)` is bottom-left

## Intended Use for Offline Rationale Generation

The `search` metadata fields (`primary_compare_idx`, `primary_compare_basis`, margins, `cot_eligible`) are designed to power offline chain-of-thought generation. A Python postprocessor can:

1. Filter steps where `cot_eligible == true`
2. Compare `candidates[chosen_idx]` vs `candidates[primary_compare_idx]`
3. Use `board_after_visible` to show board state consequences
4. Use `pv_steps` to explain planned continuations
5. Reference `local_trace` for detailed eval component comparison
6. Use `decision_mode` and `primary_compare_basis` to select the appropriate explanation template

No natural language is stored in the raw packet. All string fields are piece names, tokens, or enum labels.
