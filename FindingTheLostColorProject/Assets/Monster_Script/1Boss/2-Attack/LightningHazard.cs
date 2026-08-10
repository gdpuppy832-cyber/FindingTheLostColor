using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// 번개: 먹구름 위치에서 지면으로 뻗어나가는 번개 스크립트.
// 이지 모드: 0.4초 소멸 / 하드 모드: 5.0초 동안 필드 잔류 및 0.5초 틱 감전 피해 지속 적용.
public class LightningHazard : MonoBehaviour
{
    public float damage = 1f;
    public float lifetime = 0.4f;           // 최초 내리침 대기시간 (0.4초)
    public float easyModeDuration = 0.4f;   // 이지 모드 유지시간 (0.4초)
    public float hardModeDuration = 5.0f;   // 하드 모드 잔류 유지시간 (5.0초)
    public float residualDuration = 5.0f;   // 하드 모드 잔류 유지시간 (5.0초)
    public float damageTickInterval = 0.5f; // 지속 감전 피격 쿨타임 (0.5초)

    public bool isEasyMode = false;

    private SpriteRenderer sr;
    private Animator anim;
    private Collider2D col;
    private Dictionary<PlayerHealth, float> lastHitTimes = new Dictionary<PlayerHealth, float>();
    private PolygonCollider2D polyCol;
    private Sprite lastSprite;
    public void Init(Vector3 fromPos, Vector3 toPos, float length)
    {
        transform.rotation = Quaternion.identity;
        transform.position = toPos;
    }

    public void Init(Vector3 fromPos, Vector3 toPos, bool isEasy)
    {
        transform.rotation = Quaternion.identity;
        transform.position = toPos;
        this.isEasyMode = isEasy;
    }

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = GetComponentInChildren<SpriteRenderer>();

        anim = GetComponent<Animator>();
        if (anim == null) anim = GetComponentInChildren<Animator>();

        col = GetComponent<Collider2D>();
        if (col == null) col = GetComponentInChildren<Collider2D>();

        BossAttack boss = FindFirstObjectByType<BossAttack>();
        if (boss != null)
        {
            isEasyMode = boss.isEasyMode;
        }

        StopAllCoroutines();
        StartCoroutine(LightningLifecycleRoutine());

        polyCol = col as PolygonCollider2D;
    }
    void Update()
    {
        if (sr != null && polyCol != null && sr.sprite != null && sr.sprite != lastSprite)
        {
            lastSprite = sr.sprite;
            polyCol.pathCount = sr.sprite.GetPhysicsShapeCount();

            List<Vector2> path = new List<Vector2>();
            for (int i = 0; i < polyCol.pathCount; i++)
            {
                path.Clear();
                sr.sprite.GetPhysicsShape(i, path);
                polyCol.SetPath(i, path.ToArray());
            }
        }
    }

    private IEnumerator LightningLifecycleRoutine()
    {
        if (isEasyMode)
        {
            yield return new WaitForSeconds(easyModeDuration);
            Destroy(gameObject);
        }
        else
        {
            StartCoroutine(ResidualLightningRoutine());
        }
    }

    private IEnumerator ResidualLightningRoutine()
    {
        // 1. 번개 최초 타격 발동 (0.4초)
        yield return new WaitForSeconds(lifetime);


        // 2. 하드 모드 잔류 번개: 총 5초 중 첫 4초는 100% 진한 선명도(Alpha 1.0f) 짱짱함 유지, 마지막 1초 동안 페이드아웃
        float elapsed = 0f;
        float holdFullDuration = Mathf.Max(0f, residualDuration - 1.0f); // 4초간 100% 유지
        float fadeOutDuration = 1.0f; // 마지막 1초간 페이드아웃

        Color startColor = sr != null ? sr.color : Color.white;
        startColor.a = 1f;

        while (elapsed < residualDuration)
        {
            elapsed += Time.deltaTime;

            // [핵심] 5초 잔류하는 동안 LIGHTNING 애니메이션이 1회 끝나면 0초 시점으로 돌려 끊김 없이 무한 반복 재생!
            if (anim != null)
            {
                anim.enabled = true;
                AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.normalizedTime >= 0.95f)
                {
                    anim.Play("LIGHTNING", 0, 0f);
                }
            }

            if (sr != null)
            {
                if (elapsed <= holdFullDuration)
                {
                    // 첫 4초 동안은 선명도 100% 짱짱하게 선명함 유지
                    Color c = startColor;
                    c.a = 1f;
                    sr.color = c;
                }
                else
                {
                    // 마지막 1초 동안 스르륵 투명해짐 (1.0 -> 0.0)
                    float fadeRatio = (elapsed - holdFullDuration) / fadeOutDuration;
                    Color c = startColor;
                    c.a = Mathf.Lerp(1f, 0f, fadeRatio);
                    sr.color = c;
                }
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    void OnTriggerStay2D(Collider2D other) { TryDamage(other.gameObject); }
    void OnTriggerEnter2D(Collider2D other) { TryDamage(other.gameObject); }
    void OnCollisionStay2D(Collision2D collision) { TryDamage(collision.gameObject); }
    void OnCollisionEnter2D(Collision2D collision) { TryDamage(collision.gameObject); }

    void TryDamage(GameObject obj)
    {
        PlayerHealth player = obj.GetComponent<PlayerHealth>();
        if (player == null) player = obj.GetComponentInParent<PlayerHealth>();

        if (player != null)
        {
            if (!lastHitTimes.ContainsKey(player) || Time.time >= lastHitTimes[player] + damageTickInterval)
            {
                player.TakeDamage(damage);
                lastHitTimes[player] = Time.time;
            }
        }
    }
}