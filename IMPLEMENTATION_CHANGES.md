# 구현 변경 사항 정리

원래 계획과 실제 구현의 차이를 기록한 문서.

---

## 1. CoT 생성 방식

### 원래 계획
- `BotState::suggest_move()` 시점에 chosen/runner-up 후보를 잡아 **오프라인**에서 trace 계산 후 CoT 렌더
- `Standard::evaluate()`에 `evaluate_trace()` 함수를 별도 추가해 항별 기여도를 `EvalTrace`로 분리
- 출력 형식: `goal` (상위 전략 1개) + `reasons` (토큰 1~3개) + `cot` (1문장) + `action`
- 우세 항목 기준: `|contrib_chosen - contrib_runner|` top-k
- 마진이 작은 샘플은 드랍

### 실제 구현
- `cc_poll_next_move()` 반환 시점에 **온라인**으로 CoT 생성 (오프라인 2단 파이프라인 없음)
- `evaluate_trace()` 별도 추가 없이, 기존 `CCCandidate` 구조체에 `has_trace` 플래그와 `CCEvalTrace` 필드를 추가
- `CCEvalTrace` 필드: `clear_score`, `tspin_score`, `pc_score`, `b2b_score`, `combo_score`, `wasted_t`, `height_penalty`, `jeopardy_penalty`, `well_score`, `tslot_score[4]`, `bumpiness_penalty`, `hole_penalty`, `covered_penalty`, `row_transitions`
- 출력 형식: 단일 자연어 문장 1개 (goal/reasons 토큰 구조 없음)
- 우세 항목 기준: 절댓값이 가장 큰 delta 1개만 선택 (top-k 아님)
- 마진 기반 드랍 없음
- 가능한 CoT 문장 수: **15가지** 고정 템플릿

---

## 2. 의도 태그 시스템

### 원래 계획
- `ATTACK_EXEC`, `ATTACK_PREP`, `DOWNSTACK`, `SURVIVAL`, `SPIKE_FOR_ESCAPE` 5개 태그
- value vs spike를 이용해 의도 분류
- `pick_move`의 생존 조건 활성 여부를 태그에 반영

### 실제 구현
- 의도 태그 시스템 미구현
- goal/reasons 필드 없음
- `cotReason` 단일 문자열 필드만 존재

---

## 3. 오프닝북 처리

### 원래 계획
- 오프닝북 사용 시 해당 샘플을 제외하거나 별도 태그로 분리

### 실제 구현
- `cc_launch_async()` 호출 시 book 파라미터를 `IntPtr.Zero`로 전달 → 오프닝북 미사용
- 별도 태그 없음

---

## 4. Action 표현 형식

### 원래 계획
- `(hold, col, rot)` 형식: hold 여부 + 열(0~9) + 회전(0~3)
- vocabulary 최대 80개 action 토큰

### 실제 구현
- `{hold: bool, x: byte[4], y: byte[4]}` 형식: hold 여부 + 4개 셀의 좌표
- `CCMove.expected_x`, `CCMove.expected_y` 값을 그대로 기록
- action 토큰 vocabulary 미정의

---

## 5. 데이터셋 저장 구조

### 원래 계획
- 별도 명시 없음

### 실제 구현
- JSONL 형식, 게임 단위로 1줄
- 스텝당 필드: `canHold`, `holdedPiece`, `previewPiece`, `currentPiece`, `board`, `action`, `cotReason`
- `board`는 행 번호를 키로 갖는 객체 (상단 행부터 내림차순)
- 파일 분할: `dataset_XXXX.jsonl`, `gamesPerFile` 단위로 새 파일 생성 (기본값 100게임)
- 저장 경로: 프로젝트 루트 하위 `datasets/` 폴더

---

## 6. 데이터 수집 파이프라인

### 원래 계획
- 1단(엔진): 결정 순간 패킷 덤프 (state, candidates top-K, chosen, runner-up, principal variation)
- 2단(오프라인): trace 계산 + CoT 렌더

### 실제 구현
- 단일 온라인 파이프라인
- 피스 스폰 시 `SaveData()` → `pendingStep` 생성
- `PollNextMove()` 반환 시 `AttachCoT()`, `AttachAction()` → `pendingStep`에 후부착
- 100스텝 완료 시 `FinishCurrentGame()` → JSONL 저장 → `board.RestartGame()`으로 자동 재시작

---

## 7. Hold 관련 데이터 수집

### 원래 계획
- 별도 명시 없음

### 실제 구현
- 초기 구현에서 `TryHoldPiece()` 내 `UpdateBoard()` 호출로 홀드 교체 스폰이 별도 스텝으로 기록되는 버그 존재
- `TryHoldPiece()`에서 `DataHandler.Instance?.UpdateBoard()` 제거로 수정
- 홀드 사용 시 교체 피스 스폰은 스텝으로 기록되지 않음
