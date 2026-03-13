# Raw V3 Schema

`cot_raw_v3` is the canonical raw decision packet format. Each JSONL line wraps an engine packet built in Rust plus collection metadata added in Unity.

## Top-Level Wrapper

```json
{
  "schema_version": "cot_raw_v3",
  "engine": { "...": "RawDecisionPacketV3" },
  "collector_meta": {
    "run_id": "20260313_123456_deadbeef",
    "episode_id": 42,
    "step_id": 7,
    "game_complete": true,
    "collector_seed": 1337,
    "engine_commit": "unknown",
    "weights_hash": "abc123",
    "options_hash": "def456",
    "book_enabled": false
  }
}
```

## `engine`

```json
{
  "obs": { ... },
  "search": { ... },
  "chosen_action": { ... },
  "chosen_future_plan": [ ... ],
  "candidates": [ ... ]
}
```

### `obs`

| Field | Type | Semantics |
|---|---|---|
| `board_visible` | `u8[20][10]` | Visible playfield. Row `0` is the bottom row. |
| `board_hidden` | `u8[2][10]` | Hidden rows above the visible field. |
| `current_piece` | `string` | Current active piece. |
| `hold_piece` | `string|null` | Held piece, or `null` when hold is empty. Never `"None"`. |
| `next_visible` | `string[]` | Preview queue after the current piece. |
| `hold_enabled` | `bool` | Whether hold is enabled in the engine/options for this run. This is not Unity-style per-turn hold availability. |
| `combo` | `u32` | Current combo state at decision time. |
| `b2b_active` | `bool` | `Board.b2b_bonus` exported directly as a boolean. |
| `incoming` | `u32` | Pending incoming garbage passed into `suggest_move()`. |

### `search`

| Field | Type | Semantics |
|---|---|---|
| `nodes` | `u32` | Search nodes reported by Cold Clear. |
| `depth` | `u32` | Search depth / generation count. |
| `decision_mode` | `string` | `"Book"`, `"Normal"`, `"SurviveFilter"`, `"SpikeBackup"`. |
| `chosen_idx` | `usize` | Index of the chosen candidate in `candidates`. |
| `primary_compare_idx` | `usize|null` | Primary comparison candidate with a distinct `action_id`. |
| `primary_compare_basis` | `string` | Why the primary comparison candidate was chosen. |
| `best_value_alt_idx` | `usize|null` | Best distinct alternative by backed-up value. |
| `best_survival_alt_idx` | `usize|null` | Best distinct survival-passing alternative. |
| `best_spike_alt_idx` | `usize|null` | Best distinct alternative by backed-up spike. |
| `margin_value_vs_primary` | `i32|null` | `chosen.backed_up_value - primary.backed_up_value`. |
| `margin_spike_vs_primary` | `i32|null` | `chosen.backed_up_spike - primary.backed_up_spike`. |
| `first_survival_pass` | `bool` | Whether rank-0 candidate passes the survival filter. |
| `any_survival_pass` | `bool` | Whether any root candidate passes the survival filter. |
| `cot_eligible` | `bool` | Whether a distinct primary comparison exists. |
| `candidate_count` | `usize` | Total root candidate count produced by the engine. |
| `candidate_count_exported` | `usize` | Total candidate count exported into the packet. |

### `chosen_action`

| Field | Type | Semantics |
|---|---|---|
| `action_id` | `string` | Canonical placement identifier. |
| `piece_used` | `string` | Piece actually placed. |
| `hold_used` | `bool` | Whether the move consumed hold. |
| `cells_x` | `u8[4]` | X coordinates of the placed cells. |
| `cells_y` | `u8[4]` | Y coordinates of the placed cells. |

### `chosen_future_plan`

Future-only continuation after the chosen root action. The chosen root action itself must not appear here.

Each entry is:

| Field | Type | Semantics |
|---|---|---|
| `action_id` | `string` | Canonical identifier for that continuation placement. |
| `piece_used` | `string` | Piece placed in that continuation step. |
| `hold_used` | `bool` | Always `false` for now because DAG dedup loses hold-path provenance. |
| `cells_x` | `u8[4]` | X coordinates. |
| `cells_y` | `u8[4]` | Y coordinates. |
| `placement_kind` | `string` | Lock result kind. |
| `lines_cleared` | `u8` | Number of cleared lines. |
| `garbage_sent` | `u32` | Garbage sent by this step. |
| `combo_after` | `u32|null` | Combo after lock, `null` when combo breaks. |
| `b2b_after` | `bool` | B2B active after lock. |

### `candidates`

All exported root candidates. No fixed cap.

| Field | Type | Semantics |
|---|---|---|
| `idx` | `usize` | Export index in `candidates`. |
| `action_id` | `string` | Canonical action identifier. |
| `piece_used` | `string` | Piece actually placed by this candidate. |
| `hold_used` | `bool` | Whether the candidate uses hold. |
| `cells_x` | `u8[4]` | X coordinates. |
| `cells_y` | `u8[4]` | Y coordinates. |
| `original_rank` | `u32` | Original engine rank. |
| `distinct_rank` | `usize` | Rank after grouping by distinct `action_id`. |
| `survival_pass` | `bool` | Survival filter result for this candidate. |
| `backed_up_value` | `i32` | Backed-up search value. |
| `backed_up_spike` | `i32` | Backed-up search spike. |
| `immediate` | `object` | Immediate lock outcome. |
| `immediate_trace` | `EvalTraceV3|null` | Trace from the immediate/local evaluation. |
| `board_after_visible` | `u8[20][10]` | Board after this root placement. |
| `board_after_hidden` | `u8[2][10]` | Hidden rows after this root placement. |
| `future_plan` | `PlanStepV3[]` | Future-only continuation after this candidate root action. |
| `future_terminal_trace` | `EvalTraceV3|null` | Trace evaluated after replaying the candidate continuation horizon. |

#### `immediate`

| Field | Type | Semantics |
|---|---|---|
| `placement_kind` | `string` | Immediate lock kind. |
| `lines_cleared` | `u8` | Cleared lines on the immediate lock. |
| `garbage_sent` | `u32` | Garbage sent by the immediate lock. |
| `combo_after` | `u32|null` | Combo after immediate lock, `null` when combo breaks. |
| `b2b_after` | `bool` | B2B active after the immediate lock. |
| `perfect_clear` | `bool` | Whether the immediate lock is a PC. |

#### `immediate_trace` vs `future_terminal_trace`

- `immediate_trace` is the local trace attached to the candidate node.
- `future_terminal_trace` is recomputed after replaying the candidate continuation horizon.
- Neither field should be described as a backprop-updated explanation trace.

## Action ID

Format:

```text
{piece}|H{0/1}|X{x0},{x1},{x2},{x3}|Y{y0},{y1},{y2},{y3}
```

Cells are sorted by `(x, y)` before formatting. That guarantees the same placement yields the same `action_id`.

## Distinctness Rules

- `primary_compare_idx` must reference a candidate with a distinct `action_id` from `chosen_action.action_id`.
- `best_value_alt_idx`, `best_survival_alt_idx`, and `best_spike_alt_idx` follow the same distinctness rule.
- `distinct_rank` is assigned by first occurrence order of unique `action_id`s.

## Board Encoding

- Row-major arrays.
- Row `0` is the bottom row.
- Cell values are `0` or `1`.

## `collector_meta`

| Field | Type | Semantics |
|---|---|---|
| `run_id` | `string` | Stable identifier for the collection run. |
| `episode_id` | `int` | Monotonic episode index within the run. |
| `step_id` | `int` | Zero-based step index within the episode. |
| `game_complete` | `bool` | `true` only for the terminal step of an episode. |
| `collector_seed` | `int` | Collector-side seed/config tag for reproducibility. |
| `engine_commit` | `string` | Engine revision identifier if known at collection time. |
| `weights_hash` | `string` | Hash of the evaluator weights used for the run. |
| `options_hash` | `string` | Hash of the Cold Clear options used for the run. |
| `book_enabled` | `bool` | Whether opening book was enabled for that run. |
