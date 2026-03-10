use arrayvec::ArrayVec;
use enum_map::EnumMap;
use libtetris::*;
use opening_book::Book;
use serde::{Deserialize, Serialize};

// use crate::tree::{ ChildData, TreeState, NodeId };
use crate::dag::{ChildData, DagState, NodeId};
use crate::evaluation::Evaluator;
use crate::Options;

pub struct BotState<E: Evaluator> {
    tree: DagState<E::Value, E::Reward, E::Trace>,
    options: Options,
    forced_analysis_lines: Vec<Vec<FallingPiece>>,
    pub outstanding_thinks: u32,
}

#[derive(Serialize, Deserialize)]
pub struct Thinker {
    node: NodeId,
    board: Board,
    options: Options,
}

#[derive(Serialize, Deserialize)]
pub enum ThinkResult<V, R, T> {
    Known(NodeId, Vec<ChildData<V, R, T>>),
    Speculated(NodeId, EnumMap<Piece, Option<Vec<ChildData<V, R, T>>>>),
    Unmark(NodeId),
}

impl<E: Evaluator> BotState<E> {
    pub fn new(board: Board, options: Options) -> Self {
        BotState {
            tree: DagState::new(board, options.use_hold),
            options,
            forced_analysis_lines: vec![],
            outstanding_thinks: 0,
        }
    }

    /// Prepare a thinking cycle.
    ///
    /// Returns `Err(true)` if a thinking cycle can be preformed, but it couldn't find
    pub fn think(&mut self) -> Result<Thinker, bool> {
        if (!self.min_thinking_reached() || self.tree.nodes() < self.options.max_nodes)
            && !self.tree.is_dead()
        {
            if let Some((node, board)) = self
                .tree
                .find_and_mark_leaf(&mut self.forced_analysis_lines)
            {
                self.outstanding_thinks += 1;
                return Ok(Thinker {
                    node,
                    board,
                    options: self.options,
                });
            } else {
                return Err(true);
            }
        } else {
            return Err(false);
        }
    }

    pub fn finish_thinking(&mut self, result: ThinkResult<E::Value, E::Reward, E::Trace>) {
        self.outstanding_thinks -= 1;
        match result {
            ThinkResult::Known(node, children) => self.tree.update_known(node, children),
            ThinkResult::Speculated(node, children) => self.tree.update_speculated(node, children),
            ThinkResult::Unmark(node) => self.tree.unmark(node),
        }
    }

    pub fn is_dead(&self) -> bool {
        self.tree.is_dead()
    }

    /// Adds a new piece to the queue.
    pub fn add_next_piece(&mut self, piece: Piece) {
        self.tree.add_next_piece(piece);
    }

    pub fn reset(&mut self, field: [[bool; 10]; 40], b2b: bool, combo: u32) {
        let plan = self.tree.get_plan();
        if let Some(garbage_lines) = self.tree.reset(field, b2b, combo) {
            for path in &mut self.forced_analysis_lines {
                for mv in path {
                    mv.y += garbage_lines;
                }
            }
            let mut prev_best_path = vec![];
            for mv in plan {
                let mut mv = mv.0;
                mv.y += garbage_lines;
                prev_best_path.push(mv);
            }
            self.forced_analysis_lines.push(prev_best_path);
        } else {
            self.forced_analysis_lines.clear();
        }
    }

    pub fn min_thinking_reached(&self) -> bool {
        self.tree.nodes() > self.options.min_nodes
            && self.forced_analysis_lines.is_empty()
            && !self.tree.get_next_candidates().is_empty()
    }

    pub fn suggest_move(
        &mut self,
        eval: &E,
        book: Option<&Book>,
        incoming: u32,
    ) -> Option<(Move, crate::Info)> {
        if !self.min_thinking_reached() {
            return None;
        }

        let candidates = self.tree.get_next_candidates();
        if candidates.is_empty() {
            return None;
        }
        let mut book_move = None;
        if let Some(book) = book {
            if self.tree.board().column_heights().iter().all(|&h| h <= 10) {
                book_move = book.suggest_move(self.tree.board());
            }
        }
        let mut picked = None;
        if let Some(book_move) = book_move {
            for mv in &candidates {
                if mv.mv.same_location(&book_move) {
                    picked = Some(mv.clone());
                    break;
                }
            }
        }
        if picked.is_none() && book_move.is_some() {
            dbg!("book picked a move we can't do?");
        }
        let child = picked.unwrap_or_else(|| eval.pick_move(candidates.clone(), incoming));

        let plan = if book_move.is_none() {
            self.tree.get_plan()
        } else {
            vec![]
        };

        let mut candidate_infos = Vec::new();
        let mut first_survival_pass = false;
        let mut any_survival_pass = false;
        for (ci, mv) in candidates.iter().enumerate() {
            let survival_pass = incoming == 0
                || mv.board.column_heights()[3..6]
                    .iter()
                    .all(|h| incoming as i32 - mv.lock.garbage_sent as i32 + h <= 20);
            if candidate_infos.is_empty() {
                first_survival_pass = survival_pass;
            }
            any_survival_pass |= survival_pass;

            let board_after = mv.board.get_field();
            let pv = self.tree.get_candidate_pv(mv.original_rank as usize, 5);

            candidate_infos.push(CandidateInfo {
                move_piece: mv.mv,
                hold: mv.hold,
                eval_score: eval.get_value(&mv.evaluation),
                spike_score: eval.get_spike(&mv.evaluation),
                trace: eval.into_standard_trace(&mv.trace),
                original_rank: mv.original_rank,
                placement_kind: mv.lock.placement_kind,
                b2b: mv.lock.b2b,
                perfect_clear: mv.lock.perfect_clear,
                combo: mv.lock.combo,
                garbage_sent: mv.lock.garbage_sent,
                cleared_lines: mv.lock.cleared_lines.clone(),
                survival_pass,
                board_after,
                pv,
            });
        }

        let decision_mode = if book_move.is_some() {
            DecisionMode::Book
        } else if first_survival_pass {
            DecisionMode::Normal
        } else if any_survival_pass {
            DecisionMode::SurviveFilter
        } else {
            DecisionMode::SpikeBackup
        };

        // Compute comparison metadata
        let compare_metadata = Self::compute_compare_metadata(
            &candidate_infos,
            &decision_mode,
            &child,
        );

        let info = if book_move.is_some() {
            crate::Info::Book
        } else {
            crate::Info::Normal(Info {
                nodes: if book_move.is_some() {
                    0
                } else {
                    self.tree.nodes()
                },
                depth: if book_move.is_some() {
                    6
                } else {
                    self.tree.depth() as u32
                },
                original_rank: child.original_rank,
                decision_mode,
                plan,
                candidates: candidate_infos,
                first_survival_pass,
                any_survival_pass,
                compare_metadata,
            })
        };

        let inputs = find_moves(
            self.tree.board(),
            self.options
                .spawn_rule
                .spawn(child.mv.kind.0, self.tree.board())
                .unwrap(),
            self.options.mode,
        )
        .into_iter()
        .find(|p| p.location == child.mv)
        .unwrap()
        .inputs;
        let mv = Move {
            hold: child.hold,
            inputs: inputs.movements,
            expected_location: child.mv,
        };

        return Some((mv, info));
    }

    fn compute_compare_metadata<V, T>(
        candidate_infos: &[CandidateInfo],
        decision_mode: &DecisionMode,
        chosen: &crate::dag::MoveCandidate<V, T>,
    ) -> CompareMetadata {
        if candidate_infos.is_empty() {
            return CompareMetadata {
                primary_compare_idx: None,
                primary_compare_basis: 0,
                best_value_alt_idx: None,
                best_survival_alt_idx: None,
                best_spike_alt_idx: None,
                margin_value_vs_primary: None,
                margin_spike_vs_primary: None,
                cot_eligible: false,
            };
        }

        // Find chosen index
        let chosen_idx = candidate_infos.iter().position(|c| {
            c.hold == chosen.hold && c.move_piece.same_location(&chosen.mv)
        }).unwrap_or(0);

        let chosen_info = &candidate_infos[chosen_idx];

        // Helper: first distinct non-chosen by rank order
        let first_other_by_rank = candidate_infos.iter().enumerate()
            .find(|(i, _)| *i != chosen_idx)
            .map(|(i, _)| i as u32);

        // Helper: first distinct surviving non-chosen
        let first_surviving_other = candidate_infos.iter().enumerate()
            .find(|(i, c)| *i != chosen_idx && c.survival_pass)
            .map(|(i, _)| i as u32);

        // Helper: highest spike non-chosen
        let best_spike_alt = candidate_infos.iter().enumerate()
            .filter(|(i, _)| *i != chosen_idx)
            .max_by_key(|(_, c)| c.spike_score)
            .map(|(i, _)| i as u32);

        match decision_mode {
            DecisionMode::Book => CompareMetadata {
                primary_compare_idx: None,
                primary_compare_basis: 4, // BOOK
                best_value_alt_idx: None,
                best_survival_alt_idx: None,
                best_spike_alt_idx: None,
                margin_value_vs_primary: None,
                margin_spike_vs_primary: None,
                cot_eligible: false,
            },
            DecisionMode::Normal => {
                let primary = first_other_by_rank;
                let (margin_val, margin_spike) = primary.map(|idx| {
                    let p = &candidate_infos[idx as usize];
                    (
                        Some(chosen_info.eval_score - p.eval_score),
                        Some(chosen_info.spike_score - p.spike_score),
                    )
                }).unwrap_or((None, None));
                CompareMetadata {
                    primary_compare_idx: primary,
                    primary_compare_basis: if primary.is_some() { 1 } else { 0 }, // BEST_OTHER_BY_RANK
                    best_value_alt_idx: first_other_by_rank,
                    best_survival_alt_idx: first_surviving_other,
                    best_spike_alt_idx: best_spike_alt,
                    margin_value_vs_primary: margin_val,
                    margin_spike_vs_primary: margin_spike,
                    cot_eligible: primary.is_some(),
                }
            },
            DecisionMode::SurviveFilter => {
                // Primary is the first candidate before chosen that failed survival
                let primary = candidate_infos.iter().enumerate()
                    .take(chosen_idx)
                    .find(|(_, c)| !c.survival_pass)
                    .map(|(i, _)| i as u32);
                let (margin_val, margin_spike) = primary.map(|idx| {
                    let p = &candidate_infos[idx as usize];
                    (
                        Some(chosen_info.eval_score - p.eval_score),
                        Some(chosen_info.spike_score - p.spike_score),
                    )
                }).unwrap_or((None, None));
                CompareMetadata {
                    primary_compare_idx: primary,
                    primary_compare_basis: 2, // BEST_FILTERED_OUT_BY_SURVIVAL
                    best_value_alt_idx: first_other_by_rank,
                    best_survival_alt_idx: first_surviving_other,
                    best_spike_alt_idx: best_spike_alt,
                    margin_value_vs_primary: margin_val,
                    margin_spike_vs_primary: margin_spike,
                    cot_eligible: true,
                }
            },
            DecisionMode::SpikeBackup => {
                // Primary is second-best spike
                let primary = best_spike_alt;
                let (margin_val, margin_spike) = primary.map(|idx| {
                    let p = &candidate_infos[idx as usize];
                    (
                        Some(chosen_info.eval_score - p.eval_score),
                        Some(chosen_info.spike_score - p.spike_score),
                    )
                }).unwrap_or((None, None));
                CompareMetadata {
                    primary_compare_idx: primary,
                    primary_compare_basis: 3, // SECOND_BEST_SPIKE
                    best_value_alt_idx: first_other_by_rank,
                    best_survival_alt_idx: None, // none survive
                    best_spike_alt_idx: primary,
                    margin_value_vs_primary: margin_val,
                    margin_spike_vs_primary: margin_spike,
                    cot_eligible: primary.is_some(),
                }
            },
        }
    }

    pub fn advance_move(&mut self, mv: FallingPiece) {
        self.tree.advance_move(mv);
    }

    pub fn force_analysis_line(&mut self, path: Vec<FallingPiece>) {
        self.forced_analysis_lines.push(path);
    }
}

impl Thinker {
    pub fn think<E: Evaluator>(self, eval: &E) -> ThinkResult<E::Value, E::Reward, E::Trace> {
        if let Err(possibilities) = self.board.get_next_piece() {
            // Next unknown (implies hold is known) => Speculate
            if self.options.speculate {
                let mut children = EnumMap::new();
                for p in possibilities {
                    let mut b = self.board.clone();
                    b.add_next_piece(p);
                    children[p] = Some(self.make_children(b, eval));
                }
                ThinkResult::Speculated(self.node, children)
            } else {
                ThinkResult::Unmark(self.node)
            }
        } else {
            if self.options.use_hold
                && self.board.hold_piece.is_none()
                && self.board.get_next_next_piece().is_none()
            {
                // Next known, hold unknown => Speculate
                if self.options.speculate {
                    let mut children = EnumMap::new();
                    let possibilities = {
                        let mut b = self.board.clone();
                        b.advance_queue();
                        b.get_next_piece().unwrap_err()
                    };
                    for p in possibilities {
                        let mut b = self.board.clone();
                        b.add_next_piece(p);
                        children[p] = Some(self.make_children(b, eval));
                    }
                    ThinkResult::Speculated(self.node, children)
                } else {
                    ThinkResult::Unmark(self.node)
                }
            } else {
                // Next and hold known
                let children = self.make_children(self.board.clone(), eval);
                ThinkResult::Known(self.node, children)
            }
        }
    }

    fn make_children<E: Evaluator>(
        &self,
        mut board: Board,
        eval: &E,
    ) -> Vec<ChildData<E::Value, E::Reward, E::Trace>> {
        let mut children = vec![];

        let next = board.advance_queue().unwrap();
        let spawned = match self.options.spawn_rule.spawn(next, &board) {
            Some(spawned) => spawned,
            None => return children,
        };

        self.add_children(&mut children, &board, eval, spawned, false);

        if self.options.use_hold {
            let hold = board
                .hold(next)
                .unwrap_or_else(|| board.advance_queue().unwrap());
            if hold == next {
                return children;
            }
            let spawned = match self.options.spawn_rule.spawn(hold, &board) {
                Some(spawned) => spawned,
                None => return children,
            };

            self.add_children(&mut children, &board, eval, spawned, true);
        }

        children
    }

    fn add_children<E: Evaluator>(
        &self,
        children: &mut Vec<ChildData<E::Value, E::Reward, E::Trace>>,
        board: &Board,
        eval: &E,
        spawned: FallingPiece,
        hold: bool,
    ) {
        for mv in find_moves(&board, spawned, self.options.mode) {
            let can_be_hd =
                board.above_stack(&mv.location) && board.column_heights().iter().all(|&y| y < 18);
            let mut result = board.clone();
            let lock = result.lock_piece(mv.location);
            // Don't add deaths by lock out, don't add useless mini tspins
            if !lock.locked_out && !(can_be_hd && lock.placement_kind == PlacementKind::MiniTspin) {
                let move_time = mv.inputs.time + if hold { 1 } else { 0 };
                let (evaluation, reward, trace) = eval.evaluate(&lock, &result, move_time, spawned.kind.0);
                children.push(ChildData {
                    evaluation,
                    reward,
                    trace,
                    board: result,
                    mv: mv.location,
                });
            }
        }
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, Eq, PartialEq, Hash)]
pub enum DecisionMode {
    Book,
    Normal,
    SurviveFilter,
    SpikeBackup,
}

#[derive(Clone, Debug, Serialize, Deserialize, Eq, PartialEq, Hash)]
pub struct CandidateInfo {
    pub move_piece: FallingPiece,
    pub hold: bool,
    pub eval_score: i32,
    pub spike_score: i32,
    pub trace: Option<crate::evaluation::standard::EvalTrace>,
    pub original_rank: u32,
    pub placement_kind: PlacementKind,
    pub b2b: bool,
    pub perfect_clear: bool,
    pub combo: Option<u32>,
    pub garbage_sent: u32,
    pub cleared_lines: ArrayVec<[i32; 4]>,
    pub survival_pass: bool,
    #[serde(skip, default = "default_board_field")]
    pub board_after: [[bool; 10]; 40],
    #[serde(skip)]
    pub pv: Vec<(FallingPiece, LockResult)>,
}

fn default_board_field() -> [[bool; 10]; 40] {
    [[false; 10]; 40]
}

#[derive(Clone, Debug, Serialize, Deserialize, Eq, PartialEq, Hash)]
pub struct CompareMetadata {
    pub primary_compare_idx: Option<u32>,
    /// 0=NONE, 1=BEST_OTHER_BY_RANK, 2=BEST_FILTERED_OUT_BY_SURVIVAL, 3=SECOND_BEST_SPIKE, 4=BOOK
    pub primary_compare_basis: u8,
    pub best_value_alt_idx: Option<u32>,
    pub best_survival_alt_idx: Option<u32>,
    pub best_spike_alt_idx: Option<u32>,
    pub margin_value_vs_primary: Option<i32>,
    pub margin_spike_vs_primary: Option<i32>,
    pub cot_eligible: bool,
}

#[derive(Clone, Debug, Serialize, Deserialize, Eq, PartialEq, Hash)]
pub struct Info {
    pub nodes: u32,
    pub depth: u32,
    pub original_rank: u32,
    pub decision_mode: DecisionMode,
    pub plan: Vec<(FallingPiece, LockResult)>,
    pub candidates: Vec<CandidateInfo>,
    pub first_survival_pass: bool,
    pub any_survival_pass: bool,
    pub compare_metadata: CompareMetadata,
}
