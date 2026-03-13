use std::collections::HashSet;

use libtetris::{FallingPiece, LockResult, Piece, PlacementKind};
use serde::Serialize;

use crate::evaluation::standard::EvalTrace;
use crate::modes::normal::{CandidateInfo, DecisionMode};

// ── V3 Serde Structs ──────────────────────────────────────────────

#[derive(Serialize)]
pub struct RawDecisionPacketV3 {
    pub obs: ObservationPacketV3,
    pub search: SearchPacketV3,
    pub chosen_action: ActionPacketV3,
    pub chosen_future_plan: Vec<PlanStepV3>,
    pub candidates: Vec<CandidatePacketV3>,
}

#[derive(Serialize)]
pub struct ObservationPacketV3 {
    pub board_visible: [[u8; 10]; 20],
    pub board_hidden: [[u8; 10]; 2],
    pub current_piece: String,
    pub hold_piece: Option<String>,
    pub next_visible: Vec<String>,
    pub hold_enabled: bool,
    pub combo: u32,
    pub b2b_active: bool,
    pub incoming: u32,
}

#[derive(Serialize)]
pub struct SearchPacketV3 {
    pub nodes: u32,
    pub depth: u32,
    pub decision_mode: String,
    pub chosen_idx: usize,
    pub primary_compare_idx: Option<usize>,
    pub primary_compare_basis: String,
    pub best_value_alt_idx: Option<usize>,
    pub best_survival_alt_idx: Option<usize>,
    pub best_spike_alt_idx: Option<usize>,
    pub margin_value_vs_primary: Option<i32>,
    pub margin_spike_vs_primary: Option<i32>,
    pub first_survival_pass: bool,
    pub any_survival_pass: bool,
    pub cot_eligible: bool,
    pub candidate_count: usize,
    pub candidate_count_exported: usize,
}

#[derive(Serialize)]
pub struct ActionPacketV3 {
    pub action_id: String,
    pub piece_used: String,
    pub hold_used: bool,
    pub cells_x: [u8; 4],
    pub cells_y: [u8; 4],
}

#[derive(Serialize)]
pub struct PlanStepV3 {
    pub action_id: String,
    pub piece_used: String,
    pub hold_used: bool,
    pub cells_x: [u8; 4],
    pub cells_y: [u8; 4],
    pub placement_kind: String,
    pub lines_cleared: u8,
    pub garbage_sent: u32,
    pub combo_after: Option<u32>,
    pub b2b_after: bool,
}

#[derive(Serialize)]
pub struct CandidatePacketV3 {
    pub idx: usize,
    pub action_id: String,
    pub piece_used: String,
    pub hold_used: bool,
    pub cells_x: [u8; 4],
    pub cells_y: [u8; 4],
    pub original_rank: u32,
    pub distinct_rank: usize,
    pub survival_pass: bool,
    pub backed_up_value: i32,
    pub backed_up_spike: i32,
    pub immediate: ImmediateOutcomeV3,
    pub immediate_trace: Option<EvalTraceV3>,
    pub board_after_visible: [[u8; 10]; 20],
    pub board_after_hidden: [[u8; 10]; 2],
    pub future_plan: Vec<PlanStepV3>,
    pub future_terminal_trace: Option<EvalTraceV3>,
}

#[derive(Serialize)]
pub struct ImmediateOutcomeV3 {
    pub placement_kind: String,
    pub lines_cleared: u8,
    pub garbage_sent: u32,
    pub combo_after: Option<u32>,
    pub b2b_after: bool,
    pub perfect_clear: bool,
}

#[derive(Serialize)]
pub struct EvalTraceV3 {
    pub clear_score: i32,
    pub tspin_score: i32,
    pub pc_score: i32,
    pub b2b_score: i32,
    pub combo_score: i32,
    pub wasted_t: i32,
    pub height_penalty: i32,
    pub jeopardy_penalty: i32,
    pub well_score: i32,
    pub tslot_score: [i32; 4],
    pub bumpiness_penalty: i32,
    pub hole_penalty: i32,
    pub covered_penalty: i32,
    pub row_transitions: i32,
}

// ── Helpers ────────────────────────────────────────────────────────

pub fn piece_to_str(p: Piece) -> &'static str {
    match p {
        Piece::I => "I",
        Piece::O => "O",
        Piece::T => "T",
        Piece::L => "L",
        Piece::J => "J",
        Piece::S => "S",
        Piece::Z => "Z",
    }
}

pub fn placement_kind_to_str(pk: PlacementKind) -> &'static str {
    match pk {
        PlacementKind::None => "None",
        PlacementKind::Clear1 => "Clear1",
        PlacementKind::Clear2 => "Clear2",
        PlacementKind::Clear3 => "Clear3",
        PlacementKind::Clear4 => "Clear4",
        PlacementKind::MiniTspin => "MiniTspin",
        PlacementKind::MiniTspin1 => "MiniTspin1",
        PlacementKind::MiniTspin2 => "MiniTspin2",
        PlacementKind::Tspin => "Tspin",
        PlacementKind::Tspin1 => "Tspin1",
        PlacementKind::Tspin2 => "Tspin2",
        PlacementKind::Tspin3 => "Tspin3",
    }
}

fn decision_mode_to_str(dm: &DecisionMode) -> &'static str {
    match dm {
        DecisionMode::Book => "Book",
        DecisionMode::Normal => "Normal",
        DecisionMode::SurviveFilter => "SurviveFilter",
        DecisionMode::SpikeBackup => "SpikeBackup",
    }
}

/// Canonical action identifier: `{piece}|H{0/1}|X{x0},{x1},{x2},{x3}|Y{y0},{y1},{y2},{y3}`
/// Cells are sorted by (x, y) to ensure determinism.
pub fn make_action_id(piece: Piece, hold: bool, cells: &[(i32, i32); 4]) -> String {
    let mut sorted = *cells;
    sorted.sort_by(|a, b| a.0.cmp(&b.0).then(a.1.cmp(&b.1)));
    format!(
        "{}|H{}|X{},{},{},{}|Y{},{},{},{}",
        piece_to_str(piece),
        if hold { 1 } else { 0 },
        sorted[0].0, sorted[1].0, sorted[2].0, sorted[3].0,
        sorted[0].1, sorted[1].1, sorted[2].1, sorted[3].1,
    )
}

/// Same as make_action_id but from a FallingPiece + hold flag.
fn action_id_from_falling(fp: &FallingPiece, hold: bool) -> String {
    make_action_id(fp.kind.0, hold, &fp.cells())
}

fn board_to_visible_hidden(field: &[[bool; 10]; 40]) -> ([[u8; 10]; 20], [[u8; 10]; 2]) {
    let mut visible = [[0u8; 10]; 20];
    let mut hidden = [[0u8; 10]; 2];
    for row in 0..20 {
        for col in 0..10 {
            visible[row][col] = if field[row][col] { 1 } else { 0 };
        }
    }
    for row in 0..2 {
        for col in 0..10 {
            hidden[row][col] = if field[20 + row][col] { 1 } else { 0 };
        }
    }
    (visible, hidden)
}

fn cells_to_xy(fp: &FallingPiece) -> ([u8; 4], [u8; 4]) {
    let cells = fp.cells();
    let mut x = [0u8; 4];
    let mut y = [0u8; 4];
    for i in 0..4 {
        x[i] = cells[i].0 as u8;
        y[i] = cells[i].1 as u8;
    }
    (x, y)
}

fn eval_trace_to_v3(trace: &Option<EvalTrace>) -> Option<EvalTraceV3> {
    trace.as_ref().map(|t| EvalTraceV3 {
        clear_score: t.clear_score,
        tspin_score: t.tspin_score,
        pc_score: t.pc_score,
        b2b_score: t.b2b_score,
        combo_score: t.combo_score,
        wasted_t: t.wasted_t,
        height_penalty: t.height_penalty,
        jeopardy_penalty: t.jeopardy_penalty,
        well_score: t.well_score,
        tslot_score: t.tslot_score,
        bumpiness_penalty: t.bumpiness_penalty,
        hole_penalty: t.hole_penalty,
        covered_penalty: t.covered_penalty,
        row_transitions: t.row_transitions,
    })
}

fn plan_step_from_pv(fp: &FallingPiece, lr: &LockResult) -> PlanStepV3 {
    let (cells_x, cells_y) = cells_to_xy(fp);
    PlanStepV3 {
        action_id: action_id_from_falling(fp, false), // hold unknown in PV
        piece_used: piece_to_str(fp.kind.0).to_string(),
        hold_used: false,
        cells_x,
        cells_y,
        placement_kind: placement_kind_to_str(lr.placement_kind).to_string(),
        lines_cleared: lr.cleared_lines.len() as u8,
        garbage_sent: lr.garbage_sent,
        combo_after: lr.combo,
        b2b_after: lr.b2b,
    }
}

fn build_candidate(
    idx: usize,
    cand: &CandidateInfo,
    distinct_rank: usize,
) -> CandidatePacketV3 {
    let action_id = action_id_from_falling(&cand.move_piece, cand.hold);
    let (cells_x, cells_y) = cells_to_xy(&cand.move_piece);
    let (board_vis, board_hid) = board_to_visible_hidden(&cand.board_after);

    let future_plan: Vec<PlanStepV3> = cand
        .pv
        .iter()
        .filter(|(fp, _)| !fp.same_location(&cand.move_piece))
        .map(|(fp, lr)| plan_step_from_pv(fp, lr))
        .collect();

    CandidatePacketV3 {
        idx,
        action_id,
        piece_used: piece_to_str(cand.move_piece.kind.0).to_string(),
        hold_used: cand.hold,
        cells_x,
        cells_y,
        original_rank: cand.original_rank,
        distinct_rank,
        survival_pass: cand.survival_pass,
        backed_up_value: cand.eval_score,
        backed_up_spike: cand.spike_score,
        immediate: ImmediateOutcomeV3 {
            placement_kind: placement_kind_to_str(cand.placement_kind).to_string(),
            lines_cleared: cand.cleared_lines.len() as u8,
            garbage_sent: cand.garbage_sent,
            combo_after: cand.combo,
            b2b_after: cand.b2b,
            perfect_clear: cand.perfect_clear,
        },
        immediate_trace: eval_trace_to_v3(&cand.trace),
        board_after_visible: board_vis,
        board_after_hidden: board_hid,
        future_plan,
        future_terminal_trace: eval_trace_to_v3(&cand.future_terminal_trace),
    }
}

fn distinct_non_chosen_indices(action_ids: &[String], chosen_action_id: &str) -> Vec<usize> {
    let mut seen = HashSet::new();
    let mut indices = Vec::new();
    for (idx, action_id) in action_ids.iter().enumerate() {
        if action_id == chosen_action_id {
            continue;
        }
        if seen.insert(action_id.clone()) {
            indices.push(idx);
        }
    }
    indices
}

fn recompute_compare_indices(
    ninfo: &crate::modes::normal::Info,
    action_ids: &[String],
    chosen_idx: usize,
    chosen_action_id: &str,
) -> (Option<usize>, String, Option<usize>, Option<usize>, Option<usize>, bool) {
    let distinct_non_chosen = distinct_non_chosen_indices(action_ids, chosen_action_id);
    let first_other_by_rank = distinct_non_chosen.first().copied();

    let best_value_alt_idx = distinct_non_chosen
        .iter()
        .copied()
        .max_by_key(|&i| (ninfo.candidates[i].eval_score, ninfo.candidates[i].spike_score, -(i as i32)));

    let best_survival_alt_idx = distinct_non_chosen
        .iter()
        .copied()
        .find(|&i| ninfo.candidates[i].survival_pass);

    let best_spike_alt_idx = distinct_non_chosen
        .iter()
        .copied()
        .max_by_key(|&i| (ninfo.candidates[i].spike_score, ninfo.candidates[i].eval_score, -(i as i32)));

    let (primary_compare_idx, primary_compare_basis) = match ninfo.decision_mode {
        DecisionMode::Book => (None, "BOOK".to_string()),
        DecisionMode::Normal => (first_other_by_rank, "BEST_OTHER_BY_RANK".to_string()),
        DecisionMode::SurviveFilter => (
            ninfo.candidates
                .iter()
                .enumerate()
                .take(chosen_idx)
                .find(|(i, c)| !c.survival_pass && action_ids[*i] != chosen_action_id)
                .map(|(i, _)| i),
            "BEST_FILTERED_OUT_BY_SURVIVAL".to_string(),
        ),
        DecisionMode::SpikeBackup => (best_spike_alt_idx, "SECOND_BEST_SPIKE".to_string()),
    };

    (
        primary_compare_idx,
        primary_compare_basis,
        best_value_alt_idx,
        best_survival_alt_idx,
        best_spike_alt_idx,
        primary_compare_idx.is_some(),
    )
}

// ── Main Builder ───────────────────────────────────────────────────

/// Build a v3 packet from a Move and Info.
/// Returns None for PcLoop mode.
pub fn build_v3_packet(
    mv: &crate::Move,
    info: &crate::Info,
) -> Option<RawDecisionPacketV3> {
    let ninfo = match info {
        crate::Info::Normal(n) => n,
        _ => return None,
    };

    // Observation
    let (board_vis, board_hid) = board_to_visible_hidden(&ninfo.obs_field);
    let current_piece = ninfo.obs_queue.first()
        .map(|p| piece_to_str(*p).to_string())
        .unwrap_or_else(|| "?".to_string());
    let next_visible: Vec<String> = ninfo.obs_queue.iter().skip(1)
        .map(|p| piece_to_str(*p).to_string())
        .collect();
    let hold_enabled = ninfo.obs_can_hold;
    // Actually: can_hold is true if the player can hold this turn.
    // In cold-clear, use_hold is an option, and hold is always available if enabled.
    // The board tracks hold_piece but not "can_hold this turn" — it's always true
    // unless the game engine disables it. We set true here.

    let obs = ObservationPacketV3 {
        board_visible: board_vis,
        board_hidden: board_hid,
        current_piece,
        hold_piece: ninfo.obs_hold.map(|p| piece_to_str(p).to_string()),
        next_visible,
        hold_enabled,
        combo: ninfo.obs_combo,
        b2b_active: ninfo.obs_b2b,
        incoming: ninfo.incoming,
    };

    // Build candidates + action IDs
    let mut candidate_packets = Vec::with_capacity(ninfo.candidates.len());
    let mut action_ids: Vec<String> = Vec::with_capacity(ninfo.candidates.len());
    let mut seen_ids: Vec<String> = Vec::new();
    let mut distinct_ranks: Vec<usize> = Vec::with_capacity(ninfo.candidates.len());

    for cand in &ninfo.candidates {
        let aid = action_id_from_falling(&cand.move_piece, cand.hold);
        let dr = match seen_ids.iter().position(|s| s == &aid) {
            Some(pos) => pos,
            None => {
                let r = seen_ids.len();
                seen_ids.push(aid.clone());
                r
            }
        };
        distinct_ranks.push(dr);
        action_ids.push(aid);
    }

    for (i, cand) in ninfo.candidates.iter().enumerate() {
        candidate_packets.push(build_candidate(i, cand, distinct_ranks[i]));
    }

    // Find chosen index in candidate array
    let chosen_action_id = make_action_id(
        mv.expected_location.kind.0,
        mv.hold,
        &mv.expected_location.cells(),
    );
    let chosen_idx = action_ids.iter().position(|a| a == &chosen_action_id).unwrap_or(0);

    // Chosen action
    let (chosen_cx, chosen_cy) = cells_to_xy(&mv.expected_location);
    let chosen_action = ActionPacketV3 {
        action_id: chosen_action_id.clone(),
        piece_used: piece_to_str(mv.expected_location.kind.0).to_string(),
        hold_used: mv.hold,
        cells_x: chosen_cx,
        cells_y: chosen_cy,
    };

    // Chosen future plan (from Info.plan, already excludes root action)
    let chosen_future_plan: Vec<PlanStepV3> = ninfo
        .plan
        .iter()
        .filter(|(fp, _)| !fp.same_location(&mv.expected_location))
        .map(|(fp, lr)| plan_step_from_pv(fp, lr))
        .collect();

    let (
        primary_compare_idx,
        primary_compare_basis,
        best_value_alt_idx,
        best_survival_alt_idx,
        best_spike_alt_idx,
        cot_eligible,
    ) = recompute_compare_indices(ninfo, &action_ids, chosen_idx, &chosen_action_id);

    let (margin_val, margin_spike) = match primary_compare_idx {
        Some(pi) => {
            let chosen_c = &ninfo.candidates[chosen_idx];
            let primary_c = &ninfo.candidates[pi];
            (
                Some(chosen_c.eval_score - primary_c.eval_score),
                Some(chosen_c.spike_score - primary_c.spike_score),
            )
        }
        None => (None, None),
    };

    let search = SearchPacketV3 {
        nodes: ninfo.nodes,
        depth: ninfo.depth,
        decision_mode: decision_mode_to_str(&ninfo.decision_mode).to_string(),
        chosen_idx,
        primary_compare_idx,
        primary_compare_basis,
        best_value_alt_idx,
        best_survival_alt_idx,
        best_spike_alt_idx,
        margin_value_vs_primary: margin_val,
        margin_spike_vs_primary: margin_spike,
        first_survival_pass: ninfo.first_survival_pass,
        any_survival_pass: ninfo.any_survival_pass,
        cot_eligible,
        candidate_count: ninfo.candidates.len(),
        candidate_count_exported: ninfo.candidates.len(),
    };

    Some(RawDecisionPacketV3 {
        obs,
        search,
        chosen_action,
        chosen_future_plan,
        candidates: candidate_packets,
    })
}
