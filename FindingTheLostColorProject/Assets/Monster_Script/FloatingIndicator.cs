using UnityEngine;

public class FloatingIndicator : MonoBehaviour
{
    [Header("Floating Settings")]
    [Tooltip("둥둥 떠다니는 속도 (높을수록 빠르게 진동)")]
    public float floatSpeed = 3.0f;

    [Tooltip("둥둥 떠다니는 위아래 진폭 (높이)")]
    public float floatAmplitude = 0.2f;

    [Tooltip("좌우 미세 흔들림 진폭 (기본값: 0.05)")]
    public float swayAmplitude = 0.05f;
    
    private Vector3 startLocalPos;
    private float randomOffset;

    void Start()
    {
        // 생성된 시점의 로컬 좌표를 기준점으로 저장
        startLocalPos = transform.localPosition;
        
        // 동기화 칼군무 방지 랜덤값
        randomOffset = Random.Range(0f, 100f);
        
        // ⚠️ [예외 방어] 프리팹 원본의 로컬 스케일이 0이거나 깨져 있는 경우, 최소 강도로 스케일 복원
        if (transform.localScale.magnitude < 0.01f)
        {
            transform.localScale = Vector3.one;
        }

        // ==========================================
        // [자가 진단] 이미지 렌더러 및 리소스 장착 점검
        // ==========================================
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        if (sr == null)
        {
            Debug.LogError($"[FloatingIndicator] '{gameObject.name}' 본체 혹은 자식 오브젝트에 'Sprite Renderer' 컴포넌트가 없습니다! 이미지를 그릴 렌더러가 누락된 상태입니다.");
        }
        else if (sr.sprite == null)
        {
            Debug.LogError($"[FloatingIndicator] '{gameObject.name}'의 Sprite Renderer 안에 등록된 'Sprite' 이미지 리소스가 없습니다 (None)! 프리팹 내부 스프라이트 슬롯에 이미지를 드래그해서 할당해 주셔야 눈에 보입니다.");
        }
        else
        {
            Debug.Log($"[FloatingIndicator] 렌더러 감지 성공! 이미지명: {sr.sprite.name}, 소팅 레이어: {sr.sortingLayerName}, 오더: {sr.sortingOrder}, 알파: {sr.color.a}, 활성화여부: {sr.enabled}");
        }

        // 부모에 의한 찌그러짐 진단
        Debug.Log($"[FloatingIndicator] '{gameObject.name}' 작동 시작! 로컬 스케일: {transform.localScale}, 월드 스케일: {transform.lossyScale}");
    }

    void Update()
    {
        // 1. 위아래로 부드럽게 사인파 운동
        float newY = startLocalPos.y + Mathf.Sin((Time.time * floatSpeed) + randomOffset) * floatAmplitude;
        
        // 2. 좌우로 미세한 흔들림 추가
        float newX = startLocalPos.x + Mathf.Cos((Time.time * (floatSpeed * 0.5f)) + randomOffset) * swayAmplitude;
        
        transform.localPosition = new Vector3(newX, newY, startLocalPos.z);
    }
}
