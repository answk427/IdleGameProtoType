using System.Collections.Generic;
using UnityEngine;

// 전투 연출(데미지 숫자, 타격 이펙트, 스킬 이펙트, 보상 동전). 모두 월드 위치에 생기고, 카메라가 지나간다(3단계).
// 원점 재설정(CombatSimulation이 전함) 때는 지금 살아 있는 것을 같은 양만큼 옮긴다(docs/COMBAT_ARCHITECTURE.md 9-3).
// 이펙트 프리팹은 로컬 시뮬레이션 공간이어야 한다(트랜스폼만 옮기면 파티클도 따라오게). 월드 공간이면 데이터 검사가 잡는다.
public class CombatEffectManager : MonoBehaviour
{
    public static CombatEffectManager Instance;

    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private GameObject hitParticlePrefab; // 플레이어가 몬스터를 벨 때 (검 이펙트)
    [SerializeField] private GameObject playerHitParticlePrefab; // 플레이어가 맞을 때 (타격 이펙트)
    [SerializeField] private Canvas dynamicCanvas;

    [Header("Floating Text Colors")]
    [SerializeField] private Color playerDamageColor = Color.red;
    [SerializeField] private Color monsterDamageColor = Color.white;
    [SerializeField] private Color healColor = Color.green;

    [Header("Reward Drop")]
    [SerializeField] private GameObject rewardDropPrefab;
    [SerializeField] private Sprite coinSprite;
    [SerializeField] private Sprite chestSprite;
    [SerializeField] private int minCoinDropCount = 3;
    [SerializeField] private int maxCoinDropCount = 6;
    [SerializeField] private float chestDropChance = 0.15f;
    [SerializeField] private int rewardSortingOrder = 30;

    // 지금 살아 있는 월드 연출(원점 재설정 때 옮긴다)
    private readonly List<DamageText> liveTexts = new List<DamageText>();
    private readonly List<RewardDropEffect> liveDrops = new List<RewardDropEffect>();
    private readonly List<Transform> liveEffects = new List<Transform>();

    private CompositeDisposable busSubscriptions;

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
        busSubscriptions = new CompositeDisposable();
        GlobalCombatEvents.TargetDamaged.Subscribe(HandleTargetDamaged).AddTo(busSubscriptions);
        GlobalCombatEvents.MonsterDied.Subscribe(HandleMonsterDied).AddTo(busSubscriptions);
        GlobalCombatEvents.Healed.Subscribe(HandleHealed).AddTo(busSubscriptions);
    }

    private void OnDisable()
    {
        busSubscriptions?.Dispose();
        busSubscriptions = null;
    }

    // 데미지 숫자는 출처와 무관하게 항상 표시하되, 맞은 대상이 플레이어인지에 따라
    // 색만 다르게 한다 (플레이어가 맞으면 빨강, 몬스터가 맞으면 기존 흰색 유지).
    private void HandleTargetDamaged(GlobalCombatEvents.Damage e)
    {
        Color color = e.Target.Team == Team.Ally ? playerDamageColor : monsterDamageColor;
        SpawnDamageText(e.Amount, e.Position, color);
    }

    private void HandleHealed(GlobalCombatEvents.Heal e)
    {
        SpawnDamageText(e.Amount, e.Position, healColor, "+");
    }

    private void HandleMonsterDied(GlobalCombatEvents.MonsterDeath e) => SpawnRewardDrop(e.Monster, e.GoldReward, e.Position);

    public void SpawnDamageText(GameNumber amount, Vector3 worldPos, Color color, string prefix = "")
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
        liveTexts.Add(damageText);
        damageText.Play(amount, worldPos, dynamicCanvas, canvasRect, ReturnDamageText, color, prefix);
    }

    // 어떤 이펙트를 쓸지는 "맞은 대상이 아군(플레이어)인가"로 결정한다 (데미지 텍스트 색 판단과 동일한 기준).
    public void SpawnHitParticle(Combatant target)
    {
        bool isPlayerHit = target.Team == Team.Ally;
        GameObject prefab = isPlayerHit && playerHitParticlePrefab != null
            ? playerHitParticlePrefab
            : hitParticlePrefab;

        Vector3 worldPos = CombatantHost.EffectPositionOf(target);
        GameObject obj = PoolManager.Instance.Spawn(
            prefab,
            worldPos,
            Quaternion.identity
        );

        if (!obj.TryGetComponent(out HitParticle hitParticle))
        {
            Debug.LogError($"{prefab.name} needs HitParticle on root.");
            return;
        }

        liveEffects.Add(obj.transform);
        hitParticle.Play(worldPos, returnedObj => ReturnHitParticle(prefab, returnedObj));
    }

    public void SpawnRewardDrop(Object target, GameNumber goldAmount, Vector3 worldPos)
    {
        // 골드가 아무리 커도 코인 개수는 [min, max]로 제한되므로, 먼저 max로 잘라서 int로 바꾼다.
        int cappedAmount = (int)GameNumber.Min(goldAmount, maxCoinDropCount).ToDouble();
        int coinCount = Mathf.Clamp(cappedAmount, minCoinDropCount, maxCoinDropCount);
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

        liveDrops.Add(effect);
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
        liveEffects.Add(obj.transform);
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
        {
            liveEffects.Remove(instance.transform);
            PoolManager.Instance.Despawn(prefab, instance);
        }
    }

    public void ReturnDamageText(GameObject obj)
    {
        if (obj.TryGetComponent(out DamageText text)) liveTexts.Remove(text);
        PoolManager.Instance.Despawn(damageTextPrefab, obj);
    }

    public void ReturnHitParticle(GameObject prefab, GameObject obj)
    {
        liveEffects.Remove(obj.transform);
        PoolManager.Instance.Despawn(prefab, obj);
    }

    public void ReturnRewardDrop(GameObject obj)
    {
        if (obj.TryGetComponent(out RewardDropEffect drop)) liveDrops.Remove(drop);
        PoolManager.Instance.Despawn(EnsureRewardDropPrefab(), obj);
    }

    // 원점 재설정: 지금 살아 있는 월드 연출을 같은 양만큼 옮긴다(전투원·카메라·배경은 시뮬레이션에서 다시 계산하므로 할 일이 없다).
    public void ShiftOrigin(float delta)
    {
        for (int i = 0; i < liveTexts.Count; i++) liveTexts[i].ShiftOrigin(delta);
        for (int i = 0; i < liveDrops.Count; i++) liveDrops[i].ShiftOrigin(delta);
        for (int i = 0; i < liveEffects.Count; i++)
        {
            Transform t = liveEffects[i];
            if (t == null) continue;
            Vector3 position = t.position;
            position.x -= delta;
            t.position = position;
        }
    }
}
