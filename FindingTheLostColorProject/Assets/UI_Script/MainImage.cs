using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Image))]


public class MainImage : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Alpha 0 <-> 1 사이를 페이드하는 데 걸리는 시간 (초)")]
    public float fadeDuration = 1f;

    [Tooltip("Alpha = 1 상태로 완전히 보이는 상태를 유지하는 시간 (초)")]
    public float visibleDuration = 0.5f;

    [Tooltip("Alpha = 0 상태로 완전히 투명한 상태를 유지하는 시간 (초)")]
    public float invisibleDuration = 0.5f;

    [Header("Behavior")]
    [Tooltip("오브젝트가 활성화될 때 자동으로 시작할지 여부")]
    public bool playOnEnable = true;

    private Image image;
    private Coroutine blinkRoutine;

    void Awake()
    {
        image = GetComponent<Image>();
        if (image == null)
            image = GetComponentInChildren<Image>();
    }

    void OnEnable()
    {
        if (playOnEnable)
        {
            StopBlinking();
            blinkRoutine = StartCoroutine(BlinkRoutine());
        }
    }

    void OnDisable()
    {
        StopBlinking();
    }

    private void StopBlinking()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }
    }

    /// <summary>
    /// 외부에서 수동으로 블링크를 시작하고 싶을 때 호출
    /// </summary>
    public void StartBlinking()
    {
        StopBlinking();
        blinkRoutine = StartCoroutine(BlinkRoutine());
    }

    private IEnumerator BlinkRoutine()
    {
        if (image == null) yield break;

        // 시작은 완전히 보이는 상태(Alpha = 1)로 초기화
        SetAlpha(1f);

        while (true)
        {
            // 1. 완전히 보이는 상태 유지
            yield return WaitRealtime(visibleDuration);

            // 2. Alpha 1 -> 0 페이드 아웃
            yield return FadeAlpha(1f, 0f, fadeDuration);

            // 3. 완전히 투명한 상태 유지
            yield return WaitRealtime(invisibleDuration);

            // 4. Alpha 0 -> 1 페이드 인
            yield return FadeAlpha(0f, 1f, fadeDuration);
        }
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetAlpha(to);
    }

    private IEnumerator WaitRealtime(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSecondsRealtime(duration);
    }

    private void SetAlpha(float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
