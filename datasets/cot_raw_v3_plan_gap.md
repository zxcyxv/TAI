# cot_raw_v3 Plan vs Current Implementation

This note summarizes the gap between the latest `cot_raw_v3` redesign prompt and the code currently in this repository.

It is not a proposal. It is a status document based on the current implementation in Rust, `c-api`, Unity, and the validation/docs already checked into this branch.

## Scope

Reference intent:
- Rust is the canonical source of the raw decision packet.
- `c-api` exposes the packet as JSON.
- Unity only transports the JSON and appends minimal collector metadata.
- No natural-language CoT is generated inside Rust or Unity.

Current code inspected:
- `cold-clear-fork/bot/src/packet.rs`
- `cold-clear-fork/bot/src/modes/normal.rs`
- `cold-clear-fork/c-api/src/lib.rs`
- `Assets/Scripts/ColdClearNative.cs`
- `Assets/Scripts/ColdClearAgent.cs`
- `Assets/Scripts/DataHandler.cs`
- `Assets/Scripts/DecisionPacket.cs`
- `datasets/raw_v3_schema.md`
- `datasets/collector_protocol.md`
- `datasets/scripts/validate_raw_v3.py`
- `datasets/scripts/summarize_raw_v3.py`

## Already Aligned

The following core redesign goals are already implemented.

- Canonical engine packet construction moved to Rust.
  - `RawDecisionPacketV3` is built in `cold-clear-fork/bot/src/packet.rs`.
- JSON export path exists in `c-api`.
  - `cc_last_decision_json_len()` and `cc_copy_last_decision_json()` are implemented.
- Unity no longer reconstructs dataset-critical candidate semantics.
  - `ColdClearAgent` fetches UTF-8 JSON from Rust and passes it to `DataHandler`.
- Old `repr(C)` candidate marshaling path has been removed from the active dataset path.
  - `CCCandidate`, `CCDecisionInfo`, `CCPlanPlacement` are no longer used in Unity.
- All root candidates are exported in the JSON packet.
  - Current validation/summaries show packets with far more than 10 candidates.
- Future-plan semantics are now future-only.
  - `chosen_future_plan` and `candidate.future_plan` exclude the root action.
- Immediate vs terminal trace separation exists.
  - `immediate_trace` and `future_terminal_trace` are separate fields.
- `hold_piece` serializes as JSON `null` when empty.
- No natural-language CoT is generated inside Rust or Unity.

## Intentional Direction Change

This branch intentionally differs from the prompt on the B2B field.

- Prompt version:
  - `obs.b2b_chain: i32`
- Current implementation:
  - `obs.b2b_active: bool`

Reason:
- `libtetris::Board` does not expose a chain count.
- The current design exports `Board.b2b_bonus` directly as a boolean.

This is an intentional schema decision, not a bug, but it means the repository no longer matches the prompt literally on this field.

## Status Update

This file was originally written before the later collector patches landed.

The following items from that earlier gap list are now resolved:

- `collector_meta` now includes:
  - `collector_seed`
  - `engine_commit`
  - `weights_hash`
  - `options_hash`
  - `book_enabled`
- Unity now writes one line per step using a staged immediate-write flow instead of episode-end bulk flush.
- `game_complete` is now step-accurate.
  - only the terminal step of each episode is marked `true`
- `Book` decisions are now exportable through the same canonical Rust packet path.
- distinct compare metadata is now recomputed in `packet.rs` using distinct `action_id` logic instead of only post-filtering old indices.
- validation now checks the richer `collector_meta` fields and tighter decision-mode semantics.

Decision-mode status:
- `Book`, `Normal`, `SurviveFilter`, and `SpikeBackup` export rules are implemented in Rust packet construction.
- validator rules now check the expected compare-basis / primary-compare semantics for those modes.
- the latest sample used for validation still contained only `Normal` decisions, so non-`Normal` modes are implemented but not yet observed in a fresh sample.

## Remaining Gaps

## 1. `can_hold` was not implementable with the intended meaning, so the field was renamed

Prompt asked for:
- `can_hold: bool`

Current implementation exports:
- `hold_enabled: bool`

Reason:
- the Rust-side board/evaluator stack does not track Unity-style "hold already consumed this turn" state as a canonical observation field
- exporting `can_hold` would therefore overclaim semantic precision the engine does not actually have
- the field was renamed so the packet does not pretend to encode a state it cannot faithfully represent

Impact:
- this is a deliberate schema deviation from the prompt
- downstream code must use `hold_enabled`, not `can_hold`

## 2. Debug sync checking is still not implemented

Prompt requirement:
- comparison metadata should reflect the actual `pick_move` decision boundary
- all compare indices must be distinct by canonical `action_id`
- if no valid distinct alternative exists, emit `null`

Prompt requested:
- periodic Unity-vs-Rust observation sync check
- log or abort on mismatch

Current state:
- `DataManager`/board capture is no longer canonical
- no sync-check instrumentation is present in the active path

Impact:
- there is no built-in runtime guard for Rust/Unity observation drift during collection.

## 3. Deterministic collector metadata/settings are still only partially implemented

Prompt recommended:
- deterministic mode or collector mode
- seeded RNG for collector runs
- seed logged in metadata
- opening book disabled during collection

Current state:
- `collector_seed`, `weights_hash`, `options_hash`, and `book_enabled` are now written
- collection still does not use an engine-side seeded RNG or explicit collector-mode path
- `engine_commit` is collector-supplied metadata, not sourced from Rust automatically

Impact:
- two runs with the same high-level setup are not yet fully traceable as identical collection configurations.

## 4. Legacy collector/render artifacts still exist and should remain clearly deprecated

Current repo still contains:
- `datasets/raw_packet_schema.md`
- `datasets/CURRENT_DATA_DISTRIBUTION.md`
- `datasets/dataset_0001.rendered.jsonl`
- legacy/offline renderer scripts

Some of these are marked legacy/deprecated, which is good, but the repository still contains older collector-era artifacts next to the new format.

Impact:
- human confusion risk remains unless docs clearly direct all new work to `cot_raw_v3`.

## Practical Summary

If the question is "is the redesign direction already real?", the answer is yes.

The canonical shift has already happened:
- Rust owns the packet schema
- `c-api` owns JSON export
- Unity no longer owns dataset semantics

If the question is "does current code fully satisfy the latest prompt?", the answer is still no.

The main remaining work is no longer packet export. The remaining work is instrumentation and exact semantic cleanup:

- add sync instrumentation
- decide whether to keep the deliberate `hold_enabled` deviation or find a true engine-side `can_hold`
- improve deterministic collector controls
- continue deprecating/removing legacy outputs

## Recommended Priority Order

1. Add debug sync checking and, if useful, a report script.
2. Decide whether the schema will permanently keep `hold_enabled` instead of `can_hold`.
3. Add stronger deterministic collector controls if strict reproducibility is required.
4. Continue removing or clearly fencing off legacy collector/render artifacts.
