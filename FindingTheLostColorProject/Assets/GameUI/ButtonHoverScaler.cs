using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverScaler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    [SerializeField] private float hoverScaleFactor = 1.1f; // 마우스를 올렸을 때의 크기 비율 (1.1 = 110%)
    [SerializeField] private float scaleDuration = 0.15f;    // 크기 변환에 걸리는 시간 (초)

    private Vector3 originalScale;
    private Vector3 targetScale;
    private Coroutine scaleCoroutine;

    private void Awake()
    {
        // 원래 크기를 저장해 둡니다.
        originalScale = transform.localScale;
        targetScale = originalScale * hoverScaleFactor;
    }

    private void OnDisable()
    {
        // 오브젝트가 비활성화될 때 스케일을 원상복구하고 코루틴을 정리합니다.
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }
        transform.localScale = originalScale;
    }

    // 마우스 커서가 UI 영역에 들어왔을 때 호출됨
    public void OnPointerEnter(PointerEventData eventData)
    {
        StartScaleAnimation(targetScale);
    }

    // 마우스 커서가 UI 영역에서 벗어났을 때 호출됨
    public void OnPointerExit(PointerEventData eventData)
    {
        StartScaleAnimation(originalScale);
    }

    private void StartScaleAnimation(Vector3 target)
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ScaleRoutine(target));
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 startScale = transform.localScale;
        float elapsedTime = 0f;

        while (elapsedTime < scaleDuration)
        {
            elapsedTime += Time.unscaledDeltaTime; // 게임 일시정지 상태에서도 작동하도록 unscaledDeltaTime 사용
            transform.localScale = Vector3.Lerp(startScale, target, elapsedTime / scaleDuration);
            yield return null;
        }

        transform.localScale = target;
        scaleCoroutine = null;
    }
}
