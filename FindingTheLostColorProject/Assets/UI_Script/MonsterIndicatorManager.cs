using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MonsterIndicatorManager : MonoBehaviour
{
    public static MonsterIndicatorManager Instance { get; private set; }

    [Header("UI Image Settings (이미지 슬롯 노출)")]
    [Tooltip("왼쪽 인디케이터용 고양이 화살표 이미지 (비워두면 텍스트 ◀🐱 대체)")]
    public Sprite leftCatSprite;

    [Tooltip("오른쪽 인디케이터용 고양이 화살표 이미지 (비워두면 텍스트 🐱▶ 대체)")]
    public Sprite rightCatSprite;

    [Header("UI Layout & Font Settings")]
    [Tooltip("UI가 배치될 Canvas (비워두면 씬의 메인 Canvas 자동 탐색)")]
    public Canvas targetCanvas;

    [Tooltip("수량 텍스트용 TextMeshPro 폰트 에셋 (비워두면 기본)")]
    public TMP_FontAsset fontAsset;

    [Tooltip("화면 가두기 테두리 패딩 (픽셀 단위, 기본값: 50f)")]
    public float edgePadding = 50f;

    [Tooltip("화면 Y축 높이 배치 비율 (0.5f면 화면 중앙 높이, 기본값: 0.5f)")]
    public float yCenterRatio = 0.5f;

    [Header("Toggle Settings")]
    [Tooltip("Tab키 토글 상태 (기본값: true / 표시 중)")]
    public bool isIndicatorEnabled = true;

    // 내부 UI 요소 참조
    private GameObject leftContainer;
    private GameObject rightContainer;

    private Image leftCatImageComponent;
    private Image rightCatImageComponent;

    private TextMeshProUGUI leftCatTextComponent;
    private TextMeshProUGUI rightCatTextComponent;

    private TextMeshProUGUI leftCountText;
    private TextMeshProUGUI rightCountText;

    private Transform leftArrowTransform;
    private Transform rightArrowTransform;

    private Camera mainCamera;
    private Transform playerTransform;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;

        // 씬에서 Canvas 자동 할당
        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }

        // 플레이어 자동 할당
        FindPlayer();

        // UI 개체 자동 생성
        CreateUIElements();
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    /// <summary>
    /// 동적으로 좌/우 인디케이터 UI 요소를 Canvas 하위에 자동 셋업 (컴포넌트 독립 분리 적용)
    /// </summary>
    private void CreateUIElements()
    {
        if (targetCanvas == null) return;

        // 1. 왼쪽 인디케이터 루트 생성
        leftContainer = new GameObject("MonsterIndicator_Left");
        leftContainer.transform.SetParent(targetCanvas.transform, false);
        RectTransform leftRt = leftContainer.AddComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0f, yCenterRatio);
        leftRt.anchorMax = new Vector2(0f, yCenterRatio);
        leftRt.pivot = new Vector2(0f, 0.5f);
        leftRt.anchoredPosition = new Vector2(edgePadding, 0f);
        leftRt.sizeDelta = new Vector2(100f, 100f);

        // 1-1. 왼쪽 화살표 회전 개체
        GameObject leftArrowObj = new GameObject("ArrowHolder");
        leftArrowObj.transform.SetParent(leftContainer.transform, false);
        RectTransform leftArrowRt = leftArrowObj.AddComponent<RectTransform>();
        leftArrowRt.sizeDelta = new Vector2(60f, 60f);
        leftArrowTransform = leftArrowObj.transform;

        // [핵심 수정] Image와 TextMeshProUGUI를 별도 자식 오브젝트로 분리하여 Graphic 컴포넌트 충돌을 완벽 차단!
        GameObject leftCatImgObj = new GameObject("CatImage");
        leftCatImgObj.transform.SetParent(leftArrowObj.transform, false);
        RectTransform leftImgRt = leftCatImgObj.AddComponent<RectTransform>();
        leftImgRt.sizeDelta = new Vector2(60f, 60f);
        leftCatImageComponent = leftCatImgObj.AddComponent<Image>();

        GameObject leftCatTxtObj = new GameObject("CatText");
        leftCatTxtObj.transform.SetParent(leftArrowObj.transform, false);
        RectTransform leftTxtRt = leftCatTxtObj.AddComponent<RectTransform>();
        leftTxtRt.sizeDelta = new Vector2(80f, 40f);
        leftCatTextComponent = leftCatTxtObj.AddComponent<TextMeshProUGUI>();
        leftCatTextComponent.text = "◀🐱";
        leftCatTextComponent.fontSize = 32;
        leftCatTextComponent.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) leftCatTextComponent.font = fontAsset;

        // 1-2. 왼쪽 수량 텍스트 (회전과 독립적)
        GameObject leftCountObj = new GameObject("CountText");
        leftCountObj.transform.SetParent(leftContainer.transform, false);
        RectTransform leftCountRt = leftCountObj.AddComponent<RectTransform>();
        leftCountRt.anchoredPosition = new Vector2(0f, 40f);
        leftCountRt.sizeDelta = new Vector2(100f, 35f);
        leftCountText = leftCountObj.AddComponent<TextMeshProUGUI>();
        leftCountText.text = "×1";
        leftCountText.fontSize = 26;
        leftCountText.fontStyle = FontStyles.Bold;
        leftCountText.color = Color.yellow;
        leftCountText.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) leftCountText.font = fontAsset;


        // 2. 오른쪽 인디케이터 루트 생성
        rightContainer = new GameObject("MonsterIndicator_Right");
        rightContainer.transform.SetParent(targetCanvas.transform, false);
        RectTransform rightRt = rightContainer.AddComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(1f, yCenterRatio);
        rightRt.anchorMax = new Vector2(1f, yCenterRatio);
        rightRt.pivot = new Vector2(1f, 0.5f);
        rightRt.anchoredPosition = new Vector2(-edgePadding, 0f);
        rightRt.sizeDelta = new Vector2(100f, 100f);

        // 2-1. 오른쪽 화살표 회전 개체
        GameObject rightArrowObj = new GameObject("ArrowHolder");
        rightArrowObj.transform.SetParent(rightContainer.transform, false);
        RectTransform rightArrowRt = rightArrowObj.AddComponent<RectTransform>();
        rightArrowRt.sizeDelta = new Vector2(60f, 60f);
        rightArrowTransform = rightArrowObj.transform;

        // [핵심 수정] Image와 TextMeshProUGUI 분리
        GameObject rightCatImgObj = new GameObject("CatImage");
        rightCatImgObj.transform.SetParent(rightArrowObj.transform, false);
        RectTransform rightImgRt = rightCatImgObj.AddComponent<RectTransform>();
        rightImgRt.sizeDelta = new Vector2(60f, 60f);
        rightCatImageComponent = rightCatImgObj.AddComponent<Image>();

        GameObject rightCatTxtObj = new GameObject("CatText");
        rightCatTxtObj.transform.SetParent(rightArrowObj.transform, false);
        RectTransform rightTxtRt = rightCatTxtObj.AddComponent<RectTransform>();
        rightTxtRt.sizeDelta = new Vector2(80f, 40f);
        rightCatTextComponent = rightCatTxtObj.AddComponent<TextMeshProUGUI>();
        rightCatTextComponent.text = "🐱▶";
        rightCatTextComponent.fontSize = 32;
        rightCatTextComponent.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) rightCatTextComponent.font = fontAsset;

        // 2-2. 오른쪽 수량 텍스트
        GameObject rightCountObj = new GameObject("CountText");
        rightCountObj.transform.SetParent(rightContainer.transform, false);
        RectTransform rightCountRt = rightCountObj.AddComponent<RectTransform>();
        rightCountRt.anchoredPosition = new Vector2(0f, 40f);
        rightCountRt.sizeDelta = new Vector2(100f, 35f);
        rightCountText = rightCountObj.AddComponent<TextMeshProUGUI>();
        rightCountText.text = "×1";
        rightCountText.fontSize = 26;
        rightCountText.fontStyle = FontStyles.Bold;
        rightCountText.color = Color.yellow;
        rightCountText.alignment = TextAlignmentOptions.Center;
        if (fontAsset != null) rightCountText.font = fontAsset;

        RefreshSpriteOrTextDisplay();
    }

    /// <summary>
    /// Sprite 슬롯 할당 여부에 따라 이미지/텍스트 표시 모드를 동적으로 갱신
    /// </summary>
    public void RefreshSpriteOrTextDisplay()
    {
        if (leftCatImageComponent == null || rightCatImageComponent == null) return;

        if (leftCatSprite != null)
        {
            leftCatImageComponent.gameObject.SetActive(true);
            leftCatImageComponent.sprite = leftCatSprite;
            if (leftCatTextComponent != null) leftCatTextComponent.gameObject.SetActive(false);
        }
        else
        {
            leftCatImageComponent.gameObject.SetActive(false);
            if (leftCatTextComponent != null) leftCatTextComponent.gameObject.SetActive(true);
        }

        if (rightCatSprite != null)
        {
            rightCatImageComponent.gameObject.SetActive(true);
            rightCatImageComponent.sprite = rightCatSprite;
            if (rightCatTextComponent != null) rightCatTextComponent.gameObject.SetActive(false);
        }
        else
        {
            rightCatImageComponent.gameObject.SetActive(false);
            if (rightCatTextComponent != null) rightCatTextComponent.gameObject.SetActive(true);
        }
    }

    private void Update()
    {
        // 1. Tab키 1회 클릭 시 토글 스위칭 (동일 프레임 연속 처리 방지)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isIndicatorEnabled = !isIndicatorEnabled;
            Debug.Log($"[MonsterIndicatorManager] Tab키 입력: 인디케이터 표시 상태 = {isIndicatorEnabled}");
        }

        // 2. 플레이어 재탐색 (null 대응)
        if (playerTransform == null)
        {
            FindPlayer();
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        UpdateIndicators();
    }

    private void UpdateIndicators()
    {
        // 예외 조건: 일시정지 중이거나, 토글이 꺼졌거나, 플레이어/카메라가 없으면 숨김
        bool isPaused = PauseManager.IsPaused;
        if (!isIndicatorEnabled || isPaused || playerTransform == null || mainCamera == null)
        {
            if (leftContainer != null) leftContainer.SetActive(false);
            if (rightContainer != null) rightContainer.SetActive(false);
            return;
        }

        RefreshSpriteOrTextDisplay();

        // 씬 내의 모든 몬스터 검색 (미정화 몬스터만 대상)
        NormalMonster[] allMonsters = FindObjectsByType<NormalMonster>(FindObjectsSortMode.None);

        int leftCount = 0;
        int rightCount = 0;

        Vector3 leftSumPos = Vector3.zero;
        Vector3 rightSumPos = Vector3.zero;

        foreach (var monster in allMonsters)
        {
            // 이미 정화되었거나 비활성화된 몬스터 제외
            if (monster == null || monster.IsPurified || !monster.gameObject.activeInHierarchy) continue;

            Vector3 monsterPos = monster.transform.position;
            Vector3 viewportPos = mainCamera.WorldToViewportPoint(monsterPos);

            // 카메라 뷰포트 범위 안(0.0 ~ 1.0)에 완전히 들어온 몬스터는 제외! (화면 밖 몬스터만 카운트)
            bool isInsideScreen = (viewportPos.x >= 0f && viewportPos.x <= 1f && viewportPos.y >= 0f && viewportPos.y <= 1f && viewportPos.z > 0f);
            if (isInsideScreen) continue;

            // 플레이어 X좌표 기준으로 왼쪽 / 오른쪽 분류
            if (monsterPos.x < playerTransform.position.x)
            {
                leftCount++;
                leftSumPos += monsterPos;
            }
            else
            {
                rightCount++;
                rightSumPos += monsterPos;
            }
        }

        // --- 왼쪽 인디케이터 처리 ---
        if (leftCount > 0)
        {
            if (leftContainer != null) leftContainer.SetActive(true);
            if (leftCountText != null) leftCountText.text = $"×{leftCount}";

            // 몬스터 그룹 평균 위치를 향해 화살표 실시간 회전 (Look-at Angle)
            Vector3 avgPos = leftSumPos / leftCount;
            Vector2 dir = (avgPos - playerTransform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            if (leftArrowTransform != null)
            {
                leftArrowTransform.rotation = Quaternion.Euler(0f, 0f, angle + 180f);
            }
        }
        else
        {
            if (leftContainer != null) leftContainer.SetActive(false);
        }

        // --- 오른쪽 인디케이터 처리 ---
        if (rightCount > 0)
        {
            if (rightContainer != null) rightContainer.SetActive(true);
            if (rightCountText != null) rightCountText.text = $"×{rightCount}";

            // 몬스터 그룹 평균 위치를 향해 화살표 실시간 회전
            Vector3 avgPos = rightSumPos / rightCount;
            Vector2 dir = (avgPos - playerTransform.position).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            if (rightArrowTransform != null)
            {
                rightArrowTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
        else
        {
            if (rightContainer != null) rightContainer.SetActive(false);
        }
    }
}
