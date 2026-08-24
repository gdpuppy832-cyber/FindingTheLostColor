using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [Tooltip("지연율 (0일수록 카메라와 동일, 높을수록 더 늦게 따라옴)")]
    [Range(0f, 0.95f)]
    public float smoothFactor = 0.5f;

    public static bool lockToCamera = false; // 컷씬 등 고속 카메라 이동 시 1:1 동기화용 플래그

    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        // 1. 카메라의 목표 위치(Z축 0 고정)
        Vector3 targetPos = new Vector3(cameraTransform.position.x, cameraTransform.position.y, 0);

        if (lockToCamera)
        {
            // 컷씬 작동 시에는 Lerp 연산 없이 카메라를 1:1로 즉시 밀착 추적
            transform.position = targetPos;
        }
        else
        {
            // 2. 현재 배경 위치와 카메라 위치 사이를 부드럽게 보간 (Lerp)
            // 1 - smoothFactor를 적용하여 smoothFactor가 높을수록(지연율이 높을수록) 
            // 카메라 위치에 도달하는 속도가 느려집니다.
            transform.position = Vector3.Lerp(transform.position, targetPos, 1f - smoothFactor);
        }
    }
}