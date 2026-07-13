using UnityEngine;
using System.Collections;

public class PortalMonsterLinger : MonoBehaviour
{
    private Vector3 spawnPortalPosition;
    private NormalMonster normalMonster;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D rb;
    private bool isLingerStarted = false;

    public void Setup(Vector3 portalPos)
    {
        spawnPortalPosition = portalPos;
    }

    void Start()
    {
        normalMonster = GetComponent<NormalMonster>();
        if (normalMonster == null) normalMonster = GetComponentInParent<NormalMonster>();

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = GetComponentInChildren<Rigidbody2D>();
    }

    void Update()
    {
        // 몬스터가 정화되었을 때 단 한 번 연출을 트리거합니다.
        if (normalMonster != null && normalMonster.IsPurified && !isLingerStarted)
        {
            isLingerStarted = true;
            StartCoroutine(LingerSequence());
        }
    }

    private IEnumerator LingerSequence()
    {
        // 1. 기존 이동 AI 및 애니메이터 매개변수 정지
        MonoBehaviour[] behaviors = GetComponents<MonoBehaviour>();
        foreach (var behavior in behaviors)
        {
            // 이 스크립트 자신과 NormalMonster를 제외한 이동/공격 스크립트들을 일시 정지시킵니다.
            if (behavior != this && behavior != normalMonster)
            {
                behavior.enabled = false;
            }
        }

        Animator anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.enabled = false; // 정화 컷씬 느낌을 위해 애니메이션 정지
        }

        // 2. 한 번 위로 튀어오름 (피격되어 솟구치는 연출)
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.gravityScale = 1.8f; // 약간의 중력으로 포물선 유도
            // Y축 추진과 동시에 살짝 좌/우로 튀어오르게 랜덤 튕김 가미
            float randomX = Random.Range(-1.5f, 1.5f);
            rb.linearVelocity = new Vector2(randomX, 6.0f); 
        }

        // 튀어올랐다가 정점에 도달하는 약 0.4초 대기
        yield return new WaitForSeconds(0.4f);

        // 3. 포탈 쪽으로 빨려 들어가는 이동을 위해 물리 시뮬레이션 해제
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false; // 다른 물체와의 물리 간섭 완전 차단
        }

        // 4. 생성된 포탈 위치를 향해 빠르게 이동하며 1초간 투명화
        float duration = 0.8f;
        float elapsed = 0f;

        Vector3 startPos = transform.position;
        Color startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        // 콜라이더를 꺼서 플레이어 등과의 불필요한 충돌 방지
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
        foreach (var col in colliders)
        {
            if (col != null) col.enabled = false;
        }

        while (elapsed < duration)
        {
            if (spriteRenderer == null) yield break; // 예외 방지

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 포탈 방향으로 부드럽고 묵직하게 보간 이동 (Lerp)
            transform.position = Vector3.Lerp(startPos, spawnPortalPosition, t);

            // 서서히 투명해지기 (Alpha 페이드아웃)
            if (spriteRenderer != null)
            {
                Color c = startColor;
                c.a = Mathf.Lerp(startColor.a, 0f, t);
                spriteRenderer.color = c;
            }

            yield return null;
        }

        // 포탈에 도착했으므로 영구 소멸
        Destroy(gameObject);
    }
}
