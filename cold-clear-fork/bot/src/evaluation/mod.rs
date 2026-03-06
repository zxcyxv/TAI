use libtetris::{Board, LockResult, Piece};

use crate::dag::MoveCandidate;

pub mod standard;
pub use self::standard::Standard;
pub mod changed;

pub trait Evaluator: Send + Sync {
    type Value: Evaluation<Self::Reward> + Send + 'static;
    type Reward: Clone + Send + 'static;
    type Trace: Clone + Default + Send + 'static;

    fn name(&self) -> String;

    fn evaluate(
        &self,
        lock: &LockResult,
        board: &Board,
        move_time: u32,
        placed: Piece,
    ) -> (Self::Value, Self::Reward, Self::Trace);

    fn get_value(&self, _val: &Self::Value) -> i32 {
        0
    }

    fn into_standard_trace(&self, _trace: &Self::Trace) -> Option<crate::evaluation::standard::EvalTrace> {
        None
    }

    fn pick_move(
        &self,
        candidates: Vec<MoveCandidate<Self::Value, Self::Trace>>,
        _incoming: u32,
    ) -> MoveCandidate<Self::Value, Self::Trace> {
        candidates.into_iter().next().unwrap()
    }
}

pub trait Evaluation<R>:
    Eq
    + Ord
    + Default
    + Clone
    + std::ops::Add<R, Output = Self>
    + std::ops::Div<usize, Output = Self>
    + std::ops::Mul<usize, Output = Self>
    + std::ops::Add<Output = Self>
{
    fn modify_death(self) -> Self;
    fn weight(self, min: &Self, rank: usize) -> i64;

    fn improve(&mut self, other: Self);
}

impl<T: Evaluator> Evaluator for std::sync::Arc<T> {
    type Value = T::Value;
    type Reward = T::Reward;
    type Trace = T::Trace;

    fn name(&self) -> String {
        (**self).name()
    }

    fn evaluate(
        &self,
        lock: &LockResult,
        board: &Board,
        move_time: u32,
        placed: Piece,
    ) -> (T::Value, T::Reward, T::Trace) {
        (**self).evaluate(lock, board, move_time, placed)
    }

    fn get_value(&self, val: &Self::Value) -> i32 {
        (**self).get_value(val)
    }

    fn into_standard_trace(&self, trace: &Self::Trace) -> Option<crate::evaluation::standard::EvalTrace> {
        (**self).into_standard_trace(trace)
    }

    fn pick_move(
        &self,
        candidates: Vec<MoveCandidate<Self::Value, Self::Trace>>,
        incoming: u32,
    ) -> MoveCandidate<Self::Value, Self::Trace> {
        (**self).pick_move(candidates, incoming)
    }
}
