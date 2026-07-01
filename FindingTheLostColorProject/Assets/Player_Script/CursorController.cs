using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class CursorController : MonoBehaviour
{
    public Transform player; // 플레이어 오브젝트
    public TrailRenderer trail; // TrailRenderer 오브젝트

    [Header("Cursor Settings")]
    public List<Texture2D> cursorTextures;
    public Vector2 hotSpot = new Vector2(16, 16);

    [Header("Distance Thresholds")]
    public float mediumDistance = 3.8f;
    public float intenseDistance = 6.7f;

    [Header("Trail Settings")]
    public float trailWidth = 0.15f; // 트레일 선 굵기

    [Header("Particle Settings")]
    [Tooltip("붓질 시 생성할 파티클 시스템 (옵션)")]
    public ParticleSystem paintParticles;

    private int currentCursorIndex = -1;
    private GaugeController gaugeController; // 물감 게이지 스크립트 참조
    private PlayerHealth playerHealth; // 플레이어 체력 스크립트 참조

    void Start()
    {
        if (trail == null) trail = GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.emitting = false;
            trail.widthMultiplier = trailWidth;
        }

        if (paintParticles != null)
        {
            var emission = paintParticles.emission;
            emission.enabled = false;
        }

        // 씬에서 게이지 및 체력 컨트롤러 검색 및 참조
        gaugeController = FindFirstObjectByType<GaugeController>();
        playerHealth = FindFirstObjectByType<PlayerHealth>();
    }

    void Update()
    {
        // 1. 마우스 위치 이동
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        transform.position = mouseWorldPos;

        // 2. 거리 측정
        float distance = Vector2.Distance(player.position, transform.position);

        // 3. 거리 단계 구분 (0:근, 1:중, 2:원)
        int nextIndex = (distance < mediumDistance) ? 0 : (distance < intenseDistance) ? 1 : 2;

        // 4. 인덱스 변경 시 커서 변경 및 트레일 스타일 업데이트
        if (nextIndex != currentCursorIndex)
        {
            if (cursorTextures != null && nextIndex < cursorTextures.Count)
            {
                Cursor.SetCursor(cursorTextures[nextIndex], hotSpot, CursorMode.Auto);
            }
            UpdateTrailStyle(nextIndex);
            currentCursorIndex = nextIndex;
        }

        // 5. 트레일 그리기 조건 설정 (좌클릭 상태이면서 물감 잔량이 최소 설정치 이상 존재하며 재클릭 대기 중이 아닐 때)
        bool hasPaint = false;
        bool needsReclick = false;
        if (gaugeController != null)
        {
            hasPaint = gaugeController.currentPaint >= gaugeController.minPaintToDraw; // 최소 기준치 이상
            needsReclick = gaugeController.NeedsReclick;
        }

        // 사망 여부 및 피격 시 그리기 끊김(0.01초) 상태 감지
        bool isDead = playerHealth != null && playerHealth.IsDead;
        bool isDrawBlocked = playerHealth != null && playerHealth.IsDrawBlocked;

        bool isLeftClickHeld = false;
#if ENABLE_INPUT_SYSTEM
        // New Input System
        if (Mouse.current != null)
        {
            isLeftClickHeld = Mouse.current.leftButton.isPressed;
        }
#else
        // Legacy Input Manager
        isLeftClickHeld = Input.GetMouseButton(0);
#endif

        // 그리기 조건 성립 여부 (재클릭 대기 중이 아닐 때만 가능)
        bool canDraw = isLeftClickHeld && hasPaint && !needsReclick && !isDead && !isDrawBlocked;

        // 트레일 방출 제어
        if (trail != null)
        {
            trail.emitting = canDraw;
        }

        // 파티클 방출 제어 (인스펙터에 파티클 시스템이 등록되어 있는 경우)
        if (paintParticles != null)
        {
            var emission = paintParticles.emission;
            emission.enabled = canDraw;
        }
    }

    void UpdateTrailStyle(int index)
    {
        if (trail == null) return;

        // 거리 단계에 따른 투명도 설정
        float[] alphas = { 1.0f, 0.6f, 0.3f };
        if (index >= alphas.Length) return;
        float targetAlpha = alphas[index];

        // 거리 단계에 따른 색상 강도(Intensity) 설정 (가까움: 100%, 중간: 75%, 멈: 50%)
        float[] colorIntensities = { 1.0f, 0.75f, 0.5f };
        float intensity = colorIntensities[index];

        // 그라데이션 설정
        Gradient gradient = new Gradient();

        // 색상 키 설정 (무지개색 그라데이션에 강도 보정 적용 - 색이 연해지는 파스텔 효과)
        GradientColorKey[] colorKeys = new GradientColorKey[7];
        colorKeys[0] = new GradientColorKey(GetAdjustedColor(Color.red, intensity), 0.0f);
        colorKeys[1] = new GradientColorKey(GetAdjustedColor(new Color(1f, 0.5f, 0f), intensity), 0.16f); // 주황
        colorKeys[2] = new GradientColorKey(GetAdjustedColor(Color.yellow, intensity), 0.33f);
        colorKeys[3] = new GradientColorKey(GetAdjustedColor(Color.green, intensity), 0.5f);
        colorKeys[4] = new GradientColorKey(GetAdjustedColor(Color.blue, intensity), 0.66f);
        colorKeys[5] = new GradientColorKey(GetAdjustedColor(new Color(0.29f, 0f, 0.51f), intensity), 0.83f); // 남색
        colorKeys[6] = new GradientColorKey(GetAdjustedColor(new Color(0.56f, 0f, 1f), intensity), 1.0f); // 보라

        // 투명도 키 설정 (뒤로 갈수록 투명해짐)
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(targetAlpha, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(0.0f, 1.0f); // 끝부분 투명

        // 그라데이션 키 설정
        gradient.SetKeys(colorKeys, alphaKeys);
        trail.colorGradient = gradient;
    }

    /// <summary>
    /// 기존 색상에 흰색을 섞어 색의 농도(Intensity)를 연하게 조절하는 함수 (파스텔톤 연출)
    /// </summary>
    private Color GetAdjustedColor(Color original, float intensity)
    {
        // intensity가 1.0f이면 100% 기존 색상
        // intensity가 0.75f이면 75% 기존 색상 + 25% 흰색
        // intensity가 0.50f이면 50% 기존 색상 + 50% 흰색
        return Color.Lerp(original, Color.white, 1f - intensity);
    }
}