# Collector Protocol

This document describes how Unity writes `cot_raw_v3` packets to disk.

## Data Flow

1. Cold Clear computes the move in Rust.
2. `c-api` builds a canonical `RawDecisionPacketV3` and stores it as UTF-8 JSON.
3. Unity polls `cc_last_decision_json_len()` and `cc_copy_last_decision_json()`.
4. `DataHandler` wraps the engine packet with `collector_meta`.
5. The wrapper is written as a single JSONL line.

Unity is only a transport layer. It should not reconstruct candidate data, action identifiers, or board state for the dataset.

## File Layout

- Output directory: `datasets/`
- File pattern: `dataset_0001.jsonl`, `dataset_0002.jsonl`, ...
- One line per decision step

Each line has:

```json
{
  "schema_version": "cot_raw_v3",
  "engine": { ... },
  "collector_meta": {
    "run_id": "...",
    "episode_id": 1,
    "step_id": 0,
    "game_complete": false,
    "collector_seed": 1337,
    "engine_commit": "unknown",
    "weights_hash": "...",
    "options_hash": "...",
    "book_enabled": false
  }
}
```

## Collector Metadata

| Field | Meaning |
|---|---|
| `run_id` | Stable identifier for the collection run. |
| `episode_id` | Monotonic game index within the run. |
| `step_id` | Zero-based step index within the episode. |
| `game_complete` | `true` only on the terminal step of an episode. |
| `collector_seed` | Collector-side seed/config tag recorded in the dataset. |
| `engine_commit` | Engine revision identifier if known. |
| `weights_hash` | Hash of the evaluator weights used by the run. |
| `options_hash` | Hash of the Cold Clear options used by the run. |
| `book_enabled` | Whether opening book was enabled for the run. |

## Rotation

- `maxLinesPerFile` controls rotation.
- Rotation happens after a line is written once the current file reaches the configured line count.
- Files are overwritten when a new run starts.

## Terminal Step Handling

- `SaveData()` creates a pending step marker when a new decision point appears.
- `AttachEngineJson()` flushes the previous staged step as non-terminal, then stages the current step.
- The current staged step is written as terminal only when the episode actually ends.
- If `stepsPerGame` is reached, Unity waits until the current piece actually locks before restarting.
- This avoids resetting the board before queued commands finish executing.

## Failure Semantics

- Real top-out or collector-forced restart writes the last staged step with `game_complete: true`.
- Non-terminal steps are written with `game_complete: false`.
- Empty pending steps should never be flushed.

## Validation Workflow

After collecting a sample:

1. Run `datasets/scripts/validate_raw_v3.py`.
2. Run `datasets/scripts/summarize_raw_v3.py`.
3. Manually inspect a few packets across decision modes.
