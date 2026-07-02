using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class CaveJewelManager : MonoBehaviour
{
    public static CaveJewelManager Instance { get; private set; }

    [Header("보석 SpriteRenderer (왼쪽부터 차례대로 6개 연결)")]
    public SpriteRenderer[] jewelRenderers = new SpriteRenderer[6];

    [Header("보석 스프라이트 개별 에셋 설정 (선택사항)")]
    [Tooltip("꺼져 있을 때의 기본 보석 스프라이트 (스프라이트 교체 방식을 쓸 경우)")]
    public Sprite unlitSprite;

    [Tooltip("켜졌을 때의 개별 보석 스프라이트 6개 (왼쪽부터 빨,주,노,초,파,보 차례대로)")]
    public Sprite[] litSprites = new Sprite[6];

    [Header("색상 커스텀 설정 (색상 변경 틴트 방식)")]
    [Tooltip("꺼져 있을 때의 보석 색상")]
    public Color unlitColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    [Tooltip("켜졌을 때의 6가지 보석 색상 (왼쪽부터 빨,주,노,초,파,보)")]
    public Color[] litColors = new Color[6]
    {
        new Color(1.0f, 0.15f, 0.15f), // 빨강
        new Color(1.0f, 0.55f, 0.0f),  // 주황
        new Color(1.0f, 0.92f, 0.0f),  // 노랑
        new Color(0.1f, 0.85f, 0.2f),  // 초록
        new Color(0.1f, 0.5f, 1.0f),   // 파랑
        new Color(0.65f, 0.1f, 1.0f)   // 보라
    };

    [Header("보석 정화 완료 연출 설정 (Cinematic)")]
    [Tooltip("모든 보석이 켜졌을 때 카메라가 비출 타겟 (비워둘 시 씬 내의 CaveEntrance를 자동 검색)")]
    public Transform cameraTarget;

    [Tooltip("연출 시작 시 플레이어 위치에서 머무르는 대기 시간 N (초)")]
    public float nSeconds = 1.0f;

    [Tooltip("목표지점(동굴)으로 부드럽게 패닝하여 이동하는 시간 M (초)")]
    public float mSeconds = 2.0f;

    [Tooltip("목표지점(동굴)에 머무르며 타이틀을 노출시키는 시간 O (초)")]
    public float oSeconds = 3.0f;

    [Tooltip("플레이어 위치로 부드럽게 다시 패닝하여 돌아오는 시간 P (초)")]
    public float pSeconds = 2.0f;

    [Tooltip("플레이어로 돌아온 뒤 조작 잠금을 해제하기 전까지 머무르는 대기 시간 Q (초)")]
    public float qSeconds = 1.0f;

    [Header("타이틀 UI 텍스트 연결 (가지고 계신 타이틀 텍스트메쉬 연결)")]
    [Tooltip("기존 레거시 UI Text 컴포넌트")]
    public Text titleText;

    [Tooltip("TextMeshPro - Text(UI) 컴포넌트 (UGUI Canvas 전용)")]
    public TextMeshProUGUI titleTmpText;

    [Tooltip("TextMeshPro - 3D Text 컴포넌트 (3D 월드 공간용)")]
    public TextMeshPro titleTmp3DText;

    [Tooltip("유니티 기본 3D TextMesh 컴포넌트 (3D 월드 공간용)")]
    public Text titleLegacyTextMesh;

    [Tooltip("타이틀이 표시되는 패널/오브젝트 (비워두면 텍스트 오브젝트 자체를 켜고 끔)")]
    public GameObject titlePanelObj;

    private int[] thresholds = new int[6];
    private int totalMonsters = 0;
    private bool isInitialized = false;
    private bool isCinematicPlayed = false; // 연출 중복 실행 방지 플래그

    void Awake()
    {
        // 싱글톤 초기화
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
        // 시작 시 타이틀 오브젝트 비활성화 처리
        SetTitleActive(false);
    }

    /// <summary>
    /// PurificationManager가 필터링을 거친 정확한 고양이 총합을 전달받아 보석별 목표치를 계산합니다.
    /// </summary>
    public void Initialize(int totalCats)
    {
        totalMonsters = totalCats;
        if (totalMonsters <= 0) return;

        // 1단계당 필요한 정화 수 계산 (소수점 올림 처리)
        // ex) 40 / 6 = 6.666 -> 올림하여 7
        int stepSize = Mathf.CeilToInt(totalMonsters / 6.0f);

        // 보석별 켜지는 누적 정화 컷 계산
        // 40마리 기준: [1단계: 7], [2단계: 14], [3단계: 21], [4단계: 28], [5단계: 35], [6단계: 40]
        for (int i = 0; i < 5; i++)
        {
            thresholds[i] = (i + 1) * stepSize;
        }
        // 마지막 6번째 단계는 정확한 최종 누적 몬스터 수로 고정하여 나머지 카운트 감소 구현
        thresholds[5] = totalMonsters;

        // 디버깅용 컷 출력
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[CaveJewelManager] 총 {totalMonsters}마리 기반 보석 컷 설정 완료:");
        string[] colors = { "빨강", "주황", "노랑", "초록", "파랑", "보라" };
        for (int i = 0; i < 6; i++)
        {
            sb.AppendLine($"- [{colors[i]}] 누적 {thresholds[i]}마리 이상 정화 시 켜짐");
        }
        Debug.Log(sb.ToString());

        isInitialized = true;

        // 초기 비주얼 갱신 (0마리 정화 상태)
        UpdateJewels(0);
    }

    /// <summary>
    /// 현재 누적 정화 카운트에 맞춰 6개 보석의 점등 상태를 실시간 갱신합니다.
    /// </summary>
    public void UpdateJewels(int currentPurifiedCount)
    {
        if (!isInitialized) return;

        int litCount = 0;

        for (int i = 0; i < 6; i++)
        {
            if (jewelRenderers[i] == null) continue;

            // 해당 보석의 누적 컷을 넘겼는지 체크
            bool shouldLightUp = currentPurifiedCount >= thresholds[i];

            if (shouldLightUp)
            {
                litCount++;
                // 보석 점등
                if (litSprites != null && i < litSprites.Length && litSprites[i] != null)
                {
                    jewelRenderers[i].sprite = litSprites[i];
                }
                if (i < litColors.Length)
                {
                    jewelRenderers[i].color = litColors[i];
                }
            }
            else
            {
                // 보석 소등
                if (unlitSprite != null)
                {
                    jewelRenderers[i].sprite = unlitSprite;
                }
                jewelRenderers[i].color = unlitColor;
            }
        }

        // 6개 보석이 모두 다 켜졌을 때 시네마틱 연출 트리거
        if (litCount >= 6 && !isCinematicPlayed)
        {
            isCinematicPlayed = true;
            StartCoroutine(PlayAllPurifiedCinematic());
        }
    }

    /// <summary>
    /// 모든 고양이를 정화했을 때의 카메라 연출 및 타이틀 노출 연출 코루틴
    /// </summary>
    private IEnumerator PlayAllPurifiedCinematic()
    {
        Debug.Log("[CaveJewelManager] 모든 고양이 정화 완료 연출 시작!");

        // 1. 플레이어 조작 잠금 및 공격/상호작용 잠금
        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null) playerMove.SetControl(false);

        CursorController cursor = FindFirstObjectByType<CursorController>();
        if (cursor != null) cursor.enabled = false;

        PlayerInteraction playerInt = FindFirstObjectByType<PlayerInteraction>();
        if (playerInt != null) playerInt.SetInteractionLock(true);

        // 2. CameraFollow 컴포넌트 임시 비활성화 (지형 경계 클램프 및 충돌로 인한 끊김/흔들림 방지)
        CameraFollow camFollow = FindFirstObjectByType<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.enabled = false;
        }

        // 3. 연출 카메라 시작점과 타겟 위치 설정
        Vector3 playerPos = playerMove != null ? playerMove.transform.position : Vector3.zero;
        float yOff = camFollow != null ? camFollow.yOffset : 2.0f;
        Vector3 startCamPos = new Vector3(playerPos.x, playerPos.y + yOff, Camera.main.transform.position.z);

        // 타겟 자동 스캔
        if (cameraTarget == null)
        {
            CaveEntrance entrance = FindFirstObjectByType<CaveEntrance>();
            if (entrance != null) cameraTarget = entrance.transform;
        }
        Vector3 targetPos = cameraTarget != null ? cameraTarget.position : Vector3.zero;
        Vector3 targetCamPos = new Vector3(targetPos.x, targetPos.y + yOff, Camera.main.transform.position.z);

        // [Phase 1] 연출 시작 시 N초간 플레이어 위치에 고정 대기
        Camera.main.transform.position = startCamPos;
        yield return new WaitForSeconds(nSeconds);

        // [Phase 2] M초간 목표지점(동굴)으로 부드럽게 이동 (Ease-In Ease-Out 적용)
        float elapsed = 0f;
        while (elapsed < mSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / mSeconds);
            t = Mathf.SmoothStep(0f, 1f, t); // 가감속 보간
            Camera.main.transform.position = Vector3.Lerp(startCamPos, targetCamPos, t);
            yield return null;
        }
        Camera.main.transform.position = targetCamPos;

        // [Phase 3] O초간 목표지점을 보고있기 (그사이에 타이틀 텍스트를 출력 및 해제)
        SetTitleText("모든 고양이를\n정화했다!");
        SetTitleActive(true);
        yield return new WaitForSeconds(oSeconds);
        SetTitleActive(false);

        // [Phase 4] P초간 플레이어 위치로 부드럽게 돌아오기 (Ease-In Ease-Out 적용)
        elapsed = 0f;
        while (elapsed < pSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pSeconds);
            t = Mathf.SmoothStep(0f, 1f, t); // 가감속 보간
            Camera.main.transform.position = Vector3.Lerp(targetCamPos, startCamPos, t);
            yield return null;
        }
        Camera.main.transform.position = startCamPos;

        // [Phase 5] 돌아온 뒤 Q초간 플레이어 위치에서 머물렀다가 해제
        yield return new WaitForSeconds(qSeconds);

        // 4. CameraFollow 컴포넌트 복원 및 플레이어 조작 잠금 해제
        if (camFollow != null)
        {
            camFollow.enabled = true;
        }

        if (playerMove != null) playerMove.SetControl(true);
        if (cursor != null) cursor.enabled = true;
        if (playerInt != null) playerInt.SetInteractionLock(false);

        Debug.Log("[CaveJewelManager] 모든 고양이 정화 완료 연출 종료 및 조작 복구.");
    }

    /// <summary>
    /// 타이틀 텍스트를 입력하는 함수
    /// </summary>
    private void SetTitleText(string text)
    {
        if (titleText != null) titleText.text = text;
        if (titleTmpText != null) titleTmpText.text = text;
        if (titleTmp3DText != null) titleTmp3DText.text = text;
        if (titleLegacyTextMesh != null) titleLegacyTextMesh.text = text;
    }

    /// <summary>
    /// 타이틀 UI의 전체 활성 상태를 켜고 끕니다.
    /// </summary>
    private void SetTitleActive(bool active)
    {
        if (titlePanelObj != null)
        {
            titlePanelObj.SetActive(active);
        }
        else
        {
            // 판넬이 비어 있다면 각각의 텍스트 오브젝트 자체를 켜고 끔
            if (titleText != null) titleText.gameObject.SetActive(active);
            if (titleTmpText != null) titleTmpText.gameObject.SetActive(active);
            if (titleTmp3DText != null) titleTmp3DText.gameObject.SetActive(active);
            if (titleLegacyTextMesh != null) titleLegacyTextMesh.gameObject.SetActive(active);
        }
    }
}
