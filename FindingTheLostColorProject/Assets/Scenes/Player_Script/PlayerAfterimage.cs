using UnityEngine;

public class PlayerAfterimage : MonoBehaviour
{
    private SpriteRenderer sr;
    private float alpha;
    private float fadeSpeed;
    private Color color;

    /// <summary>
    /// 외부에서 플레이어의 현재 시각적 상태(스프라이트, 스케일 등)를 수신받아 잔상을 조립하는 초기화 함수
    /// </summary>
    public void Setup(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale, Color startColor, float fadeSpeedValue, int sortingOrder, string sortingLayerName)
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();

        // 원본과 동일한 이미지 및 트랜스폼 상태 부여
        sr.sprite = sprite;
        transform.position = position;
        transform.rotation = rotation;
        transform.localScale = scale;
        
        this.color = startColor;
        this.fadeSpeed = fadeSpeedValue;
        
        sr.color = color;
        sr.sortingOrder = sortingOrder - 1; // 플레이어 본체보다 살짝 뒤에 그려지도록 정밀 정렬
        sr.sortingLayerName = sortingLayerName;
        
        alpha = startColor.a;
    }

    void Update()
    {
        // 매 프레임 지정된 속도로 투명도를 깎아나감 (Fade Out)
        alpha -= fadeSpeed * Time.deltaTime;
        color.a = alpha;
        sr.color = color;

        // 완전히 투명해지면 스스로 메모리에서 소멸 (가비지 누수 원천 차단)
        if (alpha <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
