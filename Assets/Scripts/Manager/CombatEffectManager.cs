using UnityEngine;

public class CombatEffectManager : MonoBehaviour
{
    public static CombatEffectManager Instance;

    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private GameObject hitParticlePrefab;
    [SerializeField] private Canvas dynamicCanvas;

    [Header("Reward Drop")]
    [SerializeField] private GameObject rewardDropPrefab;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Sprite chestSprite;
    [SerializeField] private int minCoinDropCount = 3;
    [SerializeField] private int maxCoinDropCount = 6;
    [SerializeField] private float chestDropChance = 0.15f;
    [SerializeField] private int rewardSortingOrder = 30;

    private Sprite defaultCoinSprite;
    private Sprite defaultChestSprite;
    private GameObject runtimeRewardDropPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        EnsureRewardDropPrefab();
    }

    private void OnEnable()
    {
        GlobalCombatEvents.OnAnyTargetDamaged += HandleAnyTargetDamaged;
        GlobalCombatEvents.OnMonsterDied += SpawnRewardDrop;
    }

    private void OnDisable()
    {
        GlobalCombatEvents.OnAnyTargetDamaged -= HandleAnyTargetDamaged;
        GlobalCombatEvents.OnMonsterDied -= SpawnRewardDrop;
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

    public void SpawnRewardDrop(Object target, int goldAmount, Vector3 worldPos)
    {
        int coinCount = Mathf.Clamp(goldAmount, minCoinDropCount, maxCoinDropCount);
        for (int i = 0; i < coinCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.45f, 0.45f),
                Random.Range(-0.15f, 0.15f),
                0f
            );

            SpawnRewardIcon(
                GetCoinSprite(),
                Color.white,
                worldPos + Vector3.up * 0.25f,
                offset,
                Random.Range(0.45f, 0.7f),
                Random.Range(0.16f, 0.22f),
                true
            );
        }

        if (Random.value <= chestDropChance)
        {
            SpawnRewardIcon(
                GetChestSprite(),
                Color.white,
                worldPos + Vector3.up * 0.35f,
                new Vector3(Random.Range(-0.3f, 0.3f), 0f, 0f),
                0.65f,
                0.35f,
                false
            );
        }
    }

    private void SpawnRewardIcon(
        Sprite sprite,
        Color color,
        Vector3 startPosition,
        Vector3 landingOffset,
        float duration,
        float size,
        bool rotate)
    {
        GameObject prefab = EnsureRewardDropPrefab();
        if (prefab == null)
        {
            Debug.LogError("CombatEffectManager needs a RewardDropEffect prefab.");
            return;
        }

        GameObject obj = PoolManager.Instance.Spawn(prefab, startPosition, Quaternion.identity);
        if (!obj.TryGetComponent(out RewardDropEffect effect))
        {
            Debug.LogError($"{prefab.name} needs RewardDropEffect on root.");
            ReturnRewardDrop(obj);
            return;
        }

        effect.Play(sprite, color, startPosition, landingOffset, duration, size, rewardSortingOrder, rotate, ReturnRewardDrop);
    }

    private GameObject EnsureRewardDropPrefab()
    {
        if (rewardDropPrefab != null) return rewardDropPrefab;
        if (runtimeRewardDropPrefab != null) return runtimeRewardDropPrefab;

        runtimeRewardDropPrefab = new GameObject("RewardDropEffect_RuntimePrefab");
        runtimeRewardDropPrefab.transform.SetParent(transform);
        runtimeRewardDropPrefab.AddComponent<SpriteRenderer>();
        runtimeRewardDropPrefab.AddComponent<RewardDropEffect>();
        runtimeRewardDropPrefab.SetActive(false);

        return runtimeRewardDropPrefab;
    }

    private Sprite GetCoinSprite()
    {
        if (coinSprite != null) return coinSprite;
        if (defaultCoinSprite == null)
        {
            defaultCoinSprite = CreateCircleSprite("Default Coin Sprite", new Color(1f, 0.78f, 0.16f), new Color(1f, 0.95f, 0.45f));
        }

        return defaultCoinSprite;
    }

    private Sprite GetChestSprite()
    {
        if (chestSprite != null) return chestSprite;
        if (defaultChestSprite == null)
        {
            defaultChestSprite = CreateChestSprite();
        }

        return defaultChestSprite;
    }

    private Sprite CreateCircleSprite(string spriteName, Color baseColor, Color highlightColor)
    {
        const int size = 32;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = spriteName;
        texture.filterMode = FilterMode.Point;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance > radius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                Color pixelColor = distance < radius * 0.62f ? highlightColor : baseColor;
                texture.SetPixel(x, y, pixelColor);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateChestSprite()
    {
        const int width = 36;
        const int height = 28;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.name = "Default Chest Sprite";
        texture.filterMode = FilterMode.Point;

        Color outline = new Color(0.22f, 0.12f, 0.05f);
        Color wood = new Color(0.62f, 0.32f, 0.12f);
        Color lid = new Color(0.8f, 0.45f, 0.16f);
        Color metal = new Color(1f, 0.82f, 0.22f);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool body = x >= 3 && x < width - 3 && y >= 5 && y < 20;
                bool top = x >= 5 && x < width - 5 && y >= 17 && y < 25;

                if (!body && !top)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                bool edge = x == 3 || x == width - 4 || y == 5 || y == 19 || y == 24;
                bool band = x >= 16 && x <= 19;
                bool lockPlate = x >= 15 && x <= 20 && y >= 9 && y <= 14;

                if (edge)
                {
                    texture.SetPixel(x, y, outline);
                }
                else if (band || lockPlate)
                {
                    texture.SetPixel(x, y, metal);
                }
                else
                {
                    texture.SetPixel(x, y, top && y > 19 ? lid : wood);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
    }

    [Header("Generic Skill VFX")]
    [SerializeField] private float defaultVfxLifetime = 1.5f;

    // 스킬 등 범용 일회성 VFX 프리팹 재생. 풀링 후 일정 시간 뒤 자동 반납.
    public void PlayVfx(GameObject vfxPrefab, Vector3 worldPos)
    {
        if (vfxPrefab == null) return;

        GameObject obj = PoolManager.Instance.Spawn(vfxPrefab, worldPos, Quaternion.identity);
        StartCoroutine(ReturnVfxAfterDelay(vfxPrefab, obj, GetVfxLifetime(obj)));
    }

    private float GetVfxLifetime(GameObject obj)
    {
        if (obj.TryGetComponent(out ParticleSystem ps))
        {
            return ps.main.duration + ps.main.startLifetime.constantMax;
        }
        return defaultVfxLifetime;
    }

    private System.Collections.IEnumerator ReturnVfxAfterDelay(GameObject prefab, GameObject instance, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (instance != null)
            PoolManager.Instance.Despawn(prefab, instance);
    }

    public void ReturnDamageText(GameObject obj)
    {
        PoolManager.Instance.Despawn(damageTextPrefab, obj);
    }

    public void ReturnHitParticle(GameObject obj)
    {
        PoolManager.Instance.Despawn(hitParticlePrefab, obj);
    }

    public void ReturnRewardDrop(GameObject obj)
    {
        PoolManager.Instance.Despawn(EnsureRewardDropPrefab(), obj);
    }
}
