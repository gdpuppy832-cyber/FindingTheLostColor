using UnityEngine;

public class UIFloating : MonoBehaviour
{
    [Header("µÕµÕ ¶ß´Â ¼³Á¤")]
    public float height = 10f;
    public float speed = 2f;

    private RectTransform rectTransform;
    private Vector2 startPosition;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosition = rectTransform.anchoredPosition;
    }

    private void Update()
    {
        float y = Mathf.Sin(Time.time * speed) * height;

        rectTransform.anchoredPosition = startPosition + Vector2.up * y;
    }
}