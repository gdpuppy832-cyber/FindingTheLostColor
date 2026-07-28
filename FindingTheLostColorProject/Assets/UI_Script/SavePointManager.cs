using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class SavePointManager : MonoBehaviour
{
    public static SavePointManager Instance { get; private set; }

    [Header("Saved Data")]
    public string SavedSceneName = "";
    public Vector3 SavedPlayerPosition = Vector3.zero;
    public bool HasSaveData = false;

    // 사망 직전 보관할 궁극기 게이지 수치
    public float SavedSuperGauge = 0f;

    // 현재까지 정화가 완료된 몬스터의 고유 ID 해시셋 (씬이 바뀌거나 재로드되어도 보존)
    private HashSet<string> purifiedMonsterIDs = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 씬 로드 완료 이벤트 리스너 등록
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 플레이어가 깃발(체크포인트)에 닿았을 때 세이브 정보를 갱신합니다.
    /// </summary>
    public void SaveCheckpoint(Vector3 position)
    {
        SavedSceneName = SceneManager.GetActiveScene().name;
        SavedPlayerPosition = position;
        HasSaveData = true;

        // 세이브 당시 시점의 몬스터 정화 상태도 실시간 1차 업데이트
        UpdatePurifiedMonstersList();

        Debug.Log($"[SavePointManager] 체크포인트 저장 완료! 위치: {position}, 씬: {SavedSceneName}");
    }

    /// <summary>
    /// 플레이어 사망 직전, 궁극기 게이지와 최신 정화 완료된 몬스터 상태를 수집해 보관합니다.
    /// </summary>
    public void PrepareRespawn(float currentSuperGauge)
    {
        // 사망 시점에는 궁극기 게이지 수치만 백업 보관 (몬스터 정화 상태는 오직 깃발 세이브 시점에만 저장)
        SavedSuperGauge = currentSuperGauge;
        
        Debug.Log($"[SavePointManager] 부활 준비 완료! 보관된 궁극기 게이지: {SavedSuperGauge}");
    }

    /// <summary>
    /// 현재 씬 내에 존재하는 모든 NormalMonster 중 정화 완료된 것들의 고유 ID를 해시셋에 기록합니다.
    /// </summary>
    public void UpdatePurifiedMonstersList()
    {
        NormalMonster[] monsters = FindObjectsByType<NormalMonster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (monsters == null) return;

        foreach (var monster in monsters)
        {
            // Null 상태이거나 파괴 중인 몬스터는 검사 대상에서 제외
            if (monster != null && monster.gameObject != null)
            {
                try
                {
                    if (monster.IsPurified)
                    {
                        string monsterID = GetMonsterUniqueID(monster);
                        if (!string.IsNullOrEmpty(monsterID))
                        {
                            purifiedMonsterIDs.Add(monsterID);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[SavePointManager] 몬스터 정화 검사 중 예외 감지 및 복구: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 몬스터의 씬명 + 이름 + 최초 배치 좌표를 결합하여 100% 겹치지 않는 고유 식별자(ID)를 생성합니다.
    /// </summary>
    public string GetMonsterUniqueID(NormalMonster monster)
    {
        if (monster == null || monster.gameObject == null) return "";
        
        try
        {
            // 움직임이나 넉백으로 달라진 실시간 좌표 대신, 최초 스폰 위치(SpawnPosition)를 기준으로 100% 동일한 ID를 생성합니다.
            Vector3 pos = (monster.SpawnPosition != Vector3.zero) ? monster.SpawnPosition : monster.transform.position;
            return $"{monster.gameObject.scene.name}_{monster.gameObject.name}_{pos.x:F2}_{pos.y:F2}";
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SavePointManager] 몬스터 고유 ID 생성 실패 (파괴 과도기 개체): {ex.Message}");
            return "";
        }
    }

    /// <summary>
    /// 씬이 새로 읽어 들여질 때 호출되는 델리게이트 함수 (세이브포인트 부활 연동 핵심!)
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 세이브 데이터가 존재하고, 로드된 씬이 저장된 씬과 일치하는 경우 부활 물리 복구 가동!
        if (HasSaveData && scene.name == SavedSceneName)
        {
            // 1. 플레이어 오브젝트를 찾아 세이브 좌표로 텔레포트
            PlayerMove player = FindFirstObjectByType<PlayerMove>();
            if (player != null)
            {
                player.transform.position = SavedPlayerPosition;
                Debug.Log($"[SavePointManager] 플레이어 부활 텔레포트 완료 ➔ {SavedPlayerPosition}");
            }

            // 2. 저장된 궁극기 게이지 값 복구
            SuperGaugeController gauge = FindFirstObjectByType<SuperGaugeController>();
            if (gauge != null)
            {
                gauge.currentSuper = SavedSuperGauge;
                // UpdateUI() 강제 연동을 위해 임시로 AddSuperGauge 호출 또는 수동 갱신 시도
                gauge.AddSuperGauge(0f); 
                Debug.Log($"[SavePointManager] 플레이어 궁극기 게이지 수치 복구 완료 ➔ {SavedSuperGauge}");
            }

            // 3. 씬 내의 모든 몬스터 중 정화 대상 복구
            RestorePurifiedMonsters();
        }
    }

    /// <summary>
    /// 씬 안의 몬스터를 순회하여 이전에 정화 처리된 ID와 대조하여 시작하자마자 완치(정화) 상태로 강제 세팅합니다.
    /// </summary>
    private void RestorePurifiedMonsters()
    {
        NormalMonster[] monsters = FindObjectsByType<NormalMonster>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int restoredCount = 0;

        foreach (var monster in monsters)
        {
            if (monster == null) continue;

            string id = GetMonsterUniqueID(monster);
            if (purifiedMonsterIDs.Contains(id))
            {
                // 몬스터의 비주얼 및 데이터 무소음 정화 복구 처리
                monster.RestorePurificationState();
                restoredCount++;
            }
        }

        Debug.Log($"[SavePointManager] 총 {restoredCount}마리의 미정화 몬스터를 이전 정화 완료 상태로 복구했습니다!");
    }

    /// <summary>
    /// 새로운 게임 시작 시 세이브 포인트 데이터를 리셋합니다.
    /// </summary>
    public void ResetSaveData()
    {
        SavedSceneName = "";
        SavedPlayerPosition = Vector3.zero;
        HasSaveData = false;
        SavedSuperGauge = 0f;
        purifiedMonsterIDs.Clear();
        Debug.Log("[SavePointManager] 세이브 포인트 데이터가 전체 초기화되었습니다.");
    }
}
