using UnityEngine;
using System.Collections;

// 먹구름 오브젝트: 페이드인 -> 대기 -> (붓질로 지워지지 않으면) 번개 발동 신호를 BossAttack에 전달.
// 붓질 누적 시간이 paintEraseDuration에 도달하면 서서히 투명해지다 스스로 파괴됨.
public class DarkCloudHazard : MonoBehaviour
{
    public float fadeInDuration = 3f;      // 서서히 나타나는 시간
    public float holdDuration = 6f;        // 완전히 나타난 뒤 번개가 치기까지 대기하는 시간
    public float paintEraseDuration = 3f;  // 누적 붓질 시간이 이 값에 도달하면 지워짐
    public float fadeOutDuration = 1f;     // 붓질로 지워질 때 사라지는 데 걸리는 시간

    SpriteRenderer sr;
    Collider2D hitCollider;
    Transform cursorTransform;

    float elapsed = 0f;
    float cumulativePaintTime = 0f;
    bool erased = false;
    bool finished = false;
    bool fadeInLogged = false; // 페이드인 완료 로그를 한 번만 찍기 위한 플래그

    public bool IsErased => erased;
    public bool IsReadyToStrike { get; private set; } = false;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        hitCollider = GetComponent<Collider2D>();
        if (hitCollider == null) hitCollider = GetComponentInChildren<Collider2D>();

        CursorController cursor = FindFirstObjectByType<CursorController>();
        if (cursor != null) cursorTransform = cursor.transform;

        SetAlpha(0f);
    }

    void Update()
    {
        if (erased || finished) return;

        elapsed += Time.deltaTime;

        // 붓질 판정: 좌클릭 중이고 커서가 이 먹구름의 콜라이더 모양 안에 들어와 있으면 누적
        if (cursorTransform != null && hitCollider != null && Input.GetMouseButton(0))
        {
            if (hitCollider.OverlapPoint(cursorTransform.position))
            {
                cumulativePaintTime += Time.deltaTime;
            }
        }

        // 누적 붓질 시간이 paintEraseDuration에 도달하면 완전히 지워짐 (공격 취소)
        if (cumulativePaintTime >= paintEraseDuration)
        {
            erased = true;
            SetAlpha(0f);
            Destroy(gameObject);
            return;
        }

        // 알파값 = 페이드인 진행도(0~1)와 "붓질로 지워지는 진행도(1~0)"를 곱한 값.
        // 이렇게 하면 붓질을 하는 도중에도 실시간으로 점점 투명해지고,
        // 붓질을 멈추면 그 투명도 그대로 유지된 채(추가로 옅어지지도, 다시 진해지지도 않고) 대기함
        float fadeInProgress = Mathf.Clamp01(elapsed / fadeInDuration);
        float eraseProgress = 1f - Mathf.Clamp01(cumulativePaintTime / paintEraseDuration);
        SetAlpha(fadeInProgress * eraseProgress);

        // 페이드인이 완료되어 완전히 나타난 시점을 한 번만 로그로 표시
        if (!fadeInLogged && elapsed >= fadeInDuration)
        {
            fadeInLogged = true;
            Debug.Log($"[DarkCloudHazard] 먹구름 생성 완료 (페이드인 {fadeInDuration}초 종료) - 위치: {transform.position}");
        }

        // fadeInDuration + holdDuration이 지나면 번개 발동 신호
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
}