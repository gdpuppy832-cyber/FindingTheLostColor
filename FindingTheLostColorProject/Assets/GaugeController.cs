using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Image))]
public class GaugeController : MonoBehaviour
{
    [Header("Paint Settings")]
    [Tooltip("물감의 최대 용량")]
    public float maxPaint = 1f;

    [Tooltip("물감의 현재 남은 용량")]
    public float currentPaint = 1f;

    [Tooltip("우클릭 시 초당 감소량 (기본값: 0.05)")]
    public float decreaseSpeed = 0.05f;

    [Tooltip("우클릭을 뗐을 때 초당 재생량 (기본값: 0.25)")]
    public float regenSpeed = 0.25f;

    private Image gaugeImage;

    void Awake()
    {
        gaugeImage = GetComponent<Image>();
    }

    void Update()
    {
        bool isRightClickHeld = false;

#if ENABLE_INPUT_SYSTEM
        // New Input System 사용 시
        if (Mouse.current != null)
        {
            isRightClickHeld = Mouse.current.rightButton.isPressed;
        }
#else
        // Legacy Input Manager 사용 시
        isRightClickHeld = Input.GetMouseButton(1);
#endif

        if (isRightClickHeld)
        {
            // 우클릭을 누르고 있는 동안 초당 0.05씩 감소 (0이하로 떨어지지 않음)
            currentPaint -= decreaseSpeed * Time.deltaTime;
        }
        else
        {
            // 우클릭을 떼면 초당 0.25씩 재생 (1이상으로 늘어나지 않음)
            currentPaint += regenSpeed * Time.deltaTime;
        }

        // 최솟값 0, 최댓값 1로 제한
        currentPaint = Mathf.Clamp(currentPaint, 0f, maxPaint);

        // UI Image의 fillAmount에 반영
        if (gaugeImage != null)
        {
            gaugeImage.fillAmount = currentPaint / maxPaint;
        }
    }
}