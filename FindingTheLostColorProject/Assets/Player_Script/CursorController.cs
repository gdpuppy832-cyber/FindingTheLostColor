using UnityEngine;
using System.Collections.Generic;

public class CursorController : MonoBehaviour
{
    public Transform player; // 플레이어 오브젝트
    public TrailRenderer trail; // TrailRenderer 컴포넌트

    [Header("Cursor Settings")]
    public List<Texture2D> cursorTextures;
    public Vector2 hotSpot = new Vector2(16, 16);

    [Header("Distance Thresholds")]
    public float mediumDistance = 3.8f;
    public float intenseDistance = 6.7f;

    [Header("Trail Settings")]
    public float trailWidth = 0.15f; // 트레일의 고정된 작은 굵기 (원하는 대로 인스펙터에서 수정 가능)

    private int currentCursorIndex = -1;

    void Start()
    {
        if (trail == null) trail = GetComponent<TrailRenderer>();

        trail.emitting = false;

        // 시작할 때 트레일 굵기를 작게 고정
        trail.widthMultiplier = trailWidth;
    }

    void Update()
    {
        // 1. 마우스 위치 이동
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0;
        transform.position = mouseWorldPos;

        // 2. 거리 계산
        float distance = Vector2.Distance(player.position, transform.position);

        // 3. 거리별 인덱스 결정 (0:가까움, 1:보통, 2:멈)
        int nextIndex = (distance < mediumDistance) ? 0 : (distance < intenseDistance) ? 1 : 2;

        // 4. 상태 변경 시에만 실행 (최적화)
        if (nextIndex != currentCursorIndex)
        {
            Cursor.SetCursor(cursorTextures[nextIndex], hotSpot, CursorMode.Auto);
            UpdateTrailStyle(nextIndex);
            currentCursorIndex = nextIndex;
        }

        // 5. 그리기 상태 설정
        trail.emitting = Input.GetMouseButton(1);
    }

    void UpdateTrailStyle(int index)
    {
        // 거리별 투명도(Alpha) 설정 (가까울수록 1.0, 중간 0.5, 멀수록 0.1)
        float[] alphas = { 1.0f, 0.6f, 0.3f };
        float targetAlpha = alphas[index];

        // 코드에서 직접 완벽한 한 방향 빨주노초파남보(ROYGBIV) 그라데이션 생성
        Gradient gradient = new Gradient();

        // 색상 키 설정 (0%에서 100%까지 순서대로)
        GradientColorKey[] colorKeys = new GradientColorKey[7];
        colorKeys[0] = new GradientColorKey(Color.red, 0.0f);
        colorKeys[1] = new GradientColorKey(new Color(1f, 0.5f, 0f), 0.16f); // 주황
        colorKeys[2] = new GradientColorKey(Color.yellow, 0.33f);
        colorKeys[3] = new GradientColorKey(Color.green, 0.5f);
        colorKeys[4] = new GradientColorKey(Color.blue, 0.66f);
        colorKeys[5] = new GradientColorKey(new Color(0.29f, 0f, 0.51f), 0.83f); // 남색
        colorKeys[6] = new GradientColorKey(new Color(0.56f, 0f, 1f), 1.0f); // 보라

        // 투명도 키 설정 (트레일 전체에 동일한 타겟 투명도 적용, 끝부분은 자연스럽게 사라지도록 0 처리)
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(targetAlpha, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(0.0f, 1.0f); // 꼬리 부분은 투명해지도록 설정

        // 트레일에 그라데이션 적용
        gradient.SetKeys(colorKeys, alphaKeys);
        trail.colorGradient = gradient;

        // 굵기 고정 유지 방어 코드
        trail.widthMultiplier = trailWidth;
    }
}