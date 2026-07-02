using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PurificationManager : MonoBehaviour
{
    public static PurificationManager Instance { get; private set; }

    [Header("UI References (사용하시는 텍스트 컴포넌트 하나만 연결하면 작동합니다)")]
    [Tooltip("기존 레거시 UI Text 컴포넌트")]
    public Text legacyText;

    [Tooltip("TextMeshPro - Text(UI) 컴포넌트 (UGUI Canvas 전용)")]
    public TextMeshProUGUI tmpText;

    [Tooltip("TextMeshPro - 3D Text 컴포넌트 (3D 월드 공간용)")]
    public TextMeshPro tmp3DText;

    [Tooltip("유니티 기본 3D TextMesh 컴포넌트 (3D 월드 공간용)")]
    public TextMesh legacyTextMesh;

    [Header("Debug List UI References (감지된 고양이 목록 출력용 - 선택사항)")]
    [Tooltip("감지된 고양이 리스트를 표시할 레거시 UI Text")]
    public Text debugLegacyText;

    [Tooltip("감지된 고양이 리스트를 표시할 TextMeshProUGUI (Canvas 전용)")]
    public TextMeshProUGUI debugTmpText;

    [Tooltip("감지된 고양이 리스트를 표시할 3D TextMeshPro (3D 월드 공간용)")]
    public TextMeshPro debugTmp3DText;

    [Tooltip("감지된 고양이 리스트를 표시할 3D TextMesh (3D 월드 공간용)")]
    public Text debugLegacyTextMesh;

    private int totalCats = 0;
    private int purifiedCats = 0;
    
    // 이미 정화 처리가 끝난 최상단 몬스터 루트 기록 (중복 호출 방지용)
    private System.Collections.Generic.HashSet<NormalMonster> purifiedRoots = new System.Collections.Generic.HashSet<NormalMonster>();

    /// <summary>
    /// 스테이지 내 모든 무채색 고양이가 정화되었는지 여부
    /// </summary>
    public bool IsAllPurified => purifiedCats >= totalCats && totalCats > 0;

    void Awake()
    {
        // 싱글톤 패턴 설정
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 1. 활성화된(Active) 고양이 탐색
        NormalMonster[] monsters = FindObjectsByType<NormalMonster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        
        // 2. 부모-자식 관계에 같은 스크립트가 여러 번 부착된 경우 중복을 피해 각 몬스터 루트별로 단 1개만 필터링
        System.Collections.Generic.HashSet<NormalMonster> uniqueRoots = new System.Collections.Generic.HashSet<NormalMonster>();
        foreach (var m in monsters)
        {
            uniqueRoots.Add(GetRootMonster(m));
        }

        totalCats = uniqueRoots.Count;
        purifiedCats = 0;
        purifiedRoots.Clear();

        // 3. 디버그용 로그: 어떤 오브젝트들이 고양이로 카운트되었는지 콘솔창 및 화면 텍스트에 출력
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"총 {totalCats}마리의 고양이 감지됨:");
        int idx = 1;
        foreach (var m in uniqueRoots)
        {
            sb.AppendLine($"- [{idx++}] {m.name} (위치: {m.transform.position})");
        }
        
        string debugMessage = sb.ToString();
        Debug.Log(debugMessage);

        // 화면 상의 텍스트메쉬에 감지 목록 연동
        if (debugLegacyText != null) debugLegacyText.text = debugMessage;
        if (debugTmpText != null) debugTmpText.text = debugMessage;
        if (debugTmp3DText != null) debugTmp3DText.text = debugMessage;
        if (debugLegacyTextMesh != null) debugLegacyTextMesh.text = debugMessage;

        UpdateUI();

        // 보석 제어 매니저가 씬에 존재하면 총 몬스터 마릿수를 기준으로 초기화
        if (CaveJewelManager.Instance != null)
        {
            CaveJewelManager.Instance.Initialize(totalCats);
        }
    }

    // 몬스터 계층구조 내에서 가장 최상단에 있는 NormalMonster 스크립트를 찾아 반환하는 함수
    private NormalMonster GetRootMonster(NormalMonster monster)
    {
        NormalMonster rootMonster = monster;
        Transform current = monster.transform.parent;
        while (current != null)
        {
            NormalMonster p = current.GetComponent<NormalMonster>();
            if (p != null)
            {
                rootMonster = p;
            }
            current = current.parent;
        }
        return rootMonster;
    }

    // 디버깅을 위한 경로 획득 헬퍼 함수
    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        Transform current = obj.transform;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }
        return path;
    }

    /// <summary>
    /// 무채색 고양이가 정화 완료되었을 때 즉시 분자 카운트를 올리고 UI를 갱신합니다.
    /// (동일 몬스터 계층 내의 자식/부모 중복 호출은 1회만 처리)
    /// </summary>
    public void OnCatPurified(NormalMonster monster)
    {
        NormalMonster rootMonster = GetRootMonster(monster);

        // 이미 해당 몬스터 트리가 정화 기록에 들어가 있다면 중복 처리이므로 무시
        if (purifiedRoots.Contains(rootMonster)) return;

        purifiedRoots.Add(rootMonster);
        purifiedCats++;

        if (purifiedCats > totalCats)
        {
            purifiedCats = totalCats; // 안전장치
        }

        UpdateUI();

        // 고양이 정화 성공 시 보석 점등 상태 실시간 업데이트
        if (CaveJewelManager.Instance != null)
        {
            CaveJewelManager.Instance.UpdateJewels(purifiedCats);
        }
    }

    /// <summary>
    /// 분수 형태(정화 완료 수 / 전체 정화 대상 수)로 UI 텍스트를 실시간 갱신합니다.
    /// </summary>
    private void UpdateUI()
    {
        string textValue = $"고양이 {purifiedCats} / {totalCats}";

        // 1. 레거시 UI Text
        if (legacyText != null)
        {
            legacyText.text = textValue;
        }

        // 2. TextMeshPro UGUI (UI)
        if (tmpText != null)
        {
            tmpText.text = textValue;
        }

        // 3. TextMeshPro (3D)
        if (tmp3DText != null)
        {
            tmp3DText.text = textValue;
        }

        // 4. 레거시 3D TextMesh
        if (legacyTextMesh != null)
        {
            legacyTextMesh.text = textValue;
        }
    }
}
