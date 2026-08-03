using UnityEngine;

public class BossChaseMove : MonoBehaviour
{
    [Header("References")]
    [Tooltip("스테이지를 자동 스크롤시키는 메인 카메라")]
    public Transform cameraTransform;
    [Tooltip("플레이어 Transform (거리 계산 기준)")]
    public Transform player;

    [Tooltip("cameraTransform이 비어있을 때만 사용되는 대체 전진 속도 (필요하면)")]
    public float cameraMoveSpeed = 5f;

    [Header("Base Position Sway (좌우 흔들림)")]
    [Tooltip("Base Position 기준 좌우로 흔들리는 폭(진폭)")]
    public float swayAmplitude = 2f;
    [Tooltip("좌우로 흔들리는 속도(주파수)")]
    public float swayFrequency = 1f;

    [Header("Vertical Bob (Y축 흔들림)")]
    public bool enableVerticalBob = true;
    [Tooltip("위아래로 흔들리는 폭(진폭)")]
    public float verticalBobHeight = 0.5f;
    [Tooltip("위아래로 흔들리는 속도")]
    public float verticalBobSpeed = 1f;

    private float lastCameraX;
    private float cycleTimer = 0f;
    private float bobTimer = 0f;
    private float baseY;      // Y 흔들림의 기준이 되는 시작 높이
    private float basePosX;   // 계속 앞으로 이동하는 기준점 (Base Position)

    void Start()
    {
        if (cameraTransform != null)
        {
            lastCameraX = cameraTransform.position.x;
        }

        baseY = transform.position.y;
        basePosX = transform.position.x;
    }

    void Update()
    {
        if (player == null) return;

        // 1. 카메라 이동량만큼 그대로 전진 (항상 카메라와 정확히 같은 속도로 이동, 더 빠르거나 느려지지 않음)
        float camDeltaX;
        if (cameraTransform != null)
        {
            camDeltaX = cameraTransform.position.x - lastCameraX;
            lastCameraX = cameraTransform.position.x;
        }
        else
        {
            // cameraTransform이 연결되지 않은 경우를 대비한 대체 속도
            camDeltaX = cameraMoveSpeed * Time.deltaTime;
        }

        // Base Position도 카메라와 동일한 속도로 계속 오른쪽으로 이동
        basePosX += camDeltaX;

        // 2. Base Position을 중심으로 사인파 형태로 좌우 흔들림
        cycleTimer += Time.deltaTime;
        float angle = cycleTimer * (2f * Mathf.PI * swayFrequency);
        float offsetX = Mathf.Sin(angle) * swayAmplitude;

        // 3. Y축 흔들림 (옵션)
        float targetY = baseY;
        if (enableVerticalBob)
        {
            bobTimer += Time.deltaTime * verticalBobSpeed;
            targetY = baseY + Mathf.Sin(bobTimer) * verticalBobHeight;
        }

        transform.position = new Vector3(basePosX + offsetX, targetY, transform.position.z);
    }
}