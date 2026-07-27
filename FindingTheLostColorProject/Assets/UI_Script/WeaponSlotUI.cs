using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class WeaponSlotUI : MonoBehaviour
{
    public static WeaponSlotUI Instance { get; private set; }

    [Header("Weapon Sprites")]
    [Tooltip("1번 공격 모드 아이콘 (일반 브러시 스프라이트)")]
    [SerializeField] private Sprite mode1Sprite;

    [Tooltip("2번 공격 모드 아이콘 (차징 샷 스프라이트)")]
    [SerializeField] private Sprite mode2Sprite;

    [Header("UI Slot References")]
    [Tooltip("메인 무기 슬롯 (주무기 큰 이미지)")]
    [SerializeField] private Image mainSlotImage;

    [Tooltip("서브 무기 슬롯 (부무기 작은 이미지)")]
    [SerializeField] private Image subSlotImage;

    [Header("Animation Settings (아펠리오스 스타일 스왑)")]
    [Tooltip("스왑 연출 진행 시간 (초, 기본값: 0.18초)")]
    [SerializeField] private float swapDuration = 0.18f;

    [Tooltip("메인 슬롯 크기 비율 (기본값: 1.0)")]
    [SerializeField] private float mainScale = 1.0f;

    [Tooltip("서브 슬롯 크기 비율 (기본값: 0.65)")]
    [SerializeField] private float subScale = 0.65f;

    [Tooltip("서브 슬롯 투명도 (기본값: 0.5)")]
    [SerializeField] private float subAlpha = 0.5f;

    private Coroutine swapCoroutine;
    private Vector3 mainOriginalPos;
    private Vector3 subOriginalPos;
    private int currentMode = 1; // 1: 일반, 2: 차징

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 메인/서브 슬롯의 원래 로컬 좌표 기억
        if (mainSlotImage != null) mainOriginalPos = mainSlotImage.rectTransform.localPosition;
        if (subSlotImage != null) subOriginalPos = subSlotImage.rectTransform.localPosition;

        // 초기 무기 슬롯 상태 세팅 (1번 모드로 시작)
        UpdateSlotVisualsInstant(1);
    }

    /// <summary>
    /// 외부(CursorController)에서 E키를 눌러 공격 모드가 바뀔 때 호출하는 아펠리오스 스왑 함수
    /// </summary>
    /// <param name="newMode">1: 일반 브러시, 2: 차징 샷</param>
    public void OnAttackModeChanged(int newMode)
    {
        if (currentMode == newMode) return;
        currentMode = newMode;

        if (swapCoroutine != null) StopCoroutine(swapCoroutine);
        swapCoroutine = StartCoroutine(SwapAnimationRoutine(newMode));
    }

    /// <summary>
    /// 애니메이션 없이 즉시 슬롯 이미지와 크기를 셋업하는 함수 (Start 시 사용)
    /// </summary>
    public void UpdateSlotVisualsInstant(int mode)
    {
        currentMode = mode;
        if (mainSlotImage == null || subSlotImage == null) return;

        if (mode == 1)
        {
            // 1번 모드: 메인 = 일반, 서브 = 차징
            if (mode1Sprite != null) mainSlotImage.sprite = mode1Sprite;
            if (mode2Sprite != null) subSlotImage.sprite = mode2Sprite;
        }
        else
        {
            // 2번 모드: 메인 = 차징, 서브 = 일반
            if (mode2Sprite != null) mainSlotImage.sprite = mode2Sprite;
            if (mode1Sprite != null) subSlotImage.sprite = mode1Sprite;
        }

        // 트랜스폼 및 알파 즉시 동기화
        mainSlotImage.rectTransform.localPosition = mainOriginalPos;
        mainSlotImage.rectTransform.localScale = Vector3.one * mainScale;
        SetImageAlpha(mainSlotImage, 1.0f);

        subSlotImage.rectTransform.localPosition = subOriginalPos;
        subSlotImage.rectTransform.localScale = Vector3.one * subScale;
        SetImageAlpha(subSlotImage, subAlpha);
    }

    /// <summary>
    /// 메인 슬롯과 서브 슬롯이 부드럽게 교차하고 크기/투명도가 스와핑되는 아펠리오스 스타일 코루틴
    /// </summary>
    private IEnumerator SwapAnimationRoutine(int newMode)
    {
        if (mainSlotImage == null || subSlotImage == null) yield break;

        float elapsed = 0f;

        // 시작 및 목표 좌표/크기/알파 설정
        Vector3 mainStartPos = mainSlotImage.rectTransform.localPosition;
        Vector3 subStartPos = subSlotImage.rectTransform.localPosition;

        Vector3 mainStartScale = mainSlotImage.rectTransform.localScale;
        Vector3 subStartScale = subSlotImage.rectTransform.localScale;

        // 서로의 위치 교차 이동
        Vector3 mainTargetPos = subOriginalPos;
        Vector3 subTargetPos = mainOriginalPos;

        // 교차 진행 중 (0.18초간 이동하면서 1/2 지점에서 이미지 교체)
        bool imageSwapped = false;

        while (elapsed < swapDuration)
        {
            elapsed += Time.unscaledDeltaTime; // 일시정지 중에도 유려하게 반응
            float ratio = Mathf.Clamp01(elapsed / swapDuration);

            // EaseInOut Sine 곡선 적용으로 쫀득한 가속도 연출
            float t = Mathf.Sin(ratio * Mathf.PI * 0.5f);

            // 1. 위치 교차 수치 계산
            mainSlotImage.rectTransform.localPosition = Vector3.Lerp(mainStartPos, mainTargetPos, t);
            subSlotImage.rectTransform.localPosition = Vector3.Lerp(subStartPos, subTargetPos, t);

            // 2. 크기 교차 수치 계산 (메인은 작아지고 서브는 커짐)
            mainSlotImage.rectTransform.localScale = Vector3.Lerp(mainStartScale, Vector3.one * subScale, t);
            subSlotImage.rectTransform.localScale = Vector3.Lerp(subStartScale, Vector3.one * mainScale, t);

            // 3. 투명도 교차 계산
            SetImageAlpha(mainSlotImage, Mathf.Lerp(1.0f, subAlpha, t));
            SetImageAlpha(subSlotImage, Mathf.Lerp(subAlpha, 1.0f, t));

            // 절반 시점(t >= 0.5)에서 슬롯 이미지 및 위치 리셋 스왑
            if (!imageSwapped && t >= 0.5f)
            {
                imageSwapped = true;
            }

            yield return null;
        }

        // 애니메이션 완료 후 원래 메인/서브 정 위치로 보정 후 이미지/스프라이트 최종 교체
        UpdateSlotVisualsInstant(newMode);
        swapCoroutine = null;
    }

    private void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }
}
