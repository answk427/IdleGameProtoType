using UnityEngine;

public class CombatEffectManager : MonoBehaviour
{
    public static CombatEffectManager Instance;

    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private Canvas dynamicCanvas;


    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void OnEnable()
    {
        GlobalCombatEvents.OnAnyTargetDamaged += HandleAnyTargetDamaged;
    }

    private void OnDisable()
    {
        GlobalCombatEvents.OnAnyTargetDamaged -= HandleAnyTargetDamaged;
    }

    private void HandleAnyTargetDamaged(IDamageable target, int damage, Vector3 position)
    {
        SpawnDamageText(damage, position);
        SpawnHitParticle(position);
    }

    public void SpawnDamageText(int damage, Vector3 worldPos)
    {
        GameObject obj = PoolManager.Instance.Spawn(
            damageTextPrefab,
            Vector3.zero,
            Quaternion.identity,
            dynamicCanvas.transform,
            false
        );

        if (!obj.TryGetComponent(out DamageText damageText))
        {
            Debug.LogError($"{damageTextPrefab.name} needs DamageText on root.");
            return;
        }

        RectTransform canvasRect = dynamicCanvas.transform as RectTransform;
        damageText.Play(damage, worldPos, dynamicCanvas, canvasRect, ReturnDamageText);
    }

    public void SpawnHitParticle(Vector3 worldPos)
    {
        GameObject obj = PoolManager.Instance.Spawn(
            hitParticlePrefab,
            worldPos,
            Quaternion.identity
        );

        if (!obj.TryGetComponent(out HitParticle hitParticle))
        {
            Debug.LogError($"{hitParticlePrefab.name} needs HitParticle on root.");
            return;
        }

        hitParticle.Play(worldPos, ReturnHitParticle);
    }

    // 반납 창구
    public void ReturnDamageText(GameObject obj)
    {
        PoolManager.Instance.Despawn(damageTextPrefab, obj);
    }

    public void ReturnHitParticle(GameObject obj)
    {
        PoolManager.Instance.Despawn(hitParticlePrefab, obj);
    }
}