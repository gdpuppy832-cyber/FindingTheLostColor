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

    [Header("Curve Bullet Settings")]
    [Tooltip("변화구 탄환 프리팹 (일반 탄환과 별개)")]
    public GameObject curveBulletPrefab;
    [Tooltip("변화구 탄환이 항상 생성되는 위치 (일반 탄환의 3개 발사 위치와 별개)")]
    public Transform curveFirePoint;
    [Tooltip("변화구 탄환이 방향을 틀어 향할 목표 위치")]
    public Transform curveTargetPoint;
    [Tooltip("생성 후 몇 초가 지나야 curveTargetPoint 방향으로 꺾이기 시작할지")]
    public float curveStartTime = 0.8f;
    [Tooltip("방향을 튼 이후 curveTargetPoint를 향해 이동하는 속도")]
    public float curveMoveSpeed = 6f;
    [Tooltip("일반 탄환을 몇 발 발사할 때마다 그 자리를 변화구 탄환으로 대체할지 (먹물 장막 카운트와 독립적으로 관리됨)")]
    public int bulletsBeforeCurveBullet = 15;

    private int curveBulletFireCount = 0; // 변화구 전환 카운트 (bulletFireCount와 완전히 독립적으로 관리)

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

    [Header("Laser Settings")]
    [Tooltip("레이저 텔레그래프 프리팹")]
    public GameObject laserTelegraphPrefab;
    [Tooltip("레이저 본체 프리팹 (충돌 판정은 프리팹 자체에 있는 Collider2D를 그대로 사용)")]
    public GameObject laserPrefab;
    [Tooltip("레이저가 고정 발사되는 위치 (플레이어를 추적하지 않고 항상 이 위치에서 발사)")]
    public Transform laserFirePoint;
    [Tooltip("추격 시작 후 레이저를 처음 사용할 수 있게 되기까지의 시간(초)")]
    public float laserUnlockTime = 20f;
    [Tooltip("레이저 종료 후부터 다음 레이저까지의 쿨타임(초)")]
    public float laserCooldown = 10f;
    [Tooltip("레이저 텔레그래프 유지 시간(초)")]
    public float laserTelegraphDuration = 1.5f;
    [Tooltip("레이저 텔레그래프 깜빡임 간격(초)")]
    public float laserBlinkInterval = 0.5f;
    [Tooltip("레이저 본체가 유지되는 시간(초)")]
    public float laserDuration = 0.5f;

    private float attackStartTime; // 추격(공격 루프) 시작 시각 - laserUnlockTime 계산 기준
    private Coroutine laserLoopCoroutine;

    private int bulletFireCount = 0; // 탄환 발사 카운트 (bulletsBeforeInkCurtain에 도달하면 장막 발동)

    private Coroutine fireLoopCoroutine;

    void Start()
    {
        fireLoopCoroutine = StartCoroutine(FireLoop());

        // 레이저 언락/쿨타임 계산 기준 시각 (추격 시작 시점으로 간주)
        attackStartTime = Time.time;
        laserLoopCoroutine = StartCoroutine(LaserLoop());
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
                // ★ 먹물 장막 카운트(bulletFireCount)와 완전히 독립적인 변화구 카운트.
                //   일반 탄환이 나가야 할 차례 중, 15번째마다 변화구로 대체됨.
                curveBulletFireCount++;
                if (curveBulletFireCount >= bulletsBeforeCurveBullet)
                {
                    FireCurveBullet();
                    curveBulletFireCount = 0;
                }
                else
                {
                    FireBullet();
                }

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

    private void FireCurveBullet()
    {
        if (curveBulletPrefab == null)
        {
            Debug.LogWarning("[BossChaseAttack] curveBulletPrefab이 비어있어 변화구를 발사할 수 없습니다.");
            return;
        }
        if (curveFirePoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] curveFirePoint가 비어있어 변화구를 발사할 수 없습니다.");
            return;
        }
        if (curveTargetPoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] curveTargetPoint가 비어있어 변화구를 발사할 수 없습니다.");
            return;
        }

        GameObject bulletObj = Instantiate(curveBulletPrefab, curveFirePoint.position, Quaternion.identity);

        Rigidbody2D rb = bulletObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = bulletObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.left * bulletSpeed; // 생성 직후엔 일반 탄환과 동일하게 왼쪽으로 직진

        // 기존 BossChaseBullet을 그대로 재사용 -> 정화(붓질)/수명/데미지 시스템이 일반 탄환과 완전히 동일
        BossChaseBullet bullet = bulletObj.GetComponent<BossChaseBullet>();
        if (bullet == null) bullet = bulletObj.AddComponent<BossChaseBullet>();
        bullet.Initialize(attackDamage, bulletLifetime, requiredPaintOverlapTime);

        // 곡선 이동만 별도 컴포넌트로 추가 (BossChaseBullet 로직에는 전혀 관여하지 않음)
        BossChaseCurveBullet curveMotion = bulletObj.GetComponent<BossChaseCurveBullet>();
        if (curveMotion == null) curveMotion = bulletObj.AddComponent<BossChaseCurveBullet>();
        curveMotion.SetCurveParams(curveTargetPoint.position, curveStartTime, curveMoveSpeed);
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

    // 레이저 사용 가능 시점(laserUnlockTime)을 기다린 뒤, 레이저 -> 쿨타임을 반복하는 루프
    private IEnumerator LaserLoop()
    {
        float elapsedSinceStart = Time.time - attackStartTime;
        if (elapsedSinceStart < laserUnlockTime)
        {
            yield return new WaitForSeconds(laserUnlockTime - elapsedSinceStart);
        }

        while (true)
        {
            yield return StartCoroutine(FireLaser());

            // 레이저 종료 후부터 쿨타임 계산
            yield return new WaitForSeconds(laserCooldown);
        }
    }

    // 레이저 공격 전용 메서드. laserFirePoint 위치에 고정되어 플레이어를 추적하지 않음.
    // 텔레그래프 생성 -> 깜빡임 유지 -> 텔레그래프 제거 -> 레이저 생성 -> 유지 -> 자동 삭제 순서로 진행
    private IEnumerator FireLaser()
    {
        if (laserFirePoint == null)
        {
            Debug.LogWarning("[BossChaseAttack] laserFirePoint가 비어있어 레이저를 발사할 수 없습니다.");
            yield break;
        }

        // ① 텔레그래프 생성 (laserFirePoint 위치 고정)
        GameObject telegraph = null;
        if (laserTelegraphPrefab != null)
        {
            telegraph = Instantiate(laserTelegraphPrefab, laserFirePoint.position, laserFirePoint.rotation);
        }
        else
        {
            Debug.LogWarning("[BossChaseAttack] laserTelegraphPrefab이 비어있어 텔레그래프 없이 진행합니다.");
        }

        SpriteRenderer telegraphSr = telegraph != null ? telegraph.GetComponent<SpriteRenderer>() : null;

        // 1.5초(laserTelegraphDuration) 동안 유지하며 0.5초(laserBlinkInterval) 간격으로 깜빡임.
        // WaitForSeconds 대신 매 프레임 대기로 처리해서, 대기하는 동안에도 laserFirePoint 위치를 계속 따라가도록 고정
        float blinkElapsed = 0f;
        float nextBlinkTime = laserBlinkInterval;
        bool visible = true;
        while (blinkElapsed < laserTelegraphDuration)
        {
            if (telegraph != null)
            {
                telegraph.transform.position = laserFirePoint.position;
                telegraph.transform.rotation = laserFirePoint.rotation;
            }

            blinkElapsed += Time.deltaTime;

            if (blinkElapsed >= nextBlinkTime)
            {
                nextBlinkTime += laserBlinkInterval;
                visible = !visible;
                if (telegraphSr != null) telegraphSr.enabled = visible;
            }

            yield return null;
        }

        // ② 텔레그래프 제거
        if (telegraph != null) Destroy(telegraph);

        // ③ 레이저 생성
        if (laserPrefab != null)
        {
            GameObject laserObj = Instantiate(laserPrefab, laserFirePoint.position, laserFirePoint.rotation);

            BossChaseLaser hazard = laserObj.GetComponent<BossChaseLaser>();
            if (hazard == null) hazard = laserObj.AddComponent<BossChaseLaser>();
            hazard.Initialize(attackDamage);
            hazard.SetPinnedPoint(laserFirePoint); // 레이저 피벗을 laserFirePoint 위치에 계속 고정

            // laserDuration(0.5초) 동안 유지 후 자동 삭제
            Destroy(laserObj, laserDuration);
            yield return new WaitForSeconds(laserDuration);
        }
        else
        {
            Debug.LogWarning("[BossChaseAttack] laserPrefab이 비어있어 레이저를 발사할 수 없습니다.");
        }
    }
    void OnDisable()
    {
        if (fireLoopCoroutine != null)
        {
            StopCoroutine(fireLoopCoroutine);
            fireLoopCoroutine = null;
        }

        // 레이저 루프도 함께 정지
        if (laserLoopCoroutine != null)
        {
            StopCoroutine(laserLoopCoroutine);
            laserLoopCoroutine = null;
        }
    }
}