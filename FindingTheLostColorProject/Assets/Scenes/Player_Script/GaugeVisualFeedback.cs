using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GaugeVisualFeedback : MonoBehaviour
{
    [Header("Target UI Components")]
    [Tooltip("색상이 동시에 점멸할 UI 이미지들 (예: 테두리, 채워진 게이지, 배경 등)")]
    [SerializeField] private Image[] warningImages;
    [Tooltip("흔들림(Shake) 연출을 적용할 RectTransform (미지정 시 본인의 RectTransform 사용)")]
    [SerializeField] private RectTransform targetRect;

    [Header("Color Flashing Settings")]
    [Tooltip("물감 부족 경고 시 점멸할 색상")]
    [SerializeField] private Color warningColor = Color.red;
    [Tooltip("경고 점멸 1회 주기 시간 (초)")]
    [SerializeField] private float flashDuration = 0.12f;
    [Tooltip("경고 점멸 반복 횟수")]
    [SerializeField] private int flashCount = 2;

    [Header("Shake Settings")]
    [Tooltip("흔들림 지속 시간 (초)")]
    [SerializeField] private float shakeDuration = 0.25f;
    [Tooltip("흔들림 강도 (픽셀 반경)")]
    [SerializeField] private float shakeAmount = 10f;

    // 원래 상태 백업용
    private Color[] originalColors;
    private Vector2 originalPosition;

    private Coroutine flashCoroutine;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        // warningImages 배열이 지정되지 않았거나 비었을 때 컴포넌트 자동 백업 수색
        if (warningImages == null || warningImages.Length == 0)
        {
            Image myImage = GetComponent<Image>();
            if (myImage != null)
            {
                warningImages = new Image[] { myImage };
            }
            else
            {
                warningImages = GetComponentsInChildren<Image>(true);
            }
        }

        if (targetRect == null)
        {
            targetRect = GetComponent<RectTransform>();
        }

        // 각 이미지의 원래 본래 색상들 기억 캐싱
        if (warningImages != null && warningImages.Length > 0)
        {
            originalColors = new Color[warningImages.Length];
            for (int i = 0; i < warningImages.Length; i++)
            {
                if (warningImages[i] != null)
                {
                    originalColors[i] = warningImages[i].color;
                }
            }
        }

        if (targetRect != null)
        {
            originalPosition = targetRect.anchoredPosition;
        }
    }

    /// <summary>
    /// 물감 부족 시 호출되어 빨간 점멸과 게이지 흔들림 연출을 시작합니다.
    /// </summary>
    public void TriggerFeedback()
    {
        // 1. 점멸 연출 시작
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());

        // 2. 흔들림 연출 시작
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            if (targetRect != null) targetRect.anchoredPosition = originalPosition;
        }
        shakeCoroutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        if (warningImages == null || warningImages.Length == 0 || originalColors == null) yield break;

        for (int c = 0; c < flashCount; c++)
        {
            // 빨간 경고색으로 페이드인
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDuration;
                for (int i = 0; i < warningImages.Length; i++)
                {
                    if (warningImages[i] != null && i < originalColors.Length)
                    {
                        warningImages[i].color = Color.Lerp(originalColors[i], warningColor, t);
                    }
                }
                yield return null;
            }

            // 다시 원래 색상으로 페이드아웃
            elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flashDuration;
                for (int i = 0; i < warningImages.Length; i++)
                {
                    if (warningImages[i] != null && i < originalColors.Length)
                    {
                        warningImages[i].color = Color.Lerp(warningColor, originalColors[i], t);
                    }
                }
                yield return null;
            }
        }

        // 최종 복구 정렬
        for (int i = 0; i < warningImages.Length; i++)
        {
            if (warningImages[i] != null && i < originalColors.Length)
            {
                warningImages[i].color = originalColors[i];
            }
        }
        flashCoroutine = null;
    }

    private IEnumerator ShakeRoutine()
    {
        if (targetRect == null) yield break;

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            
            // 무작위 구체 좌표를 통해 원점을 기준으로 흔들기
            Vector2 randomOffset = Random.insideUnitCircle * shakeAmount;
            targetRect.anchoredPosition = originalPosition + randomOffset;

            yield return null;
        }

        // 원래 원점 위치로 칼정렬 복귀
        targetRect.anchoredPosition = originalPosition;
        shakeCoroutine = null;
    }

    // 외부(GaugeController 등)에서 특정 이미지 본래 색상을 동적으로 변경할 경우 캐시를 수동 갱신해 주는 용도
    public void SetOriginalColorAt(int index, Color color)
    {
        if (originalColors != null && index >= 0 && index < originalColors.Length)
        {
            originalColors[index] = color;
            if (flashCoroutine == null && warningImages != null && warningImages[index] != null)
            {
                warningImages[index].color = color;
            }
        }
    }
}
