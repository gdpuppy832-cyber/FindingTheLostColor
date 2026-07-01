// After
using UnityEngine;
public class Projectile : MonoBehaviour
{
    public LayerMask targetLayer;
    public float lifetime = 3f;
    public float blinkTime = 1f;      // 소멸 전 반짝이는 시간
    public float blinkInterval = 0.1f; // 반짝임 간격

    SpriteRenderer sr;

    // After
    Camera mainCam;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>();

        mainCam = Camera.main;

        Destroy(gameObject, lifetime);
        Invoke(nameof(StartBlink), Mathf.Max(0f, lifetime - blinkTime));
    }

    void Update()
    {
        if (mainCam == null) return;

        Vector3 viewportPos = mainCam.WorldToViewportPoint(transform.position);
        bool onScreen = viewportPos.z > 0f &&
                         viewportPos.x > 0f && viewportPos.x < 1f &&
                         viewportPos.y > 0f && viewportPos.y < 1f;

        if (!onScreen)
            Destroy(gameObject);
    }

    void StartBlink()
    {
        if (sr != null)
            StartCoroutine(BlinkRoutine());
    }

    System.Collections.IEnumerator BlinkRoutine()
    {
        while (true)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & targetLayer) != 0)
        {
            Debug.Log("투사체 적중: " + other.name);
            Destroy(gameObject);
        }
    }
}