using UnityEngine;
using System.Collections;

// �Ա��� ������Ʈ: ���̵��� -> ��� -> (������ �������� ������) ���� �ߵ� ��ȣ�� BossAttack�� ����.
// ���� ���� �ð��� paintEraseDuration�� �����ϸ� ������ ���������� ������ �ı���.
public class DarkCloudHazard : MonoBehaviour
{
    public float fadeInDuration = 3f;      // 안개가 나타나는 시간
    public float holdDuration = 6f;        // 안개가 나타난 후 공격을 준비하는 시간
    public float paintEraseDuration = 3f;  // 누적 붓질 시간이 이 값을 넘으면 지워짐
    public float fadeOutDuration = 1f;     // 공격이 발동될 때 사라지는 데 걸리는 시간

    SpriteRenderer sr;
    Collider2D hitCollider;
    Transform cursorTransform;
    GaugeController gaugeController; // 물감 잔량 확인용
    PlayerHealth playerHealth; // 사망/피격 상태 확인용

    float elapsed = 0f;
    float cumulativePaintTime = 0f;
    bool erased = false;
    bool finished = false;
    bool fadeInLogged = false; // ���̵��� �Ϸ� �α׸� �� ���� ��� ���� �÷���

    public bool IsErased => erased;
    public bool IsReadyToStrike { get; private set; } = false;
    bool isFadingOut = false; // ���� �ߵ� �� ���̵�ƿ� ������ (�ߺ� ȣ�� ����)

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();
        hitCollider = GetComponent<Collider2D>();
        if (hitCollider == null) hitCollider = GetComponentInChildren<Collider2D>();
        CursorController cursor = FindFirstObjectByType<CursorController>();
        if (cursor != null) cursorTransform = cursor.transform;
        gaugeController = FindFirstObjectByType<GaugeController>();
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        SetAlpha(0f);
    }

    void Update()
    {
        if (erased || finished || isFadingOut) return;
        elapsed += Time.deltaTime;

        // ���̵����� ���� ������ �ʾ�����(������ ��Ÿ���� ��) ���� ��ü�� ������ ����
        bool isStillFadingIn = elapsed < fadeInDuration;

        // ���� ����: CursorController�� canDraw�� ������ ���� - ������ Ʈ���Ͽ� ���� ������ ������ ���� ��ȿ
        bool hasPaint = gaugeController == null || gaugeController.currentPaint >= gaugeController.minPaintToDraw;
        bool needsReclick = gaugeController != null && gaugeController.NeedsReclick;
        bool isDead = playerHealth != null && playerHealth.IsDead;
        bool isDrawBlocked = playerHealth != null && playerHealth.IsDrawBlocked;
        bool canDraw = !isStillFadingIn && Input.GetMouseButton(0) && hasPaint && !needsReclick && !isDead && !isDrawBlocked;

        if (cursorTransform != null && hitCollider != null && canDraw)
        {
            if (hitCollider.OverlapPoint(cursorTransform.position))
            {
                cumulativePaintTime += Time.deltaTime;
            }
        }

        // 누적 붓질 시간이 paintEraseDuration을 넘으면 지워짐 (제거 취급)
        if (cumulativePaintTime >= paintEraseDuration)
        {
            erased = true;
            SetAlpha(0f);
            Destroy(gameObject);
            return;
        }

        // 알파값 = 페이드인 진행도(0~1)와 "지워지는 진행도(1~0)"를 곱한 값.
        // 이렇게 하면 안개가 나타나는 도중에는 실시간으로 지울 수 있으면서도,
        // 지우는 진척만 잘 남으면 그대로 유지된 채(중간에 사라지지도, 다시 짙어지지도 않고) 유지됨
        float fadeInProgress = Mathf.Clamp01(elapsed / fadeInDuration);
        float eraseProgress = 1f - Mathf.Clamp01(cumulativePaintTime / paintEraseDuration);
        SetAlpha(fadeInProgress * eraseProgress);

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
}