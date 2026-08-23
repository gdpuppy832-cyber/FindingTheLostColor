using UnityEngine;

public class BossChaseMove : MonoBehaviour
{
    [Header("References")]
    [Tooltip("���������� �ڵ� ��ũ�ѽ�Ű�� ���� ī�޶�")]
    public Transform cameraTransform;
    [Tooltip("�÷��̾� Transform (�Ÿ� ��� ����)")]
    public Transform player;

    [Tooltip("cameraTransform�� ������� ���� ���Ǵ� ��ü ���� �ӵ� (�ʿ��ϸ�)")]
    public float cameraMoveSpeed = 5f;

    [Header("Base Position Sway (�¿� ��鸲)")]
    [Tooltip("Base Position ���� �¿�� ��鸮�� ��(����)")]
    public float swayAmplitude = 2f;
    [Tooltip("�¿�� ��鸮�� �ӵ�(���ļ�)")]
    public float swayFrequency = 1f;

    [Header("Vertical Bob (Y�� ��鸲)")]
    public bool enableVerticalBob = true;
    [Tooltip("���Ʒ��� ��鸮�� ��(����)")]
    public float verticalBobHeight = 0.5f;
    [Tooltip("���Ʒ��� ��鸮�� �ӵ�")]
    public float verticalBobSpeed = 1f;

    private float lastCameraX;
    private float cycleTimer = 0f;
    private float bobTimer = 0f;
    private float baseY;      // Y ��鸲�� ������ �Ǵ� ���� ����
    private float basePosX;   // ��� ������ �̵��ϴ� ������ (Base Position)
    [Header("씬 시작 컷씬 잠금")]
    [Tooltip("true면 이 보스의 좌우/상하 흔들림 이동이 완전히 정지됩니다.")]
    public bool movementLocked = true;
    void Start()
    {
        if (cameraTransform != null)
        {
            lastCameraX = cameraTransform.position.x;
        }

        baseY = transform.position.y;
        basePosX = transform.position.x;
    }
    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;

        // 잠금 해제 시, 잠겨있던 동안 카메라가 이동한 거리만큼 한꺼번에 훅 튀는 것을 방지하기 위해
        // 카메라 기준점을 현재 위치로 다시 맞춰줌
        if (!locked && cameraTransform != null)
        {
            lastCameraX = cameraTransform.position.x;
        }
    }
    void Update()
    {
        if (movementLocked) return;
        if (player == null) return;

        // 1. ī�޶� �̵�����ŭ �״�� ���� (�׻� ī�޶�� ��Ȯ�� ���� �ӵ��� �̵�, �� �����ų� �������� ����)
        float camDeltaX;
        if (cameraTransform != null)
        {
            camDeltaX = cameraTransform.position.x - lastCameraX;
            lastCameraX = cameraTransform.position.x;
        }
        else
        {
            // cameraTransform�� ������� ���� ��츦 ����� ��ü �ӵ�
            camDeltaX = cameraMoveSpeed * Time.deltaTime;
        }

        // Base Position�� ī�޶�� ������ �ӵ��� ��� ���������� �̵�
        basePosX += camDeltaX;

        // 2. Base Position�� �߽����� ������ ���·� �¿� ��鸲
        cycleTimer += Time.deltaTime;
        float angle = cycleTimer * (2f * Mathf.PI * swayFrequency);
        float offsetX = Mathf.Sin(angle) * swayAmplitude;

        // 3. Y�� ��鸲 (�ɼ�)
        float targetY = baseY;
        if (enableVerticalBob)
        {
            bobTimer += Time.deltaTime * verticalBobSpeed;
            targetY = baseY + Mathf.Sin(bobTimer) * verticalBobHeight;
        }

        transform.position = new Vector3(basePosX + offsetX, targetY, transform.position.z);
    }
}