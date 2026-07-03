using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    private static PuzzleManager instance;
    public static PuzzleManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<PuzzleManager>();
            }
            return instance;
        }
    }

    [Header("퍼즐 오브젝트 매칭 (자동 할당되지 않는 경우 수동 연결 가능)")]
    [Tooltip("6개의 퍼즐 등불 배열 (미설정 시 시작할 때 puzzleIndex에 맞춰 자동 수집/배치됩니다)")]
    public PuzzleLamp[] lamps = new PuzzleLamp[6];

    [Tooltip("6개의 퍼즐 발판 배열 (미설정 시 시작할 때 plateIndex에 맞춰 자동 수집/배치됩니다)")]
    public PuzzlePlate[] plates = new PuzzlePlate[6];

    [Tooltip("개방할 퍼즐 문")]
    public PuzzleDoor puzzleDoor;

    [Header("입력 진행 상황")]
    [Tooltip("현재까지 맞춘 입력 단계 (0~6)")]
    public int currentStep = 0;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 1. 등불(Lamps) 배열 자동 검색 및 순서 배치 (인스펙터가 비어있거나 수동 할당을 하지 않은 경우 대비)
        bool hasMissingLamps = false;
        for (int i = 0; i < 6; i++)
        {
            if (lamps == null || lamps.Length < 6 || lamps[i] == null)
            {
                hasMissingLamps = true;
                break;
            }
        }

        if (hasMissingLamps)
        {
            lamps = new PuzzleLamp[6];
            PuzzleLamp[] foundLamps = FindObjectsByType<PuzzleLamp>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var l in foundLamps)
            {
                if (l != null && l.puzzleIndex >= 0 && l.puzzleIndex < 6)
                {
                    lamps[l.puzzleIndex] = l;
                }
            }
            Debug.Log("[PuzzleManager] 씬 내의 PuzzleLamp를 찾아 인덱스별로 자동 배치 완료했습니다.");
        }

        // 2. 발판(Plates) 배열 자동 검색 및 순서 배치
        bool hasMissingPlates = false;
        for (int i = 0; i < 6; i++)
        {
            if (plates == null || plates.Length < 6 || plates[i] == null)
            {
                hasMissingPlates = true;
                break;
            }
        }

        if (hasMissingPlates)
        {
            plates = new PuzzlePlate[6];
            PuzzlePlate[] foundPlates = FindObjectsByType<PuzzlePlate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var p in foundPlates)
            {
                if (p != null && p.plateIndex >= 0 && p.plateIndex < 6)
                {
                    plates[p.plateIndex] = p;
                }
            }
            Debug.Log("[PuzzleManager] 씬 내의 PuzzlePlate를 찾아 인덱스별로 자동 배치 완료했습니다.");
        }

        // 3. 문(PuzzleDoor) 자동 매칭
        if (puzzleDoor == null)
        {
            puzzleDoor = FindFirstObjectByType<PuzzleDoor>();
            if (puzzleDoor != null)
            {
                Debug.Log($"[PuzzleManager] 씬 내의 PuzzleDoor({puzzleDoor.gameObject.name})를 자동 매칭했습니다.");
            }
        }
    }

    /// <summary>
    /// 플레이어가 특정 발판을 밟았을 때 호출됩니다.
    /// </summary>
    public void OnPlateStepped(PuzzlePlate steppedPlate)
    {
        // 6개 발판 완료하여 문이 이미 열렸다면 감지 무시
        if (currentStep >= 6) return;

        int index = steppedPlate.plateIndex;

        // 1. 올바른 순서의 발판을 밟은 경우 (예: 기다리던 단계와 발판 인덱스가 매치됨)
        if (index == currentStep)
        {
            // 발판 자체를 활성화 상태로 잠금 (중복 작동 방지)
            if (plates != null && index < plates.Length && plates[index] != null)
            {
                plates[index].SetActivated(true);
            }

            // 순서에 해당하는 등불을 영구 점등 유지 상태로 전환
            if (lamps != null && index < lamps.Length && lamps[index] != null)
            {
                lamps[index].LockLit();
            }

            currentStep++;
            Debug.Log($"[PuzzleManager] 올바른 순서의 발판을 밟았습니다! ({currentStep} / 6 단계 완료)");

            // 최종 6단계 전체 클리어 시 문 개방
            if (currentStep == 6)
            {
                if (puzzleDoor != null)
                {
                    puzzleDoor.Open();
                }
                Debug.Log("[PuzzleManager] 퍼즐 해결! 문이 개방됩니다.");
            }
        }
        // 2. 다른 엉뚱한 순서의 발판을 잘못 밟아 실패한 경우
        else
        {
            ResetPuzzle();
        }
    }

    /// <summary>
    /// 퍼즐 오입력 시 모든 점등된 등불을 끄고 발판을 다시 밟을 수 있게 복원하며 순서 기록을 초기화합니다.
    /// </summary>
    public void ResetPuzzle()
    {
        currentStep = 0;

        // 모든 등불 리셋
        if (lamps != null)
        {
            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] != null)
                {
                    lamps[i].ResetLamp();
                }
            }
        }

        // 모든 발판 리셋 (비활성화 상태로 원복하여 다시 밟을 수 있게 함)
        if (plates != null)
        {
            for (int i = 0; i < plates.Length; i++)
            {
                if (plates[i] != null)
                {
                    plates[i].SetActivated(false);
                }
            }
        }

        if (puzzleDoor != null)
        {
            puzzleDoor.ResetDoor();
        }

        Debug.LogWarning("[PuzzleManager] 순서가 틀렸습니다! 모든 등불과 발판이 원상복구되고 기록이 초기화됩니다.");
    }
}
