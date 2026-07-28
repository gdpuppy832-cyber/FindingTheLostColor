using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MonsterIndicatorManager : MonoBehaviour
{
    public static MonsterIndicatorManager Instance { get; private set; }

    [Header("UI Image Settings (이미지 슬롯 노출)")]
    [Tooltip("가장 가까운 고양이 방향을 가리키는 화살표 이미지 스프라이트")]
    public Sprite arrowSprite;

    [Tooltip("남은 고양이 마리수가 표시될 고양이 얼굴 이미지 스프라이트")]
    public Sprite catFaceSprite;

    [Header("Arrow Pivot Settings (화살표 회전축 피벗)")]
    [Tooltip("화살표 이미지의 회전축 피벗 좌표 (기본값: (0, 0.5) - 왼쪽 중앙 피벗)")]
    public Vector2 arrowPivot = new Vector2(0f, 0.5f);

    [Header("Left Indicator Settings (왼쪽 인디케이터 개별 튜닝)")]
    [Tooltip("왼쪽 화살표 이미지 크기")]
    public Vector2 leftArrowImageSize = new Vector2(60f, 60f);

    [Tooltip("왼쪽 고양이 얼굴 이미지 크기")]
    public Vector2 leftCatFaceImageSize = new Vector2(80f, 80f);

    [Tooltip("왼쪽 화살표와 고양이 얼굴 사이의 간격 패딩")]
    public float leftGapBetweenImages = 40f;

    [Tooltip("왼쪽 고양이 얼굴 안쪽 수량 텍스트의 X, Y 위치 오프셋")]
    public Vector2 leftTextOffset = Vector2.zero;

    [Tooltip("왼쪽 수량 텍스트 폰트 크기")]
    public float leftFontSize = 24f;

    [Header("Right Indicator Settings (오른쪽 인디케이터 개별 튜닝)")]
    [Tooltip("오른쪽 화살표 이미지 크기")]
    public Vector2 rightArrowImageSize = new Vector2(60f, 60f);

    [Tooltip("오른쪽 고양이 얼굴 이미지 크기")]
    public Vector2 rightCatFaceImageSize = new Vector2(80f, 80f);

    [Tooltip("오른쪽 화살표와 고양이 얼굴 사이의 간격 패딩")]
    public float rightGapBetweenImages = 40f;

    [Tooltip("오른쪽 고양이 얼굴 안쪽 수량 텍스트의 X, Y 위치 오프셋")]
    public Vector2 rightTextOffset = Vector2.zero;

    [Tooltip("오른쪽 수량 텍스트 폰트 크기")]
    public float rightFontSize = 24f;

    [Header("UI Canvas & Screen Settings")]
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

    private RectTransform leftArrowRt;
    private RectTransform leftArrowImgRt;
    private RectTransform leftCatFaceRt;
    private RectTransform leftCatFaceImgRt;
    private RectTransform leftCountRt;

    private RectTransform rightArrowRt;
    private RectTransform rightArrowImgRt;
    private RectTransform rightCatFaceRt;
    private RectTransform rightCatFaceImgRt;
    private RectTransform rightCountRt;

    private Image leftArrowImageComponent;
    private Image rightArrowImageComponent;

    private Image leftCatFaceImageComponent;
    private Image rightCatFaceImageComponent;

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

        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }

        FindPlayer();
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
    /// 동적으로 좌/우 인디케이터 UI 요소를 Canvas 하위에 자동 셋업 (왼쪽 중앙 (0, 0.5) 피벗 기준적용)
    /// </summary>
    private void CreateUIElements()
    {
        if (targetCanvas == null) return;

        // ----------------------------------------------------
        // 1. 왼쪽 인디케이터 루트 생성
        // ----------------------------------------------------
        leftContainer = new GameObject("MonsterIndicator_Left");
        leftContainer.transform.SetParent(targetCanvas.transform, false);
        RectTransform leftRt = leftContainer.AddComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0f, yCenterRatio);
        leftRt.anchorMax = new Vector2(0f, yCenterRatio);
        leftRt.pivot = new Vector2(0f, 0.5f);
        leftRt.anchoredPosition = new Vector2(edgePadding, 0f);
        leftRt.sizeDelta = new Vector2(250f, 150f);

        // 1-1. [화살표 회전체] (Left Center 피벗 (0, 0.5) 적용!)
        GameObject leftArrowObj = new GameObject("ArrowHolder");
        leftArrowObj.transform.SetParent(leftContainer.transform, false);
        leftArrowRt = leftArrowObj.AddComponent<RectTransform>();
        leftArrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        leftArrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        leftArrowRt.pivot = arrowPivot; // 왼쪽 중앙 (0, 0.5) 피벗
        leftArrowTransform = leftArrowObj.transform;

        GameObject leftArrowImgObj = new GameObject("ArrowImage");
        leftArrowImgObj.transform.SetParent(leftArrowObj.transform, false);
        leftArrowImgRt = leftArrowImgObj.AddComponent<RectTransform>();
        leftArrowImgRt.anchorMin = new Vector2(0.5f, 0.5f);
        leftArrowImgRt.anchorMax = new Vector2(0.5f, 0.5f);
        leftArrowImgRt.pivot = arrowPivot; // 왼쪽 중앙 (0, 0.5) 피벗
        leftArrowImgRt.anchoredPosition = Vector2.zero;
        leftArrowImageComponent = leftArrowImgObj.AddComponent<Image>();
        leftArrowImageComponent.type = Image.Type.Simple;
        leftArrowImageComponent.preserveAspect = true;
        leftArrowImageComponent.raycastTarget = false;

        // 1-2. [고양이 얼굴 고정체]
        GameObject leftCatFaceObj = new GameObject("CatFaceHolder");
        leftCatFaceObj.transform.SetParent(leftContainer.transform, false);
        leftCatFaceRt = leftCatFaceObj.AddComponent<RectTransform>();
        leftCatFaceRt.anchorMin = new Vector2(0.5f, 0.5f);
        leftCatFaceRt.anchorMax = new Vector2(0.5f, 0.5f);
        leftCatFaceRt.pivot = new Vector2(0.5f, 0.5f);

        GameObject leftCatFaceImgObj = new GameObject("CatFaceImage");
        leftCatFaceImgObj.transform.SetParent(leftCatFaceObj.transform, false);
        leftCatFaceImgRt = leftCatFaceImgObj.AddComponent<RectTransform>();
        leftCatFaceImgRt.anchorMin = new Vector2(0.5f, 0.5f);
        leftCatFaceImgRt.anchorMax = new Vector2(0.5f, 0.5f);
        leftCatFaceImgRt.pivot = new Vector2(0.5f, 0.5f);
        leftCatFaceImgRt.anchoredPosition = Vector2.zero;
        leftCatFaceImageComponent = leftCatFaceImgObj.AddComponent<Image>();
        leftCatFaceImageComponent.type = Image.Type.Simple;
        leftCatFaceImageComponent.preserveAspect = true;
        leftCatFaceImageComponent.raycastTarget = false;

        // 1-3. 수량 텍스트
        GameObject leftCountObj = new GameObject("CountText");
        leftCountObj.transform.SetParent(leftCatFaceObj.transform, false);
        leftCountRt = leftCountObj.AddComponent<RectTransform>();
        leftCountRt.anchorMin = new Vector2(0.5f, 0.5f);
        leftCountRt.anchorMax = new Vector2(0.5f, 0.5f);
        leftCountRt.pivot = new Vector2(0.5f, 0.5f);
        leftCountText = leftCountObj.AddComponent<TextMeshProUGUI>();
        leftCountText.text = "×1";
        leftCountText.fontStyle = FontStyles.Bold;
        leftCountText.color = Color.yellow;
        leftCountText.alignment = TextAlignmentOptions.Center;
        leftCountText.outlineWidth = 0.35f;
        leftCountText.outlineColor = Color.black;
        leftCountText.raycastTarget = false;
        if (fontAsset != null) leftCountText.font = fontAsset;


        // ----------------------------------------------------
        // 2. 오른쪽 인디케이터 루트 생성
        // ----------------------------------------------------
        rightContainer = new GameObject("MonsterIndicator_Right");
        rightContainer.transform.SetParent(targetCanvas.transform, false);
        RectTransform rightRt = rightContainer.AddComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(1f, yCenterRatio);
        rightRt.anchorMax = new Vector2(1f, yCenterRatio);
        rightRt.pivot = new Vector2(1f, 0.5f);
        rightRt.anchoredPosition = new Vector2(-edgePadding, 0f);
        rightRt.sizeDelta = new Vector2(250f, 150f);

        // 2-1. [화살표 회전체] (Left Center 피벗 (0, 0.5) 적용!)
        GameObject rightArrowObj = new GameObject("ArrowHolder");
        rightArrowObj.transform.SetParent(rightContainer.transform, false);
        rightArrowRt = rightArrowObj.AddComponent<RectTransform>();
        rightArrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        rightArrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        rightArrowRt.pivot = arrowPivot; // 왼쪽 중앙 (0, 0.5) 피벗
        rightArrowTransform = rightArrowObj.transform;

        GameObject rightArrowImgObj = new GameObject("ArrowImage");
        rightArrowImgObj.transform.SetParent(rightArrowObj.transform, false);
        rightArrowImgRt = rightArrowImgObj.AddComponent<RectTransform>();
        rightArrowImgRt.anchorMin = new Vector2(0.5f, 0.5f);
        rightArrowImgRt.anchorMax = new Vector2(0.5f, 0.5f);
        rightArrowImgRt.pivot = arrowPivot; // 왼쪽 중앙 (0, 0.5) 피벗
        rightArrowImgRt.anchoredPosition = Vector2.zero;
        rightArrowImageComponent = rightArrowImgObj.AddComponent<Image>();
        rightArrowImageComponent.type = Image.Type.Simple;
        rightArrowImageComponent.preserveAspect = true;
        rightArrowImageComponent.raycastTarget = false;

        // 2-2. [고양이 얼굴 고정체]
        GameObject rightCatFaceObj = new GameObject("CatFaceHolder");
        rightCatFaceObj.transform.SetParent(rightContainer.transform, false);
        rightCatFaceRt = rightCatFaceObj.AddComponent<RectTransform>();
        rightCatFaceRt.anchorMin = new Vector2(0.5f, 0.5f);
        rightCatFaceRt.anchorMax = new Vector2(0.5f, 0.5f);
        rightCatFaceRt.pivot = new Vector2(0.5f, 0.5f);

        GameObject rightCatFaceImgObj = new GameObject("CatFaceImage");
        rightCatFaceImgObj.transform.SetParent(rightCatFaceObj.transform, false);
        rightCatFaceImgRt = rightCatFaceImgObj.AddComponent<RectTransform>();
        rightCatFaceImgRt.anchorMin = new Vector2(0.5f, 0.5f);
        rightCatFaceImgRt.anchorMax = new Vector2(0.5f, 0.5f);
        rightCatFaceImgRt.pivot = new Vector2(0.5f, 0.5f);
        rightCatFaceImgRt.anchoredPosition = Vector2.zero;
        rightCatFaceImageComponent = rightCatFaceImgObj.AddComponent<Image>();
        rightCatFaceImageComponent.type = Image.Type.Simple;
        rightCatFaceImageComponent.preserveAspect = true;
        rightCatFaceImageComponent.raycastTarget = false;

        // 2-3. 수량 텍스트
        GameObject rightCountObj = new GameObject("CountText");
        rightCountObj.transform.SetParent(rightCatFaceObj.transform, false);
        rightCountRt = rightCountObj.AddComponent<RectTransform>();
        rightCountRt.anchorMin = new Vector2(0.5f, 0.5f);
        rightCountRt.anchorMax = new Vector2(0.5f, 0.5f);
        rightCountRt.pivot = new Vector2(0.5f, 0.5f);
        rightCountText = rightCountObj.AddComponent<TextMeshProUGUI>();
        rightCountText.text = "×1";
        rightCountText.fontStyle = FontStyles.Bold;
        rightCountText.color = Color.yellow;
        rightCountText.alignment = TextAlignmentOptions.Center;
        rightCountText.outlineWidth = 0.35f;
        rightCountText.outlineColor = Color.black;
        rightCountText.raycastTarget = false;
        if (fontAsset != null) rightCountText.font = fontAsset;

        // 레이아웃 적용
        ApplyLayoutSettings();
        RefreshSpriteOrTextDisplay();
    }

    /// <summary>
    /// 피벗 수치 및 인스펙터의 개별 레이아웃을 실시간 적용
    /// </summary>
    public void ApplyLayoutSettings()
    {
        // 0. 피벗 적용
        if (leftArrowRt != null) leftArrowRt.pivot = arrowPivot;
        if (leftArrowImgRt != null) leftArrowImgRt.pivot = arrowPivot;
        if (rightArrowRt != null) rightArrowRt.pivot = arrowPivot;
        if (rightArrowImgRt != null) rightArrowImgRt.pivot = arrowPivot;

        // 1. 왼쪽 인디케이터 레이아웃 적용
        if (leftArrowRt != null) leftArrowRt.sizeDelta = leftArrowImageSize;
        if (leftArrowImgRt != null) leftArrowImgRt.sizeDelta = leftArrowImageSize;

        if (leftCatFaceRt != null) leftCatFaceRt.sizeDelta = leftCatFaceImageSize;
        if (leftCatFaceImgRt != null) leftCatFaceImgRt.sizeDelta = leftCatFaceImageSize;

        float leftHalfGap = leftGapBetweenImages * 0.5f;
        if (leftArrowRt != null) leftArrowRt.anchoredPosition = new Vector2(-leftHalfGap, 0f);
        if (leftCatFaceRt != null) leftCatFaceRt.anchoredPosition = new Vector2(leftHalfGap, 0f);

        if (leftCountRt != null)
        {
            leftCountRt.anchoredPosition = leftTextOffset;
            leftCountRt.sizeDelta = leftCatFaceImageSize;
        }
        if (leftCountText != null) leftCountText.fontSize = leftFontSize;


        // 2. 오른쪽 인디케이터 레이아웃 적용
        if (rightArrowRt != null) rightArrowRt.sizeDelta = rightArrowImageSize;
        if (rightArrowImgRt != null) rightArrowImgRt.sizeDelta = rightArrowImageSize;

        if (rightCatFaceRt != null) rightCatFaceRt.sizeDelta = rightCatFaceImageSize;
        if (rightCatFaceImgRt != null) rightCatFaceImgRt.sizeDelta = rightCatFaceImageSize;

        float rightHalfGap = rightGapBetweenImages * 0.5f;
        if (rightArrowRt != null) rightArrowRt.anchoredPosition = new Vector2(-rightHalfGap, 0f);
        if (rightCatFaceRt != null) rightCatFaceRt.anchoredPosition = new Vector2(rightHalfGap, 0f);

        if (rightCountRt != null)
        {
            rightCountRt.anchoredPosition = rightTextOffset;
            rightCountRt.sizeDelta = rightCatFaceImageSize;
        }
        if (rightCountText != null) rightCountText.fontSize = rightFontSize;
    }

    private void OnValidate()
    {
        ApplyLayoutSettings();
        RefreshSpriteOrTextDisplay();
    }

    public void RefreshSpriteOrTextDisplay()
    {
        if (leftArrowImageComponent != null)
        {
            if (arrowSprite != null)
            {
                leftArrowImageComponent.sprite = arrowSprite;
                leftArrowImageComponent.gameObject.SetActive(true);
            }
            else
            {
                leftArrowImageComponent.gameObject.SetActive(false);
            }
        }

        if (rightArrowImageComponent != null)
        {
            if (arrowSprite != null)
            {
                rightArrowImageComponent.sprite = arrowSprite;
                rightArrowImageComponent.gameObject.SetActive(true);
            }
            else
            {
                rightArrowImageComponent.gameObject.SetActive(false);
            }
        }

        if (leftCatFaceImageComponent != null)
        {
            if (catFaceSprite != null)
            {
                leftCatFaceImageComponent.sprite = catFaceSprite;
                leftCatFaceImageComponent.gameObject.SetActive(true);
            }
            else
            {
                leftCatFaceImageComponent.gameObject.SetActive(false);
            }
        }

        if (rightCatFaceImageComponent != null)
        {
            if (catFaceSprite != null)
            {
                rightCatFaceImageComponent.sprite = catFaceSprite;
                rightCatFaceImageComponent.gameObject.SetActive(true);
            }
            else
            {
                rightCatFaceImageComponent.gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        // Tab 키 입력 시 인디케이터 토글
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            isIndicatorEnabled = !isIndicatorEnabled;
            Debug.Log($"[MonsterIndicator] 표시 상태 변경: {(isIndicatorEnabled ? "켜짐" : "꺼짐")}");
        }

        if (!isIndicatorEnabled)
        {
            SetContainersActive(false, false);
            return;
        }

        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                SetContainersActive(false, false);
                return;
            }
        }

        NormalMonster[] monsters = FindObjectsByType<NormalMonster>(FindObjectsSortMode.None);
        List<NormalMonster> unpurifiedLeft = new List<NormalMonster>();
        List<NormalMonster> unpurifiedRight = new List<NormalMonster>();

        NormalMonster closestLeftMonster = null;
        float minLeftDist = float.MaxValue;

        NormalMonster closestRightMonster = null;
        float minRightDist = float.MaxValue;

        foreach (var monster in monsters)
        {
            if (monster == null || monster.IsPurified) continue;

            float dist = Vector2.Distance(playerTransform.position, monster.transform.position);

            if (monster.transform.position.x < playerTransform.position.x)
            {
                unpurifiedLeft.Add(monster);
                if (dist < minLeftDist)
                {
                    minLeftDist = dist;
                    closestLeftMonster = monster;
                }
            }
            else
            {
                unpurifiedRight.Add(monster);
                if (dist < minRightDist)
                {
                    minRightDist = dist;
                    closestRightMonster = monster;
                }
            }
        }

        // 1. 왼쪽 인디케이터 갱신
        if (unpurifiedLeft.Count > 0)
        {
            SetLeftContainerActive(true);
            leftCountText.text = $"×{unpurifiedLeft.Count}";

            if (closestLeftMonster != null && leftArrowTransform != null)
            {
                Vector3 dir = (closestLeftMonster.transform.position - playerTransform.position).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                leftArrowTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
        else
        {
            SetLeftContainerActive(false);
        }

        // 2. 오른쪽 인디케이터 갱신
        if (unpurifiedRight.Count > 0)
        {
            SetRightContainerActive(true);
            rightCountText.text = $"×{unpurifiedRight.Count}";

            if (closestRightMonster != null && rightArrowTransform != null)
            {
                Vector3 dir = (closestRightMonster.transform.position - playerTransform.position).normalized;
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                rightArrowTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
        else
        {
            SetRightContainerActive(false);
        }
    }

    private void SetContainersActive(bool left, bool right)
    {
        SetLeftContainerActive(left);
        SetRightContainerActive(right);
    }

    private void SetLeftContainerActive(bool active)
    {
        if (leftContainer != null && leftContainer.activeSelf != active)
        {
            leftContainer.SetActive(active);
        }
    }

    private void SetRightContainerActive(bool active)
    {
        if (rightContainer != null && rightContainer.activeSelf != active)
        {
            rightContainer.SetActive(active);
        }
    }
}
