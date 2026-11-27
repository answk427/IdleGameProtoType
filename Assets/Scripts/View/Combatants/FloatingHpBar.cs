using System;
using UnityEngine;
using UnityEngine.UI;

// 전투원 머리 위 HP 바. 같은 오브젝트의 호스트가 내는 HP 상태를 구독한다(docs/COMBAT_ARCHITECTURE.md 10-10 D29).
// Awake에서 한 번 구독하고 파괴될 때 해제한다. 몬스터가 풀에서 다시 나와도 구독을 새로 만들지 않아 할당이 없다.
// 꺼져 있는 동안(죽은 몬스터)에도 값은 받아 둔다.
public class FloatingHpBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;

    private IDisposable subscription;

    private void Awake()
    {
        var owner = GetComponentInParent<CombatantHost>();
        if (owner == null)
        {
            Debug.LogError($"[FloatingHpBar] {name}: 부모 중에 전투원 호스트(CombatantHost)가 없습니다.");
            return;
        }

        subscription = owner.Hp.Subscribe(UpdateHpBar);
    }

    private void OnDestroy() => subscription?.Dispose();

    private void UpdateHpBar(HpState hp)
    {
        if (fillImage != null) fillImage.fillAmount = hp.Ratio;
    }
}
