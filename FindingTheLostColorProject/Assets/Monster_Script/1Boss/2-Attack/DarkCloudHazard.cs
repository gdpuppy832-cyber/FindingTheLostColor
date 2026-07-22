using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// 먹구름 컴포넌트: 페이드인 -> 유지 -> (붓질로 지워지지 않으면) 번개 발동 신호를 BossAttack에 전달.
// 누적 붓질 시간이 paintEraseDuration을 넘으면 먹구름 스스로 페이드아웃 후 파괴됨.
public class DarkCloudHazard : MonoBehaviour
{
    public float fadeInDuration = 3f;      // 안개가 나타나는 시간
    public float holdDuration = 6f;        // 안개가 나타난 후 공격을 준비하는 시간
    public float fadeOutDuration = 1f;     // 공격이 발동될 때 사라지는 데 걸리는 시간

    [Header("체력 설정")]
    [Tooltip("먹구름의 최대 체력")]
    public float maxHealth = 2f;
    [Tooltip("일반 붓질(1번 모드)로 겹쳐 있을 때 초당 체력 감소량")]
    public float paintDamagePerSecond = 1f;
    float currentHealth;

    SpriteRenderer sr;
    Collider2D hitCollider;
    Transform cursorTransform;
    GaugeController gaugeController; // 물감 잔량 확인용
    PlayerHealth playerHealth; // 사망/피격 상태 확인용
    CursorController cursorController; // 차징 공격(2번 모드) 자체 감지용

    [Header("피격 시 부드러운 투명화 설정")]
    [Tooltip("체력이 깎였을 때, 실제 알파값이 목표 알파값을 따라가는 속도 (초당 변화율). 클수록 빠르게 투명해짐")]
    public float alphaSmoothSpeed = 3f;

    float elapsed = 0f;
    bool erased = false;
    bool finished = false;
    bool fadeInLogged = false; // 페이드인 완료 로그를 한 번만 찍기 위한 플래그
    float currentDisplayAlpha = 0f; // ★ 실제로 화면에 표시 중인 알파값 (목표치를 향해 서서히 이동)

    public bool IsErased => erased;
    public bool IsReadyToStrike { get; private set; } = false;
    bool isFadingOut = false; // 공격 발동 시 페이드아웃 진행중 (중복 호출 방지)
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        hitCollider = GetComponent<Collider2D>();
        if (hitCollider == null) hitCollider = GetComponentInChildren<Collider2D>();
        CursorController cursor = FindFirstObjectByType<CursorController>();
        if (cursor != null)
        {
            cursorTransform = cursor.transform;
            cursorController = cursor;
        }
        gaugeController = FindFirstObjectByType<GaugeController>();
        playerHealth = FindFirstObjectByType<PlayerHealth>();

        currentHealth = maxHealth;

        SetAlpha(0f);
    }

    void Update()
    {
        if (erased || finished || isFadingOut) return;
        elapsed += Time.deltaTime;

        // 페이드인이 아직 끝나지 않았으면(안개가 나타나는 중) 전부 판정에서 제외
        bool isStillFadingIn = elapsed < fadeInDuration;

        // 1번 모드(일반 붓질) 감지: CursorController의 canDraw 조건과 동일 - 조건이 유효할 때만 겹침 판정
        bool hasPaint = gaugeController == null || gaugeController.currentPaint >= gaugeController.minPaintToDraw;
        bool needsReclick = gaugeController != null && gaugeController.NeedsReclick;
        bool isDead = playerHealth != null && playerHealth.IsDead;
        bool isDrawBlocked = playerHealth != null && playerHealth.IsDrawBlocked;

        bool isMode1 = cursorController == null || cursorController.attackMode == 1;
        bool canDraw = !isStillFadingIn && isMode1 && Input.GetMouseButton(0) && hasPaint && !needsReclick && !isDead && !isDrawBlocked;

        // 일반 붓질로 겹쳐 있는 동안 초당 paintDamagePerSecond씩 체력 감소
        if (cursorTransform != null && hitCollider != null && canDraw)
        {
            if (hitCollider.OverlapPoint(cursorTransform.position))
            {
                currentHealth -= paintDamagePerSecond * Time.deltaTime;
            }
        }

        // 2번 모드(차징 샷) 감지: 차징 중(누르고 있는 동안)에는 아무 영향 없고,
        // 차징이 완료된 상태에서 마우스를 뗀 그 프레임(= 실제 차징 공격이 발동되는 순간)에만
        // 커서가 이 먹구름의 콜라이더 위에 있으면 맞은 것으로 간주하고 체력을 절반 깎음
        if (!isStillFadingIn && cursorController != null && cursorController.attackMode == 2
            && cursorController.IsChargeCompleted && IsMouseButtonUpThisFrame())
        {
            if (cursorTransform != null && hitCollider != null
                && hitCollider.OverlapPoint(cursorTransform.position))
            {
                currentHealth -= maxHealth * 0.5f;
            }
        }

        if (currentHealth <= 0f)
        {
            erased = true;
            StartCoroutine(FadeOutAndDestroyOnErase()); // ★ 완전히 지워질 때도 즉시 사라지지 않고 부드럽게 페이드아웃 후 파괴
            return;
        }

        // 목표 알파값 = 페이드인 진행도(0~1) x 남은 체력 비율(1~0).
        // 안개가 나타나는 도중에도 실시간으로 데미지를 받을 수 있으면서,
        // 체력이 줄어든 만큼만 옅어지고 회복 전까지는 다시 짙어지지 않음
        float fadeInProgress = Mathf.Clamp01(elapsed / fadeInDuration);
        float healthProgress = Mathf.Clamp01(currentHealth / maxHealth);
        float targetAlpha = fadeInProgress * healthProgress;

        // ★ 즉시 대입하지 않고, 목표 알파값을 향해 서서히 보간
        // (차징 샷처럼 체력이 한 번에 크게 깎여도 알파값이 뚝 떨어지지 않고 부드럽게 옅어짐)
        currentDisplayAlpha = Mathf.MoveTowards(currentDisplayAlpha, targetAlpha, alphaSmoothSpeed * Time.deltaTime);
        SetAlpha(currentDisplayAlpha);

        // ���̵����� �Ϸ�Ǿ� ������ ��Ÿ�� ������ �� ���� �α׷� ǥ��
        if (!fadeInLogged && elapsed >= fadeInDuration)
        {
            fadeInLogged = true;
        }

        // fadeInDuration + holdDuration�� ������ ���� �ߵ� ��ȣ
        if (elapsed >= fadeInDuration + holdDuration)
        {
            finished = true;
            IsReadyToStrike = true;
        }
    }

    // 좌클릭을 뗀 프레임인지 확인 (New Input System / 레거시 Input 모두 대응)
    bool IsMouseButtonUpThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }


    void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }
    // ���� �ߵ� �� ȣ��: ���� ���İ����� ������ 0���� ���̵�ƿ��� �� ������ �ı���
    public void StartFadeOutAndDestroy()
    {
        if (isFadingOut) return;
        isFadingOut = true;
        StartCoroutine(FadeOutRoutine());
    }

    System.Collections.IEnumerator FadeOutRoutine()
    {
        float startAlpha = sr != null ? sr.color.a : 0f;
        float t = 0f;

        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float ratio = fadeOutDuration > 0f ? Mathf.Clamp01(t / fadeOutDuration) : 1f;
            SetAlpha(Mathf.Lerp(startAlpha, 0f, ratio));
            yield return null;
        }

        SetAlpha(0f);
        Destroy(gameObject);
    }
    // 붓질로 완전히 지워졌을 때(erased) 사용하는 페이드아웃. 기존 FadeOutRoutine과 동일한 로직을 재사용.
    IEnumerator FadeOutAndDestroyOnErase()
    {
        float startAlpha = currentDisplayAlpha;
        float t = 0f;

        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            float ratio = fadeOutDuration > 0f ? Mathf.Clamp01(t / fadeOutDuration) : 1f;
            currentDisplayAlpha = Mathf.Lerp(startAlpha, 0f, ratio);
            SetAlpha(currentDisplayAlpha);
            yield return null;
        }

        SetAlpha(0f);
        Destroy(gameObject);
    }
}