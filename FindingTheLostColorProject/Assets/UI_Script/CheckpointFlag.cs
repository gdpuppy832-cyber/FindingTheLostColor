using UnityEngine;
using System.Collections;
using TMPro;

[RequireComponent(typeof(Collider2D))]
public class CheckpointFlag : MonoBehaviour
{
    [Header("Flag Sprites")]
    [Tooltip("플레이어와 접촉 전 - 무채색 깃발 스프라이트")]
    [SerializeField] private Sprite uncoloredSprite;
    
    [Tooltip("플레이어와 접촉 후 - 색깔 깃발 스프라이트")]
    [SerializeField] private Sprite coloredSprite;

    [Header("Visual Components")]
    [Tooltip("깃발의 SpriteRenderer (없으면 자기 자신 또는 자식에서 자동 검색)")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("자식 오브젝트로 있는 텍스트 컴포넌트 (Floating Text 연출용)")]
    [SerializeField] private TMP_Text floatingText;

    [Header("Settings")]
    [Tooltip("재접촉 대기시간 (쿨타임, 초)")]
    [SerializeField] private float recontactCooldown = 1f;

    [Tooltip("텍스트 연출 시간 (초)")]
    [SerializeField] private float floatDuration = 1.5f;

    [Tooltip("텍스트가 떠오르는 높이")]
    [SerializeField] private float floatHeight = 1.5f;

    private bool isCooldown = false;
    private bool isPlayerOverlapping = false; // 플레이어가 아직 깃발 영역 안에 머물러 있는지 확인용

    private Coroutine textCoroutine;
    private Vector3 textOriginalLocalPos;
    private Color textOriginalColor;

    private void Awake()
    {
        // Collider2D Trigger 세팅 강제 적용 (기획대로 충돌 시 저장)
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        // 컴포넌트 자동 캐싱
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (floatingText == null)
        {
            floatingText = GetComponentInChildren<TMP_Text>();
        }

        // 초기 비주얼 설정 (활성화 전이므로 무채색 깃발 세팅)
        if (spriteRenderer != null && uncoloredSprite != null)
        {
            spriteRenderer.sprite = uncoloredSprite;
        }

        // 텍스트 초기 상태 세팅 (보이지 않게 숨김)
        if (floatingText != null)
        {
            floatingText.gameObject.SetActive(false);
            textOriginalLocalPos = floatingText.transform.localPosition;
            textOriginalColor = floatingText.color;
        }
    }

    private void Start()
    {
        // 씬 로드 및 세이브포인트 부활 직후 깃발 위에서 시작하더라도 2초간 세이브 재갱신 연출이 튀어나오지 않도록 쿨타임 가동
        StartCoroutine(CooldownRoutine());

        // 만약 씬이 재로드되었을 때 이미 이 깃발이 저장된 부활 깃발이라면, 색깔 깃발로 자동 동기화 해 줍니다.
        if (SavePointManager.Instance != null && SavePointManager.Instance.HasSaveData)
        {
            // 이 깃발의 좌표와 저장된 세이브 포인트의 좌표가 매우 근접(0.5m 이내)하다면 본 깃발이 활성 깃발임
            float distance = Vector3.Distance(transform.position, SavePointManager.Instance.SavedPlayerPosition);
            if (distance < 2.0f && SavePointManager.Instance.SavedSceneName == gameObject.scene.name)
            {
                if (spriteRenderer != null && coloredSprite != null)
                {
                    spriteRenderer.sprite = coloredSprite;
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 플레이어 판정 (태그 비교 또는 PlayerMove 컴포넌트 소지 여부로 이중 교차 검사하여 감지 보장!)
        bool isPlayer = collision.CompareTag("Player") || 
                        collision.GetComponentInParent<PlayerMove>() != null || 
                        collision.GetComponentInChildren<PlayerMove>() != null;

        if (isPlayer)
        {
            // [중복 갱신 방지 가드]: 쿨타임 중이거나, 이미 겹쳐진 채 머물러 있는 상태면 무시
            if (isCooldown || isPlayerOverlapping) return;

            isPlayerOverlapping = true;

            // 1. 체크포인트 저장 실행
            if (SavePointManager.Instance != null)
            {
                // 플레이어가 깃발에 부딪힌 시점의 좌표로 세이브 포인트 지정
                SavePointManager.Instance.SaveCheckpoint(collision.transform.position);
            }

            // 2. 깃발 색상 변경 (활성화 완료 연출)
            if (spriteRenderer != null && coloredSprite != null)
            {
                spriteRenderer.sprite = coloredSprite;
            }

            // 3. 안내 텍스트 1.5초간 공중 부양하며 사라지는 코루틴 실행
            if (floatingText != null)
            {
                if (textCoroutine != null) StopCoroutine(textCoroutine);
                textCoroutine = StartCoroutine(FloatingTextRoutine("저장 완료!"));
            }

            // 4. 1초 재접촉 대기시간 가동
            StartCoroutine(CooldownRoutine());
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        // 플레이어가 깃발 충돌 영역을 완전히 빠져나갔을 때만 재접촉이 가능하게 해제
        bool isPlayer = collision.CompareTag("Player") || 
                        collision.GetComponentInParent<PlayerMove>() != null || 
                        collision.GetComponentInChildren<PlayerMove>() != null;

        if (isPlayer)
        {
            isPlayerOverlapping = false;
            Debug.Log("[CheckpointFlag] 플레이어가 깃발 영역을 벗어났습니다. 다음 접촉 시 갱신이 허용됩니다.");
        }
    }

    /// <summary>
    /// 텍스트가 1.5초간 천천히 상승하며 투명해지는 예쁜 부양 연출 코루틴
    /// </summary>
    private IEnumerator FloatingTextRoutine(string message)
    {
        floatingText.text = message;
        floatingText.gameObject.SetActive(true);

        float elapsed = 0f;
        
        // 텍스트 위치 및 투명도 초기화
        floatingText.transform.localPosition = textOriginalLocalPos;
        floatingText.color = textOriginalColor;

        Vector3 startPos = textOriginalLocalPos;
        Vector3 targetPos = startPos + new Vector3(0f, floatHeight, 0f);

        while (elapsed < floatDuration)
        {
            // unscaledDeltaTime 적용으로 게임 일시정지 중에도 유려하게 연출되도록 연동
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / floatDuration);

            // 1. 위로 서서히 떠오르기 (Ease Out 효과 가미)
            float t = Mathf.Sin(ratio * Mathf.PI * 0.5f); // Ease Out Sine
            floatingText.transform.localPosition = Vector3.Lerp(startPos, targetPos, t);

            // 2. 서서히 투명화 (Fade Out)
            Color c = textOriginalColor;
            c.a = Mathf.Lerp(textOriginalColor.a, 0f, ratio);
            floatingText.color = c;

            yield return null;
        }

        // 최종 초기화 후 숨기기
        floatingText.gameObject.SetActive(false);
        floatingText.transform.localPosition = textOriginalLocalPos;
        floatingText.color = textOriginalColor;
        textCoroutine = null;
    }

    /// <summary>
    /// 1초의 재접촉 방지 쿨타임 코루틴
    /// </summary>
    private IEnumerator CooldownRoutine()
    {
        isCooldown = true;
        yield return new WaitForSeconds(recontactCooldown);
        isCooldown = false;
    }
}
