use std::ffi::CStr;
use std::mem::MaybeUninit;
use std::os::raw::c_char;
use std::sync::Arc;

use cold_clear::PcPriority;
use enumset::EnumSet;
use libtetris::{
    Board, FallingPiece, LockResult, MovementMode, Piece, PieceMovement, SpawnRule, TspinStatus,
};

type CCAsyncBot = cold_clear::Interface;

type CCBook = cold_clear::Book;

macro_rules! cenum {
    (@match $v:ident $name:ident $($item:ident => $to:expr),*) => {
        #[allow(unreachable_patterns)]
        match $v {
            $(
                $name::$item => $to,
            )*
            _ => unreachable!()
        }
    };
    (@rmatch $v:ident $name:ident $($item:ident => $to:pat),*) => {
        #[allow(unreachable_patterns)]
        match $v {
            $(
                $to => $name::$item,
            )*
            _ => unreachable!()
        }
    };
    (@enum $name:ident $($item:ident => $to:expr),*) => {
        #[derive(Copy, Clone, Debug)]
        #[allow(non_camel_case_types)]
        #[repr(C)]
        enum $name {
            $($item,)*
        }
    };
    ($(enum $name:ident => $t:ty { $($rest:tt)* })*) => {
        $(
        cenum!(@enum $name $($rest)*);

        impl From<$name> for $t {
            fn from(v: $name) -> $t {
                cenum! { @match
                    v $name
                    $($rest)*
                }
            }
        }

        impl From<$t> for $name {
            fn from(v: $t) -> $name {
                cenum! { @rmatch
                    v $name
                    $($rest)*
                }
            }
        }
        )*
    };
}

cenum! {
    enum CCPiece => Piece {
        CC_I => Piece::I,
        CC_O => Piece::O,
        CC_T => Piece::T,
        CC_L => Piece::L,
        CC_J => Piece::J,
        CC_S => Piece::S,
        CC_Z => Piece::Z
    }

    enum CCTspinStatus => TspinStatus {
        CC_NONE => TspinStatus::None,
        CC_MINI => TspinStatus::Mini,
        CC_FULL => TspinStatus::Full
    }

    enum CCMovement => PieceMovement {
        CC_LEFT => PieceMovement::Left,
        CC_RIGHT => PieceMovement::Right,
        CC_CW => PieceMovement::Cw,
        CC_CCW => PieceMovement::Ccw,
        CC_DROP => PieceMovement::SonicDrop
    }

    enum CCSpawnRule => SpawnRule {
        CC_ROW_19_OR_20 => SpawnRule::Row19Or20,
        CC_ROW_21_AND_FALL => SpawnRule::Row21AndFall
    }

    enum CCMovementMode => MovementMode {
        CC_0G => MovementMode::ZeroG,
        CC_20G => MovementMode::TwentyG,
        CC_HARD_DROP_ONLY => MovementMode::HardDropOnly
    }

    enum CCPcPriority => Option<PcPriority> {
        CC_PC_OFF => None,
        CC_PC_FASTEST => Some(PcPriority::Fastest),
        CC_PC_ATTACK => Some(PcPriority::HighestAttack)
    }
}

#[repr(C)]
#[derive(Copy, Clone, Debug)]
#[allow(non_camel_case_types)]
enum CCBotPollStatus {
    CC_MOVE_PROVIDED,
    CC_WAITING,
    CC_BOT_DEAD,
}

#[repr(C)]
#[derive(Copy, Clone, Debug)]
struct CCMove {
    hold: bool,
    expected_x: [u8; 4],
    expected_y: [u8; 4],
    movement_count: u8,
    movements: [CCMovement; 32],
    nodes: u32,
    depth: u32,
    original_rank: u32,
}

#[repr(C)]
#[derive(Copy, Clone, Debug)]
struct CCPlanPlacement {
    piece: CCPiece,
    tspin: CCTspinStatus,
    expected_x: [u8; 4],
    expected_y: [u8; 4],
    cleared_lines: [i32; 4],
}

#[repr(C)]
#[derive(Copy, Clone, Debug)]
pub struct CCEvalTrace {
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

#[repr(C)]
#[derive(Copy, Clone, Debug)]
pub struct CCCandidate {
    pub piece: CCPiece,
    pub expected_x: [u8; 4],
    pub expected_y: [u8; 4],
    pub hold: bool,
    pub eval_score: i32,
    pub has_trace: bool,
    pub trace: CCEvalTrace,
}

#[repr(C)]
struct CCOptions {
    mode: CCMovementMode,
    spawn_rule: CCSpawnRule,
    pcloop: CCPcPriority,
    min_nodes: u32,
    max_nodes: u32,
    threads: u32,
    use_hold: bool,
    speculate: bool,
}

#[repr(C)]
struct CCWeights {
    back_to_back: i32,
    bumpiness: i32,
    bumpiness_sq: i32,
    row_transitions: i32,
    height: i32,
    top_half: i32,
    top_quarter: i32,
    jeopardy: i32,
    cavity_cells: i32,
    cavity_cells_sq: i32,
    overhang_cells: i32,
    overhang_cells_sq: i32,
    covered_cells: i32,
    covered_cells_sq: i32,
    tslot: [i32; 4],
    well_depth: i32,
    max_well_depth: i32,
    well_column: [i32; 10],

    b2b_clear: i32,
    clear1: i32,
    clear2: i32,
    clear3: i32,
    clear4: i32,
    tspin1: i32,
    tspin2: i32,
    tspin3: i32,
    mini_tspin1: i32,
    mini_tspin2: i32,
    perfect_clear: i32,
    combo_garbage: i32,
    move_time: i32,
    wasted_t: i32,

    use_bag: bool,
    timed_jeopardy: bool,
    stack_pc_damage: bool,
}

fn convert_hold(hold: *mut CCPiece) -> Option<Piece> {
    if hold.is_null() {
        None
    } else {
        Some(unsafe { *hold }.into())
    }
}

fn convert_from_c_options(options: &CCOptions) -> cold_clear::Options {
    cold_clear::Options {
        max_nodes: options.max_nodes,
        min_nodes: options.min_nodes,
        use_hold: options.use_hold,
        speculate: options.speculate,
        pcloop: options.pcloop.into(),
        mode: options.mode.into(),
        spawn_rule: options.spawn_rule.into(),
        threads: options.threads,
    }
}

fn convert_from_c_weights(weights: &CCWeights) -> cold_clear::evaluation::Standard {
    cold_clear::evaluation::Standard {
        back_to_back: weights.back_to_back,
        bumpiness: weights.bumpiness,
        bumpiness_sq: weights.bumpiness_sq,
        row_transitions: weights.row_transitions,
        height: weights.height,
        top_half: weights.top_half,
        top_quarter: weights.top_quarter,
        jeopardy: weights.jeopardy,
        cavity_cells: weights.cavity_cells,
        cavity_cells_sq: weights.cavity_cells_sq,
        overhang_cells: weights.overhang_cells,
        overhang_cells_sq: weights.overhang_cells_sq,
        covered_cells: weights.covered_cells,
        covered_cells_sq: weights.covered_cells_sq,
        tslot: weights.tslot,
        well_depth: weights.well_depth,
        max_well_depth: weights.max_well_depth,
        well_column: weights.well_column,

        b2b_clear: weights.b2b_clear,
        clear1: weights.clear1,
        clear2: weights.clear2,
        clear3: weights.clear3,
        clear4: weights.clear4,
        tspin1: weights.tspin1,
        tspin2: weights.tspin2,
        tspin3: weights.tspin3,
        mini_tspin1: weights.mini_tspin1,
        mini_tspin2: weights.mini_tspin2,
        perfect_clear: weights.perfect_clear,
        combo_garbage: weights.combo_garbage,
        move_time: weights.move_time,
        wasted_t: weights.wasted_t,

        use_bag: weights.use_bag,
        timed_jeopardy: weights.timed_jeopardy,
        stack_pc_damage: weights.stack_pc_damage,
        sub_name: None,
    }
}

#[no_mangle]
unsafe extern "C" fn cc_launch_with_board_async(
    options: &CCOptions,
    weights: &CCWeights,
    book: *const CCBook,
    field: &[[bool; 10]; 40],
    bag_remain: u32,
    hold: *mut CCPiece,
    b2b: bool,
    combo: u32,
    pieces: *const CCPiece,
    count: u32,
) -> *mut CCAsyncBot {
    let mut board = Board::new_with_state(
        *field,
        EnumSet::try_from_u32(bag_remain).unwrap_or_default(),
        convert_hold(hold),
        b2b,
        combo,
    );
    for i in 0..count as usize {
        board.add_next_piece((*pieces.add(i)).into());
    }
    let book = if book.is_null() {
        None
    } else {
        Arc::increment_strong_count(book);
        Some(Arc::from_raw(book))
    };
    Box::into_raw(Box::new(cold_clear::Interface::launch(
        board,
        convert_from_c_options(options),
        convert_from_c_weights(weights),
        book,
    )))
}

#[no_mangle]
unsafe extern "C" fn cc_launch_async(
    options: &CCOptions,
    weights: &CCWeights,
    book: *const CCBook,
    pieces: *const CCPiece,
    count: u32,
) -> *mut CCAsyncBot {
    let mut board = Board::new();
    for i in 0..count as usize {
        board.add_next_piece((*pieces.add(i)).into());
    }
    let book = if book.is_null() {
        None
    } else {
        Arc::increment_strong_count(book);
        Some(Arc::from_raw(book))
    };
    Box::into_raw(Box::new(cold_clear::Interface::launch(
        board,
        convert_from_c_options(options),
        convert_from_c_weights(weights),
        book,
    )))
}

#[no_mangle]
extern "C" fn cc_destroy_async(bot: *mut CCAsyncBot) {
    unsafe {
        Box::from_raw(bot);
    }
}

#[no_mangle]
extern "C" fn cc_reset_async(
    bot: &mut CCAsyncBot,
    field: &[[bool; 10]; 40],
    b2b: bool,
    combo: u32,
) {
    bot.reset(*field, b2b, combo);
}

#[no_mangle]
extern "C" fn cc_add_next_piece_async(bot: &mut CCAsyncBot, piece: CCPiece) {
    bot.add_next_piece(piece.into());
}

#[no_mangle]
extern "C" fn cc_request_next_move(bot: &mut CCAsyncBot, incoming: u32) {
    bot.suggest_next_move(incoming);
}

fn convert_plan_placement(
    (falling_piece, lock_result): &(FallingPiece, LockResult),
) -> CCPlanPlacement {
    let mut expected_x = [0; 4];
    let mut expected_y = [0; 4];
    for (i, &(x, y)) in falling_piece.cells().iter().enumerate() {
        expected_x[i] = x as u8;
        expected_y[i] = y as u8;
    }

    let mut cleared_lines = [-1; 4];
    for (i, &cl) in lock_result.cleared_lines.iter().enumerate() {
        cleared_lines[i] = cl;
    }

    CCPlanPlacement {
        piece: falling_piece.kind.0.into(),
        tspin: falling_piece.tspin.into(),
        expected_x: expected_x,
        expected_y: expected_y,
        cleared_lines: cleared_lines,
    }
}

fn convert_plan(
    info: &cold_clear::Info,
    plan: *mut MaybeUninit<CCPlanPlacement>,
    plan_length: *mut u32,
) {
    if !plan.is_null() && !plan_length.is_null() {
        let plan_length = unsafe { &mut *plan_length };
        let plan = unsafe { std::slice::from_raw_parts_mut(plan, *plan_length as usize) };
        let n = info.plan().len().min(plan.len());
        for i in 0..n {
            plan[i] = MaybeUninit::new(convert_plan_placement(&info.plan()[i]));
        }
        *plan_length = n as u32;
    }
}

fn convert_candidates(
    info: &cold_clear::Info,
    candidates_out: *mut MaybeUninit<CCCandidate>,
    candidates_length: *mut u32,
) {
    if !candidates_out.is_null() && !candidates_length.is_null() {
        if let cold_clear::Info::Normal(ninfo) = info {
            let candidates_length = unsafe { &mut *candidates_length };
            let candidates_out = unsafe { std::slice::from_raw_parts_mut(candidates_out, *candidates_length as usize) };
            let n = ninfo.candidates.len().min(candidates_out.len());
            for i in 0..n {
                let cand = &ninfo.candidates[i];
                let mut expected_x = [0; 4];
                let mut expected_y = [0; 4];
                for (j, &(x, y)) in cand.move_piece.cells().iter().enumerate() {
                    expected_x[j] = x as u8;
                    expected_y[j] = y as u8;
                }
                
                let (has_trace, trace) = match &cand.trace {
                    Some(t) => (true, CCEvalTrace {
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
                    }),
                    None => (false, unsafe { std::mem::zeroed() }),
                };

                candidates_out[i] = MaybeUninit::new(CCCandidate {
                    piece: cand.move_piece.kind.0.into(),
                    expected_x,
                    expected_y,
                    hold: cand.hold,
                    eval_score: cand.eval_score,
                    has_trace,
                    trace,
                });
            }
            *candidates_length = n as u32;
        } else {
            unsafe { *candidates_length = 0; }
        }
    }
}

fn convert(m: libtetris::Move, info: cold_clear::Info) -> CCMove {
    let mut expected_x = [0; 4];
    let mut expected_y = [0; 4];
    for (i, &(x, y)) in m.expected_location.cells().iter().enumerate() {
        expected_x[i] = x as u8;
        expected_y[i] = y as u8;
    }
    let mut movements = [CCMovement::CC_DROP; 32];
    for (i, &mv) in m.inputs.iter().enumerate() {
        movements[i] = mv.into();
    }
    CCMove {
        hold: m.hold,
        expected_x,
        expected_y,
        movement_count: m.inputs.len() as u8,
        movements,
        nodes: match &info {
            cold_clear::Info::Normal(info) => info.nodes as u32,
            cold_clear::Info::PcLoop(_) => 0,
            cold_clear::Info::Book => 0,
        },
        depth: match &info {
            cold_clear::Info::Normal(info) => info.depth as u32,
            cold_clear::Info::PcLoop(info) => info.depth as u32,
            cold_clear::Info::Book => 0,
        },
        original_rank: match &info {
            cold_clear::Info::Normal(info) => info.original_rank as u32,
            cold_clear::Info::PcLoop(_) => 0,
            cold_clear::Info::Book => 0,
        },
    }
}

#[no_mangle]
extern "C" fn cc_poll_next_move(
    bot: &mut CCAsyncBot,
    mv: *mut CCMove,
    plan: *mut MaybeUninit<CCPlanPlacement>,
    plan_length: *mut u32,
    candidates: *mut MaybeUninit<CCCandidate>,
    candidates_length: *mut u32,
) -> CCBotPollStatus {
    match bot.poll_next_move() {
        Ok((m, info)) => {
            bot.play_next_move(m.expected_location);
            convert_plan(&info, plan, plan_length);
            convert_candidates(&info, candidates, candidates_length);
            unsafe { mv.write(convert(m, info)) };
            CCBotPollStatus::CC_MOVE_PROVIDED
        }
        Err(cold_clear::BotPollState::Waiting) => CCBotPollStatus::CC_WAITING,
        Err(cold_clear::BotPollState::Dead) => CCBotPollStatus::CC_BOT_DEAD,
    }
}

#[no_mangle]
extern "C" fn cc_block_next_move(
    bot: &mut CCAsyncBot,
    mv: *mut CCMove,
    plan: *mut MaybeUninit<CCPlanPlacement>,
    plan_length: *mut u32,
    candidates: *mut MaybeUninit<CCCandidate>,
    candidates_length: *mut u32,
) -> CCBotPollStatus {
    match bot.block_next_move() {
        Some((m, info)) => {
            bot.play_next_move(m.expected_location);
            convert_plan(&info, plan, plan_length);
            convert_candidates(&info, candidates, candidates_length);
            unsafe { mv.write(convert(m, info)) };
            CCBotPollStatus::CC_MOVE_PROVIDED
        }
        None => CCBotPollStatus::CC_BOT_DEAD,
    }
}

#[no_mangle]
unsafe extern "C" fn cc_default_options(options: *mut CCOptions) {
    let o = cold_clear::Options::default();
    options.write(CCOptions {
        max_nodes: o.max_nodes,
        min_nodes: o.min_nodes,
        use_hold: o.use_hold,
        speculate: o.speculate,
        pcloop: o.pcloop.into(),
        mode: o.mode.into(),
        spawn_rule: o.spawn_rule.into(),
        threads: o.threads,
    });
}

fn convert_weights(w: cold_clear::evaluation::Standard) -> CCWeights {
    CCWeights {
        back_to_back: w.back_to_back,
        bumpiness: w.bumpiness,
        bumpiness_sq: w.bumpiness_sq,
        row_transitions: w.row_transitions,
        height: w.height,
        top_half: w.top_half,
        top_quarter: w.top_quarter,
        jeopardy: w.jeopardy,
        cavity_cells: w.cavity_cells,
        cavity_cells_sq: w.cavity_cells_sq,
        overhang_cells: w.overhang_cells,
        overhang_cells_sq: w.overhang_cells_sq,
        covered_cells: w.covered_cells,
        covered_cells_sq: w.covered_cells_sq,
        tslot: w.tslot,
        well_depth: w.well_depth,
        max_well_depth: w.max_well_depth,
        well_column: w.well_column,

        b2b_clear: w.b2b_clear,
        clear1: w.clear1,
        clear2: w.clear2,
        clear3: w.clear3,
        clear4: w.clear4,
        tspin1: w.tspin1,
        tspin2: w.tspin2,
        tspin3: w.tspin3,
        mini_tspin1: w.mini_tspin1,
        mini_tspin2: w.mini_tspin2,
        perfect_clear: w.perfect_clear,
        combo_garbage: w.combo_garbage,
        move_time: w.move_time,
        wasted_t: w.wasted_t,

        use_bag: w.use_bag,
        timed_jeopardy: w.timed_jeopardy,
        stack_pc_damage: w.stack_pc_damage,
    }
}

#[no_mangle]
unsafe extern "C" fn cc_default_weights(weights: *mut CCWeights) {
    weights.write(convert_weights(cold_clear::evaluation::Standard::default()));
}

#[no_mangle]
unsafe extern "C" fn cc_fast_weights(weights: *mut CCWeights) {
    weights.write(convert_weights(
        cold_clear::evaluation::Standard::fast_config(),
    ));
}

#[no_mangle]
unsafe extern "C" fn cc_load_book_from_file(path: *const c_char) -> *const CCBook {
    let result = (|| {
        let path = CStr::from_ptr(path).to_str().ok()?;
        Some(Arc::new(cold_clear::Book::load(path).ok()?))
    })();
    match result {
        Some(book) => Arc::into_raw(book),
        None => std::ptr::null(),
    }
}

#[no_mangle]
unsafe extern "C" fn cc_load_book_from_memory(data: *const c_char, length: u32) -> *const CCBook {
    let data = std::slice::from_raw_parts(data as *const u8, length as usize);
    match cold_clear::MemoryBook::load(data).ok() {
        Some(book) => Arc::into_raw(Arc::new(book.into())),
        None => std::ptr::null(),
    }
}

#[no_mangle]
unsafe extern "C" fn cc_destroy_book(book: *const CCBook) {
    Arc::from_raw(book);
}
