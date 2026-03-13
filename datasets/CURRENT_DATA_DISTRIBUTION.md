# Legacy Data Distribution

This document summarizes the older Unity-assembled raw packet pipeline and its first-pass offline CoT renderer.

- It does not describe the canonical `cot_raw_v3` packet format.
- It should not be used as the schema reference for new collection runs.
- Current schema reference: [raw_v3_schema.md](/C:/Users/jrjin/Desktop/TAI/Tetris_AI/datasets/raw_v3_schema.md)
- Current collection protocol: [collector_protocol.md](/C:/Users/jrjin/Desktop/TAI/Tetris_AI/datasets/collector_protocol.md)

This document summarizes the legacy raw decision packet dataset and the first-pass offline CoT rendering behavior as of March 10, 2026.

## Dataset Scope

- Source raw file: `datasets/dataset_0001.jsonl`
- Source rendered file: `datasets/dataset_0001.rendered.jsonl`
- Current raw sample count: 10,000 steps
- Episode structure observed in the latest file:
  - `episode_id=1..100`
  - 100 steps per completed episode

## Current Rendered Distribution

These numbers come from the current `datasets/scripts/render_cot.py` and `datasets/scripts/analyze_rendered.py` outputs over the full 10,000-step rendered file.

### All Steps

- Total steps: `10000`
- Blank CoT texts: `0`
- Steps with `safe` atom: `2934` (`29.34%`)
- Steps with `safe` as the only atom: `2103` (`21.03%`)
- Steps where chosen action uses hold: `2766` (`27.66%`)

### Goal Distribution

- `STACK_SHAPE`: `4168`
- `DOWNSTACK`: `3030`
- `PIECE_MANAGEMENT`: `1505`
- `ATTACK_EXEC`: `1297`
- `SURVIVAL`: `0`
- `PRESSURE_CONTINUE`: `0`

### Top Atom Distribution

- `bumpiness`: `3052`
- `safe`: `2934`
- `well`: `2424`
- `height`: `1511`
- `attack_now`: `1264`
- `clear_value`: `1260`
- `tslot`: `991`
- `hold`: `826`
- `holes`: `370`
- `coverage`: `184`

### Top Atom Combos

- `safe`: `2103`
- `well`: `1799`
- `bumpiness`: `1292`
- `clear_value,attack_now`: `971`
- `hold,safe`: `821`
- `bumpiness,height`: `592`
- `tslot`: `461`
- `height`: `236`

## Latest Episode Snapshot

For the latest fully observed episode (`episode_id=100`, 100 steps):

- Steps with `safe` atom: `30` (`30.0%`)
- Steps with `safe` as the only atom: `23` (`23.0%`)
- Hold actions: `29` (`29.0%`)
- Goal distribution:
  - `STACK_SHAPE`: `42`
  - `DOWNSTACK`: `30`
  - `PIECE_MANAGEMENT`: `14`
  - `ATTACK_EXEC`: `14`

The latest episode is consistent with the full-file distribution, which suggests the current `safe` overrepresentation is a renderer rule issue rather than a single-episode anomaly.

## How This Distribution Is Produced

The current rendered distribution is not a direct Cold Clear output. It is produced by a two-stage pipeline:

1. Cold Clear raw decision packet capture in Unity
2. Offline heuristic rendering in Python

### Stage 1: Raw Decision Packet Capture

The Unity and Rust integration now records:

- Full observation state:
  - `board_visible`
  - `board_hidden`
  - current piece, hold piece, next queue
  - `combo`
  - `b2b_chain`
- Search metadata:
  - `nodes`
  - `depth`
  - `decision_mode`
  - `chosen_idx`
- Full candidate list:
  - `original_rank`
  - `survival_pass`
  - `backed_up_value`
  - `backed_up_spike`
  - `placement_kind`
  - `combo_after`
  - `garbage_sent`
  - local evaluation trace
  - canonical `placement_token`
- Chosen action:
  - `action_hold`
  - chosen placement coordinates
  - canonical `action_token`

This stage is intended to preserve the actual Cold Clear search result without forcing natural-language explanations online.

### Stage 2: Offline CoT Rendering

The current renderer in `datasets/scripts/render_cot.py` transforms each raw packet into:

- `cot_goal`
- `cot_atoms`
- `cot_text`
- `runner_up_token`

The renderer currently uses the following logic:

#### Candidate Comparison

- `chosen` is selected from `chosen_idx`
- `runner_up` is the highest-valued non-chosen candidate
- duplicate placement tokens are skipped when possible

#### Goal Inference

The current goal priority order is:

1. `SURVIVAL` if `decision_mode` is `SurviveFilter` or `SpikeBackup`
2. `ATTACK_EXEC` if:
   - placement is a T-Spin variant, or
   - `lines_cleared >= 2`, or
   - `garbage_sent >= 4`
3. `PRESSURE_CONTINUE` if:
   - `combo_after >= 2`, or
   - `b2b == true`
4. `DOWNSTACK` if absolute chosen trace penalties imply enough stack cleanup pressure
5. `PIECE_MANAGEMENT` if the chosen move used hold
6. `STACK_SHAPE` otherwise

#### Atom Extraction

Current atoms are scored mainly from chosen-vs-runner-up deltas in:

- immediate spike
- clear value
- height
- holes
- covered cells
- jeopardy
- well score
- bumpiness
- T-slot score

If no positive delta survives, fallback atoms are inserted from:

- T-Spin presence
- line clear presence
- hold usage
- `survival_pass`
- backed-up value

## Current Interpretation

The current rendered distribution implies:

- The raw packet pipeline is functioning and producing stable candidate/search metadata.
- The offline CoT renderer is deterministic, but still overuses generic fallback explanations.
- In particular, `safe` appears too often because the renderer treats `survival_pass=true` as an explanation too broadly.

This means the dataset currently preserves useful raw decision packets, but the first-pass natural-language CoT layer still needs tuning before it should be treated as high-quality explanation data.

## Known Quality Issues In The Current Renderer

- `safe` is overrepresented:
  - `29.34%` of all steps
  - `21.03%` of all steps as the only atom
- `PIECE_MANAGEMENT` is often assigned too early just because hold was used.
- `DOWNSTACK` is currently inferred from absolute trace pressure instead of chosen-vs-runner-up improvement.
- Goal and atom combinations are not yet constrained tightly enough, so some sentences are internally weak even if deterministic.

## Next Tuning Direction

The next renderer revision should likely:

- restrict `safe` to true decision boundaries
- demote hold from goal trigger to supporting evidence in many cases
- infer `DOWNSTACK` from relative improvement rather than absolute penalty totals
- enforce stronger goal-to-atom compatibility

The raw packet data itself should remain unchanged. Renderer tuning should happen by rerendering derived files from the preserved raw packets.
