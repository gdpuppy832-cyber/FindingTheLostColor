using UnityEngine;
using UnityEngine.UI;
using System.Collections;
// Graphic은 Image, RawImage, Text 등 UI 컴포넌트들의 공통 부모 클래스

/// <summary>
/// 씬 시작 컷씬(S2_SceneStartCutsceneController) 동안에는 그대로 존재하다가,
/// 컷씬이 완전히 끝나는 시점에 알파(투명도)가 서서히 0으로 내려가며 사라지는 오브젝트에 붙임.
/// SpriteRenderer 또는 CanvasGroup 중 붙어있는 쪽을 자동으로 사용해서 페이드함.
/// 이 스크립트 자체는 스스로 실행되지 않고, S2_SceneStartCutsceneController가
/// HideNow()를 호출해서 페이드아웃을 시작시키는 용도로만 쓰임.
/// </summary>
public class HideAfterCutscene : MonoBehaviour
{
    [Tooltip("알파가 1에서 0으로 서서히 줄어드는 데 걸리는 시간(초)")]
    public float fadeDuration = 1f;

    [Tooltip("페이드가 완전히 끝난 뒤(알파 0) 오브젝트 자체를 비활성화할지 여부")]
    public bool deactivateAfterFade = true;

    private SpriteRenderer spriteRenderer;
    private CanvasGroup canvasGroup;
    private Graphic uiGraphic; // Image, RawImage, Text 등 CanvasGroup 없이 단독으로 붙어있는 UI 컴포넌트 대응
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        canvasGroup = GetComponent<CanvasGroup>();

        // CanvasGroup이 없을 때만 Graphic을 사용 (CanvasGroup이 있으면 그쪽이 자식 전체를 한 번에 제어하므로 우선함)
        if (canvasGroup == null)
        {
            uiGraphic = GetComponent<Graphic>();
        }
    }

    /// <summary>컷씬이 완전히 끝나는 시점에 호출. 알파를 1초(기본값) 동안 0으로 페이드아웃시킨다.</summary>
    public void HideNow()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        fadeCoroutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float elapsed = 0f;
        float startAlpha = GetCurrentAlpha();

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            SetAlpha(Mathf.Lerp(startAlpha, 0f, t));
            yield return null;
        }

        SetAlpha(0f);
        fadeCoroutine = null;

        if (deactivateAfterFade)
        {
            gameObject.SetActive(false);
        }
    }

    private float GetCurrentAlpha()
    {
        if (spriteRenderer != null) return spriteRenderer.color.a;
        if (canvasGroup != null) return canvasGroup.alpha;
        if (uiGraphic != null) return uiGraphic.color.a;
        return 1f;
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }

        if (uiGraphic != null)
        {
            Color c = uiGraphic.color;
            c.a = alpha;
            uiGraphic.color = c;
        }
    }
}