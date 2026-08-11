using UnityEngine;

/// <summary>
/// 씬에 빈 GameObject를 만들어 부착. Start()에서 Monster-Platform 레이어 간
/// 충돌이 실제로 켜져 있는지(Physics2D.GetIgnoreLayerCollision) 확인한다.
/// true가 찍히면(=충돌 무시 상태) 그게 바로 낙하의 원인이다.
/// </summary>
public class LayerCollisionCheck : MonoBehaviour
{
    public string layerA = "Monster";
    public string layerB = "Platform";

    void Start()
    {
        // 다른 스크립트들의 Start()가 다 끝난 뒤에 확인하기 위해 한 프레임 대기
        StartCoroutine(CheckAfterDelay());
    }

    System.Collections.IEnumerator CheckAfterDelay()
    {
        yield return null; // 한 프레임 대기 (모든 Start()가 끝난 다음 프레임)

        int a = LayerMask.NameToLayer(layerA);
        int b = LayerMask.NameToLayer(layerB);

        if (a == -1 || b == -1)
        {
            Debug.LogError($"[LayerCollisionCheck] 레이어 이름을 찾을 수 없음: {layerA}={a}, {layerB}={b}");
            yield break;
        }

        bool isIgnored = Physics2D.GetIgnoreLayerCollision(a, b);
        Debug.LogError($"[LayerCollisionCheck] '{layerA}'({a}) - '{layerB}'({b}) 충돌 무시 여부: {isIgnored} " +
                        $"({(isIgnored ? "★★★ 충돌이 꺼져있음 = 낙하 원인 확정" : "정상 (충돌 켜짐)")})");
    }
}