using UnityEngine;

public class Piece : MonoBehaviour
{
    public Board board { get; private set; }
    public Vector3Int[] cells { get; private set; }
    public Vector3Int position { get; private set; }
    public TetrominoData data { get; private set; }
    public int rotationIndex { get; private set; }
    public ControlCommand nextCommand { get; private set; }

    [Header("Handling Settings")]
    [Tooltip("Delayed Auto Shift (seconds): Time before auto-repeat starts")]
    public float das = 0.145f; 
    [Tooltip("Auto Repeat Rate (seconds): Time between steps. 0 = Instant")]
    public float arr = 0f;
    [Tooltip("DAS Cut Delay (seconds): Delay added on spawn")]
    public float dcd = 0.017f;
    [Tooltip("Soft Drop Factor: Gravity multiplier (e.g., 20x)")]
    public float sdf = 9999f;

    public float stepDelay = .4f;
    public float lockDelay = 0.25f;

    private float stepTimer;
    private float lockTime;
    
    // Movement Handling Variables
    private float dasTimer;
    private float arrTimer;
    private Vector2Int moveDirection;
    private bool isDasActive;
    private bool wasLastActionRotation;


    public void Initialize(Board board, Vector3Int position, TetrominoData data)
    { 
        this.board = board;
        this.position = position;
        this.data = data;
        this.rotationIndex = 0;
        this.nextCommand = ControlCommand.None;
        
        // DCD 적용: 스폰 시 DAS 타이머를 음수로 시작하여 지연 효과 부여
        this.dasTimer = -dcd; 
        this.arrTimer = 0f;
        this.stepTimer = 0f;
        this.lockTime = 0f;
        this.isDasActive = false;
        this.moveDirection = Vector2Int.zero;
        this.wasLastActionRotation = false;

        if (data.cells == null) Debug.LogWarning("[Piece] Data Cell is null");
        cells ??= new Vector3Int[data.cells.Length];
        
        for (int i = 0; i < data.cells.Length; i++) 
            cells[i] = (Vector3Int)data.cells[i];
    }

    public void UpdateAfter()
    {
        board.Clear(this);

        lockTime += Time.deltaTime;

        if (ColdClearAgent.Instance.enableBot) RunCommand();
        else {
            if (HandleHold())
            {
                board.Set(this);
                return;
            }

            HandleRotation();
            HandleMoveInputs();
            HandleGravity();
            HandleHardDrop();
        }

        board.Set(this);
    }

    private void RunCommand()
    {
        if (nextCommand == ControlCommand.None) return;
        switch (nextCommand)
        {
            case ControlCommand.MoveLeft:
                Move(Vector2Int.left);
                break;
            case ControlCommand.MoveRight:
                Move(Vector2Int.right);
                break;
            case ControlCommand.SoftDrop:
                SoftDrop();
                break;
            case ControlCommand.HardDrop:
                HardDrop();
                break;
            case ControlCommand.Hold:
                board.TryHoldPiece();
                break;
            case ControlCommand.RotateLeft:
                Rotate(-1);
                break;
            case ControlCommand.RotateRight:
                Rotate(1);
                break;
            case ControlCommand.Rotate180:
                Rotate180();
                break;
            default:
                break;
        }

        Debug.Log("[Piece] Run Command: " + nextCommand.ToString());
        nextCommand = ControlCommand.None;
    }

    public bool EnqueueCommand(ControlCommand command)
    {
        if (nextCommand != ControlCommand.None) return false;
        nextCommand = command;
        return true;
    }

    private void HandleRotation()
    {
        // 시계 반대 방향
        if (Input.GetKeyDown(KeyCode.Z))
        {
            Rotate(-1);
        }
        // 시계 방향
        if (Input.GetKeyDown(KeyCode.X))
        {
            Rotate(1);
        }
        // 180도 회전
        if (Input.GetKeyDown(KeyCode.A))
        {
            Rotate180();
        }
    }

    private bool HandleHold()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            return board.TryHoldPiece();
        }

        return false;
    }

    private void HandleMoveInputs()
    {
        // 좌우 입력 확인 (우선순위: 최근 입력 혹은 동시 입력 시 상쇄 등 로직 필요하지만 간단히 처리)
        int xInput = 0;
        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.L)) xInput = 1;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.J)) xInput = -1;

        // 키를 뗐거나 방향이 바뀌면 DAS 초기화
        if (xInput == 0 || moveDirection.x != xInput)
        {
            isDasActive = false;
            dasTimer = -dcd; // DCD 재적용 여부는 게임 룰에 따라 다름 (보통 스폰시에만 적용하나 여기선 안전하게 리셋)
            arrTimer = 0f;
            
            if (xInput != 0) // 방향 전환 즉시 이동
            {
                moveDirection = new Vector2Int(xInput, 0);
                Move(moveDirection);
                dasTimer = 0f; // 첫 이동 후 DAS 충전 시작
            }
            else
            {
                moveDirection = Vector2Int.zero;
            }
            return;
        }

        // 키를 누르고 있는 중
        if (xInput != 0)
        {
            dasTimer += Time.deltaTime;

            if (!isDasActive)
            {
                // DAS 충전 완료 확인
                if (dasTimer >= das)
                {
                    isDasActive = true;
                    arrTimer = 0f; // DAS 발동 즉시 ARR 타이머 시작
                }
            }

            if (isDasActive)
            {
                // ARR이 0이면 즉시 벽까지 이동 (Instant DAS)
                if (arr <= 0f)
                {
                    while (Move(moveDirection)) { }
                }
                else
                {
                    arrTimer += Time.deltaTime;
                    if (arrTimer >= arr)
                    {
                        // 프레임 드랍 등으로 시간이 많이 지났을 경우를 대비해 while 대신 반복 횟수 계산 가능
                        // 여기서는 간단히 한 칸씩 이동
                        Move(moveDirection);
                        arrTimer -= arr; // 잔여 시간 유지
                    }
                }
            }
        }
    }

    private void HandleGravity()
    {
        float currentGravity = stepDelay;

        // SDF 적용
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.K))
        {
            // SDF가 매우 크면 즉시 바닥으로 (Sonic Drop)
            if (sdf >= 40f) // 40 이상이면 거의 즉시 이동으로 간주
            {
                currentGravity = 0f; 
                while (Move(Vector2Int.down)) { }
            }
            else
            {
                currentGravity = stepDelay / sdf;
            }
        }

        stepTimer += Time.deltaTime;

        if (stepTimer >= currentGravity)
        {
            Step();
            stepTimer = 0f; // 잔여 시간 무시하고 리셋 (일반적인 테트리스 방식)
        }
    }

    private void HandleHardDrop()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HardDrop();
        }
    }

    private void Step()
    {
        // stepTimer는 HandleGravity에서 처리하므로 여기서는 이동만 담당
        Move(Vector2Int.down);

        if (lockTime >= lockDelay)
        {
            Lock();
        }
    }

    private void SoftDrop()
    {
        while (Move(Vector2Int.down)) continue;
    }

    private void HardDrop()
    {
        while (Move(Vector2Int.down)) continue;

        Lock();
    }

    private void Lock()
    {
        SpinKind spinKind = DetectSpinKind();
        Spin lockSpin = ConvertToDetailedSpin(spinKind);

        board.Set(this);
        int clearedLines = board.ClearLines();
        board.NotifyPieceLocked();
        board.RecordLastLockSpin(lockSpin);
        ScoreResult scoreResult = board.ResolveScore(clearedLines, lockSpin);

        if (clearedLines > 0 || lockSpin != Spin.None)
        {
            string allClearTag = scoreResult.allClear ? " / ALL CLEAR" : "";
            Debug.Log($"[Score] +{scoreResult.scoreGain} / ATK +{scoreResult.attack} / {lockSpin} / Lines {clearedLines} / B2B {scoreResult.backToBackChain} / Combo {scoreResult.combo}{allClearTag}");
        }

        DataManager.Instance?.GetBoardData();
        DataManager.Instance?.GetHoldData();
        DataManager.Instance?.GetPreviewData();

        if (DataHandler.Instance != null && DataHandler.Instance.ConsumePendingRestartAfterLock())
        {
            return;
        }

        board.SpawnPiece();
    }

    private bool Move(Vector2Int translation, bool fromRotationKick = false)
    {
        Vector3Int newPosition = position;
        newPosition.x += translation.x;
        newPosition.y += translation.y;

        bool vaild = board.IsVaildPosition(this, newPosition);

        if (vaild)
        { 
            position = newPosition;
            lockTime = 0f;
            if (!fromRotationKick)
            {
                wasLastActionRotation = false;
            }
        }

        return vaild;
    }

    private void Rotate(int direction)
    {
        int originalRotation = rotationIndex;
        rotationIndex = Wrap(rotationIndex + direction, 0, 4);

        ApplyRotationMatrix(direction);

        bool rotated = TestWallKicks(originalRotation, direction);
        if (!rotated)
        {
            rotationIndex = originalRotation;
            ApplyRotationMatrix(-direction);
            return;
        }

        wasLastActionRotation = true;
    }

    private void Rotate180()
    {
        int originalRotation = rotationIndex;
        int targetRotation = Wrap(rotationIndex + 2, 0, 4);

        // 180도는 90도 회전을 두 번 적용
        ApplyRotationMatrix(1);
        ApplyRotationMatrix(1);

        bool rotated = TestWallKicks180(originalRotation, targetRotation);
        if (!rotated)
        {
            // 롤백
            ApplyRotationMatrix(-1);
            ApplyRotationMatrix(-1);
            return;
        }

        wasLastActionRotation = true;
    }

    private void ApplyRotationMatrix(int direction)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            // Offset
            Vector3 cell = cells[i];

            // SRS Kick Table Calculation
            int x, y;
            switch (data.tetromino)
            {
                // case 흘리기
                case Tetromino.I:
                case Tetromino.O: // I, O Case
                    cell.x -= .5f;
                    cell.y -= .5f;
                    x = Mathf.CeilToInt((cell.x * Data.RotationMatrix[0] * direction) + (cell.y * Data.RotationMatrix[1] * direction));
                    y = Mathf.CeilToInt((cell.x * Data.RotationMatrix[2] * direction) + (cell.y * Data.RotationMatrix[3] * direction));
                    break;

                default:
                    x = Mathf.RoundToInt((cell.x * Data.RotationMatrix[0] * direction) + (cell.y * Data.RotationMatrix[1] * direction));
                    y = Mathf.RoundToInt((cell.x * Data.RotationMatrix[2] * direction) + (cell.y * Data.RotationMatrix[3] * direction));
                    break;
            }

            cells[i] = new(x, y, 0);
        }
    }

    private bool TestWallKicks(int rotationIndex, int rotationDirection)
    {
        int wallKickIndex = GetWallKickIndex(rotationIndex, rotationDirection);

        for (int i = 0; i < data.wallKicks.GetLength(1); i++)
        {
            Vector2Int translation = data.wallKicks[wallKickIndex, i];
            if (Move(translation, true)) return true;
        }

        return false;
    }

    private bool TestWallKicks180(int rotationIndex, int targetRotation)
    {
        Vector2Int[,] kicks = data.wallKicks180;

        for (int i = 0; i < kicks.GetLength(1); i++)
        {
            Vector2Int translation = kicks[rotationIndex, i];
            if (Move(translation, true))
            {
                this.rotationIndex = targetRotation;
                return true;
            }
        }

        return false;
    }

    private SpinKind DetectSpinKind()
    {
        if (!wasLastActionRotation || data.tetromino == Tetromino.O)
        {
            return SpinKind.None;
        }

        if (!IsImmobile())
        {
            return SpinKind.None;
        }

        if (data.tetromino == Tetromino.T)
        {
            return IsTSpinCornerLocked() ? SpinKind.TSpin : SpinKind.None;
        }

        return SpinKind.AllSpin;
    }

    private Spin ConvertToDetailedSpin(SpinKind spinKind)
    {
        if (spinKind == SpinKind.None)
        {
            return Spin.None;
        }

        if (spinKind == SpinKind.TSpin)
        {
            return Spin.TSpin;
        }

        switch (data.tetromino)
        {
            case Tetromino.I:
                return Spin.ISpin;
            case Tetromino.J:
                return Spin.JSpin;
            case Tetromino.L:
                return Spin.LSpin;
            case Tetromino.S:
                return Spin.SSpin;
            case Tetromino.Z:
                return Spin.ZSpin;
            case Tetromino.T:
                return Spin.TSpin;
            default:
                return Spin.None;
        }
    }

    private bool IsImmobile()
    {
        if (board == null)
        {
            return false;
        }

        Vector3Int left = position + Vector3Int.left;
        Vector3Int right = position + Vector3Int.right;
        Vector3Int down = position + Vector3Int.down;
        Vector3Int up = position + Vector3Int.up;

        bool canMoveLeft = board.IsVaildPosition(this, left);
        bool canMoveRight = board.IsVaildPosition(this, right);
        bool canMoveDown = board.IsVaildPosition(this, down);
        bool canMoveUp = board.IsVaildPosition(this, up);

        return !canMoveLeft && !canMoveRight && !canMoveDown && !canMoveUp;
    }

    private bool IsTSpinCornerLocked()
    {
        Vector3Int center = position;
        int occupiedCorners = 0;

        if (IsBlockedCell(center + new Vector3Int(-1, 1, 0))) occupiedCorners++;
        if (IsBlockedCell(center + new Vector3Int(1, 1, 0))) occupiedCorners++;
        if (IsBlockedCell(center + new Vector3Int(-1, -1, 0))) occupiedCorners++;
        if (IsBlockedCell(center + new Vector3Int(1, -1, 0))) occupiedCorners++;

        return occupiedCorners >= 3;
    }

    private bool IsBlockedCell(Vector3Int cellPosition)
    {
        if (board == null || board.tilemap == null)
        {
            return true;
        }

        if (!board.Bounds.Contains((Vector2Int)cellPosition))
        {
            return true;
        }

        return board.tilemap.HasTile(cellPosition);
    }

    private int GetWallKickIndex(int rotationIndex, int rotationDirection)
    {
        int wallKickIndex = rotationIndex * 2;

        if (rotationDirection < 0)
        {
            wallKickIndex--;
        }

        return Wrap(wallKickIndex, 0, data.wallKicks.GetLength(0));
    }

    // 래핑 함수 0 1 2 3
    private int Wrap(int input, int min, int max)
    {
        int range = max - min;
        return ((input - min) % range + range) % range + min;
    }
}
