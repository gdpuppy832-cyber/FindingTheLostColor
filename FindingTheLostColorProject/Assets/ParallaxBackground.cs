using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [Tooltip("지연율 (0일수록 카메라와 동일, 높을수록 더 늦게 따라옴)")]
    [Range(0f, 0.95f)]
    public float smoothFactor = 0.5f;

    private Transform cameraTransform;

    void Start()
    {
        cameraTransform = Camera.main.transform;
    }

    void LateUpdate()
    {
        // 1. 카메라의 목표 위치(Z축 0 고정)
        Vector3 targetPos = new Vector3(cameraTransform.position.x, cameraTransform.position.y, 0);

        // 2. 현재 배경 위치와 카메라 위치 사이를 부드럽게 보간 (Lerp)
        // 1 - smoothFactor를 적용하여 smoothFactor가 높을수록(지연율이 높을수록) 
        // 카메라 위치에 도달하는 속도가 느려집니다.
        transform.position = Vector3.Lerp(transform.position, targetPos, 1f - smoothFactor);
    }
}