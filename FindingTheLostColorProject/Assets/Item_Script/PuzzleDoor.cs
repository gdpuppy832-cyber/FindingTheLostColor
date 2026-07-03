using UnityEngine;
using UnityEngine.UI;

public class PuzzleDoor : InteractableObject
{
    [Header("문 설정")]
    [Tooltip("비활성화할 문 콜라이더")]
    public Collider2D doorCollider;

    [Tooltip("문 이미지 SpriteRenderer")]
    public SpriteRenderer doorRenderer;

    [Header("카메라 흔들림 효과")]
    [Tooltip("문이 열릴 때 흔들릴 카메라 진동 강도")]
    public float shakeIntensity = 0.3f;

    [Tooltip("문이 열릴 때 흔들릴 시간 (초)")]
    public float shakeDuration = 0.5f;

    [Header("문 개방 시 색상 효과")]
    [Tooltip("문이 열릴 때 변경할 색상 (기본값은 투명도 0으로 완전히 안 보이게 가림)")]
    public Color openedColor = new Color(1f, 1f, 1f, 0f);

    [Header("상호작용 텍스트메쉬 설정")]
    [Tooltip("문이 열리고 플레이어가 진입 구역 내에 감지되었을 때 나타낼 3D TextMesh 컴포넌트")]
    public TextMesh doorPromptTextMesh;

    [Tooltip("텍스트메쉬가 없을 시 대용할 일반 텍스트 게임오브젝트")]
    public GameObject doorPromptTextObject;

    [Header("씬 이동 및 페이드 설정")]
    [Tooltip("문 진입 시 이동할 다음 씬 이름")]
    public string nextSceneName = "MainStage";

    [Tooltip("화면 페이드 아웃에 사용할 UI Canvas (비워두면 씬 전체 캔버스 자동 탐색)")]
    public Canvas hudCanvas;

    [Tooltip("화면을 어둡게 페이드 아웃시킬 단색 이미지 슬롯 (비워두면 자동 생성)")]
    public Image customFadeImage;

    [Tooltip("씬 전환 페이드 아웃 시간 (초)")]
    public float fadeDuration = 1.0f;

    private bool isOpened = false;
    private bool isTransitioning = false;

    void Awake()
    {
        // 상호작용 안내 문구 설정
        promptMessage = "W키를 눌러 다음 구역으로 이동";
        // 상호작용 가능 반경 기본값
        interactionRadius = 2.0f;
    }

    void Start()
    {
        if (doorCollider == null) doorCollider = GetComponent<Collider2D>();
        if (doorRenderer == null) doorRenderer = GetComponent<SpriteRenderer>();

        // 시작 시 상호작용 텍스트메쉬 투명/비활성화 처리
        SetTextVisible(false);

        // 시작 시 상호작용 기능 비활성화 (문이 열렸을 때만 W키 상호작용이 가능해야 하므로)
        enabled = false;
    }

    /// <summary>
    /// 문을 열어 길을 트고 카메라를 흔듭니다.
    /// </summary>
    public void Open()
    {
        if (isOpened) return;
        isOpened = true;

        // 1. 충돌 판정 완화 (IsTrigger = true로 변경하여 지나갈 수 있게 함)
        if (doorCollider != null)
        {
            doorCollider.isTrigger = true;
        }

        // 2. 투명하게 만들어 문 열림 시각화
        if (doorRenderer != null)
        {
            doorRenderer.color = openedColor;
        }

        // 3. 카메라 흔들림 연동
        CameraFollow camFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (camFollow == null)
        {
            camFollow = FindFirstObjectByType<CameraFollow>();
        }

        if (camFollow != null)
        {
            camFollow.TriggerShake(shakeIntensity, shakeDuration);
        }

        // 4. 문이 열렸으므로 W키 상호작용 컴포넌트(이 스크립트) 활성화
        enabled = true;

        Debug.Log($"[PuzzleDoor] {gameObject.name} 문 개방! W키 상호작용 기능이 활성화되었습니다.");
    }

    /// <summary>
    /// W키 상호작용 트리거 (플레이어가 문 앞에서 W키를 누르면 다음 씬 이동)
    /// </summary>
    public override void OnInteract()
    {
        if (!isOpened || isTransitioning) return;
        isTransitioning = true;

        // 진입 시작 시 텍스트메쉬도 안전하게 숨김
        SetTextVisible(false);

        // 1. 플레이어 조작 잠금 (이동 및 물리 제어권 해제)
        PlayerMove playerMove = FindFirstObjectByType<PlayerMove>();
        if (playerMove != null) playerMove.SetControl(false);

        // 2. 그리기 공격 조작 잠금
        CursorController cursor = FindFirstObjectByType<CursorController>();
        if (cursor != null) cursor.enabled = false;

        // 3. 상호작용 안내창 잠금
        PlayerInteraction playerInt = FindFirstObjectByType<PlayerInteraction>();
        if (playerInt != null) playerInt.SetInteractionLock(true);

        // 4. 페이드 아웃 연출 및 씬 전환 코루틴 시작
        StartCoroutine(TransitionToNextStage());
    }

    /// <summary>
    /// 플레이어 진입 시 머리 위 텍스트메쉬 활성화/투명 해제
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isOpened) return;

        if (collision.CompareTag("Player"))
        {
            SetTextVisible(true);
        }
    }

    /// <summary>
    /// 플레이어가 영역을 벗어나면 다시 텍스트메쉬 비활성화/투명화
    /// </summary>
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!isOpened) return;

        if (collision.CompareTag("Player"))
        {
            SetTextVisible(false);
        }
    }

    /// <summary>
    /// 텍스트메쉬의 투명도(Alpha) 및 오브젝트 활성화 상태 일괄 제어
    /// </summary>
    private void SetTextVisible(bool visible)
    {
        if (doorPromptTextMesh != null)
        {
            // 알파값을 조절해 투명/불투명 전환
            Color col = doorPromptTextMesh.color;
            col.a = visible ? 1f : 0f;
            doorPromptTextMesh.color = col;
        }

        if (doorPromptTextObject != null)
        {
            doorPromptTextObject.SetActive(visible);
        }
    }

    /// <summary>
    /// 화면을 페이드 아웃시킨 뒤 다음 씬으로 안전하게 이동합니다.
    /// </summary>
    private System.Collections.IEnumerator TransitionToNextStage()
    {
        Image targetFadeImage = customFadeImage;
        GameObject spawnedFadeObj = null;

        if (targetFadeImage == null)
        {
            Canvas targetCanvas = hudCanvas != null ? hudCanvas : FindFirstObjectByType<Canvas>();

            // 스크린 스페이스 캔버스 자동 검색
            if (targetCanvas != null && targetCanvas.renderMode == RenderMode.WorldSpace)
            {
                Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var c in allCanvases)
                {
                    if (c.renderMode != RenderMode.WorldSpace)
                    {
                        targetCanvas = c;
                        break;
                    }
                }
            }

            if (targetCanvas != null)
            {
                spawnedFadeObj = new GameObject("PuzzleDoor_FadeOverlay");
                spawnedFadeObj.transform.SetParent(targetCanvas.transform, false);

                RectTransform rect = spawnedFadeObj.AddComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;

                targetFadeImage = spawnedFadeObj.AddComponent<Image>();
                targetFadeImage.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        if (targetFadeImage != null)
        {
            targetFadeImage.gameObject.SetActive(true);

            float elapsed = 0f;
            Color baseColor = targetFadeImage.color;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                targetFadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            targetFadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        }
        else
        {
            yield return new WaitForSeconds(fadeDuration);
        }

        // 다음 구역 씬 로드
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneName);
    }

    /// <summary>
    /// 문을 다시 리셋하여 닫는 함수
    /// </summary>
    public void ResetDoor()
    {
        isOpened = false;
        isTransitioning = false;

        if (doorCollider != null)
        {
            doorCollider.isTrigger = false;
        }
        if (doorRenderer != null)
        {
            doorRenderer.color = Color.white;
        }

        // 텍스트메쉬도 안전하게 다시 비활성/투명화
        SetTextVisible(false);

        // 문이 다시 닫혔으므로 상호작용 컴포넌트 비활성화
        enabled = false;
    }
}
