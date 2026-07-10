using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DashGaugeController : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("투명도를 일괄 조절할 캔버스 그룹 컴포넌트")]
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Tooltip("아치형 혹은 원형 채우기(Filled) 타입의 UI 이미지")]
    [SerializeField] private Image gaugeImage;

    [Header("Fade & Delay Settings")]
    [Tooltip("쿨타임 완료 후 완전히 충전된 상태로 유지되는 대기 시간 (초)")]
    [SerializeField] private float holdDuration = 0.5f;

    [Tooltip("서서히 투명해지며 사라지는 시간 (초)")]
    [SerializeField] private float fadeDuration = 1.0f;

    [Tooltip("게이지 충전 방향 (true: 1->0으로 소모됨, false: 0->1로 차오름)")]
    [SerializeField] private bool fillIsCooldown = false;

    private Coroutine activeRoutine;

    private void Awake()
    {
        // [지능형 자동 할당 강화] 비활성화된 자식 컴포넌트까지 꼼꼼하게 다 훑어옵니다.
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        if (gaugeImage == null)
        {
            gaugeImage = GetComponent<Image>();
            if (gaugeImage == null) gaugeImage = GetComponentInChildren<Image>(true);
        }
    }

    private void Start()
    {
        // 자가 진단 경고 로그 띄우기 (컴포넌트 실종 경고)
        if (canvasGroup == null)
        {
            Debug.LogError("[DashGauge] CanvasGroup 컴포넌트를 찾지 못했습니다! 게이지 오브젝트에 CanvasGroup을 추가해 주세요.");
        }
        if (gaugeImage == null)
        {
            Debug.LogError("[DashGauge] UI Image 컴포넌트를 찾지 못했습니다! 자식 오브젝트에 UI Image가 있는지 확인해 주세요.");
        }

        // 씬 시작 시에는 게이지를 안전하게 투명하게 숨겨놓습니다.
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
    }

    /// <summary>
    /// 플레이어가 대쉬를 사용하는 순간 호출되어 아치형 게이지 쿨타임 애니메이션을 가동합니다.
    /// </summary>
    /// <param name="cooldown">대쉬의 쿨타임 총 시간 (초)</param>
    public void StartDashGauge(float cooldown)
    {
        Debug.Log($"[DashGauge] PlayerMove 로부터 대쉬 신호 수신! 연출 준비중 (쿨타임: {cooldown}초)");
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }
        activeRoutine = StartCoroutine(DashGaugeRoutine(cooldown));
    }

    private IEnumerator DashGaugeRoutine(float cooldown)
    {
        // 컴포넌트 유실 시 콘솔에 원인을 명확하게 리포트하고 정지
        if (canvasGroup == null || gaugeImage == null)
        {
            Debug.LogError($"[DashGauge] 컴포넌트 누락으로 인해 게이지를 가동하지 못하고 튕겨나갔습니다! (CanvasGroup 연결됨: {canvasGroup != null}, Image 연결됨: {gaugeImage != null})");
            yield break;
        }

        Debug.Log("[DashGauge] 대쉬 게이지 작동 가동! (화면 노출)");
        // 1. 게이지 투명도 즉시 켜기
        canvasGroup.alpha = 1f;
        
        float elapsed = 0f;
        while (elapsed < cooldown)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / cooldown);

            // 게이지의 fillAmount를 실시간 갱신 (Radial Filled 이미지 연동)
            gaugeImage.fillAmount = fillIsCooldown ? (1f - progress) : progress;
            yield return null;
        }

        // 쿨타임이 끝난 프레임에 최종 값 고정
        gaugeImage.fillAmount = fillIsCooldown ? 0f : 1f;

        // 2. 쿨타임이 다 찬 상태로 잠시 유지 (사용자에게 완전히 찼다는 피드백 제공)
        yield return new WaitForSeconds(holdDuration);

        // 3. 지정된 시간 동안 부드럽게 Fade Out 시켜 투명화
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        activeRoutine = null;
        Debug.Log("[DashGauge] 대쉬 게이지 연출 종료 (완전 은닉)");
    }
}
