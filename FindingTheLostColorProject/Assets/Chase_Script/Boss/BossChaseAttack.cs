using UnityEngine;
using System.Collections;

public class BossChaseAttack : MonoBehaviour
{
    [Header("Bullet Prefab")]
    [Tooltip("발사할 탄환 프리팹")]
    public GameObject bulletPrefab;

    [Header("Fire Points (3개 중 무작위 발사)")]
    [Tooltip("발사 위치 1")]
    public Transform firePoint1;
    [Tooltip("발사 위치 2")]
    public Transform firePoint2;
    [Tooltip("발사 위치 3")]
    public Transform firePoint3;

    [Header("Bullet Movement")]
    [Tooltip("탄환 이동 속도")]
    public float bulletSpeed = 5f;

    [Header("Fire Timing")]
    [Tooltip("탄환을 발사하는 간격(초)")]
    public float fireInterval = 1.5f;

    [Header("Bullet Lifetime")]
    [Tooltip("탄환이 생성된 후 자동으로 사라지는 시간(초)")]
    public float bulletLifetime = 5f;

    [Header("Paint Destroy Settings")]
    [Tooltip("붓질(1번 모드)이 탄환과 겹쳐 있어야 하는 누적 시간(초). 이 시간을 채우면 탄환이 파괴됨")]
    public float requiredPaintOverlapTime = 1f;

    [Header("Boss Damage")]
    [Tooltip("모든 보스 공격이 공통으로 사용하는 공격력. 앞으로 추가될 공격도 이 값을 사용함")]
    public int attackDamage = 1;

    [Header("Ink Curtain Settings")]
    [Tooltip("먹물 장막 프리팹")]
    public GameObject inkCurtainPrefab;
    [Tooltip("먹물 장막이 생성될 위치")]
    public Transform inkCurtainSpawnPoint;
    [Tooltip("먹물 장막 이동 속도 (탄환처럼 오른쪽 -> 왼쪽으로 이동)")]
    public float inkCurtainSpeed = 4f;
    [Tooltip("먹물 장막이 자동으로 사라지기까지의 시간(초)")]
    public float inkCurtainLifetime = 8f;
    [Tooltip("탄환을 몇 발 발사할 때마다 먹물 장막으로 전환할지")]
    public int bulletsBeforeInkCurtain = 10;
    [Tooltip("붓질(1번 모드)이 먹물 장막과 겹쳐 있어야 하는 누적 시간(초). 탄환의 requiredPaintOverlapTime과 별개로 장막 전용 값")]
    public float inkCurtainRequiredPaintOverlapTime = 3f;

    private int bulletFireCount = 0; // 탄환 발사 카운트 (bulletsBeforeInkCurtain에 도달하면 장막 발동)

    private Coroutine fireLoopCoroutine;

    void Start()
    {
        fireLoopCoroutine = StartCoroutine(FireLoop());
    }

    private IEnumerator FireLoop()
    {
        while (true)
        {
            if (bulletFireCount >= bulletsBeforeInkCurtain)
            {
                // 탄환을 bulletsBeforeInkCurtain발 발사했으면 이번 차례는 먹물 장막으로 대체
                FireInkCurtain();
                bulletFireCount = 0;
            }
            else
            {
                FireBullet();
                bulletFireCount++;
            }

            yield return new WaitForSeconds(fireInterval);
        }
    }

    private void FireBullet()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("[BossChaseAttack] bulletPrefab이 비어있어 발사할 수 없습니다.");
            return;
        }

        Transform chosenPoint = GetRandomFirePoint();
        if (chosenPoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] 유효한 발사 위치가 없어 발사할 수 없습니다.");
            return;
        }

        GameObject bulletObj = Instantiate(bulletPrefab, chosenPoint.position, Quaternion.identity);

        Rigidbody2D rb = bulletObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = bulletObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.left * bulletSpeed;

        BossChaseBullet bullet = bulletObj.GetComponent<BossChaseBullet>();
        if (bullet == null) bullet = bulletObj.AddComponent<BossChaseBullet>();
        bullet.Initialize(attackDamage, bulletLifetime, requiredPaintOverlapTime);
    }
    // 먹물 장막(Ink Curtain) 공격 전용 메서드. 탄환과 동일하게 오른쪽 -> 왼쪽으로 이동하며,
    // attackDamage/requiredPaintOverlapTime을 새로 만들지 않고 그대로 재사용함
    private void FireInkCurtain()
    {
        if (inkCurtainPrefab == null)
        {
            Debug.LogWarning("[BossChaseAttack] inkCurtainPrefab이 비어있어 먹물 장막을 발사할 수 없습니다.");
            return;
        }
        if (inkCurtainSpawnPoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] inkCurtainSpawnPoint가 비어있어 먹물 장막을 발사할 수 없습니다.");
            return;
        }

        GameObject curtainObj = Instantiate(inkCurtainPrefab, inkCurtainSpawnPoint.position, Quaternion.identity);

        Rigidbody2D rb = curtainObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = curtainObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.left * inkCurtainSpeed;

        BossInkCurtain curtain = curtainObj.GetComponent<BossInkCurtain>();
        if (curtain == null) curtain = curtainObj.AddComponent<BossInkCurtain>();
        curtain.Initialize(attackDamage, inkCurtainLifetime, inkCurtainRequiredPaintOverlapTime);
    }

    private Transform GetRandomFirePoint()
    {
        System.Collections.Generic.List<Transform> validPoints = new System.Collections.Generic.List<Transform>();
        if (firePoint1 != null) validPoints.Add(firePoint1);
        if (firePoint2 != null) validPoints.Add(firePoint2);
        if (firePoint3 != null) validPoints.Add(firePoint3);

        if (validPoints.Count == 0) return null;

        return validPoints[Random.Range(0, validPoints.Count)];
    }

    void OnDisable()
    {
        if (fireLoopCoroutine != null)
        {
            StopCoroutine(fireLoopCoroutine);
            fireLoopCoroutine = null;
        }
    }
}