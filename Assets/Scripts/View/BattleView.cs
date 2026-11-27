using System.Collections.Generic;
using UnityEngine;

// 전투의 그림(docs/COMBAT_ARCHITECTURE.md 12절 D44). 시뮬레이션의 전투원에 GameObject(호스트)를 붙이고 떼고, 화면 프레임마다 보간해 그린다.
// - 월드의 Joined: 그 전투원의 종류(Archetype)로 프리팹을 골라 풀에서 꺼내 붙인다(플레이어, 몬스터, 보스가 같은 길).
// - 월드의 Left: 떼서 풀로 돌려놓는다. 같은 전투원이 다시 들어오면 그 Joined에 새로 붙는다.
// - 스테이지를 떠나면 이전 스테이지 몬스터의 GameObject 풀을 비운다. 그 프레임의 배달(Left)이 다 끝난 뒤(Render)에 한다.
// 시뮬레이션은 이 클래스를 모른다. 이 클래스도 전투원을 만들거나 월드를 바꾸지 않는다.
// (예전 MonsterSpawner. 파일과 GUID를 옮겨 씬의 참조와 보스 부모(spawnRoot)가 그대로 남는다.)
public class BattleView : MonoBehaviour
{
    [Tooltip("보스를 둘 부모(일반 몬스터와 플레이어는 부모 없이 둔다)")]
    [SerializeField] private Transform spawnRoot;

    private readonly Dictionary<CombatantHost, GameObject> prefabOf = new Dictionary<CombatantHost, GameObject>();
    private readonly List<int> stagesToRelease = new List<int>();
    private Presentation presentation;
    private PoolManager pools;
    private PlayAreaBounds playArea;
    private CombatantViews views;
    private System.IDisposable stageSubscription;
    private int currentStage;

    // 전투원 → 뷰. 이펙트 위치, 카메라, 개발 도구(판정 몸 표시)가 찾는다. Bind 전에는 null.
    public CombatantViews Views => views;

    // GameInitializer가 게임을 만든 뒤, 흐름을 시작하기 전에 잇는다(첫 Joined부터 받는다).
    // 풀과 바닥 높이(PlayAreaBounds)도 조립 지점이 넘긴다(정적 조회 없음).
    public void Bind(Game game, Presentation presentationAssets, PoolManager poolManager, PlayAreaBounds playAreaBounds)
    {
        presentation = presentationAssets;
        pools = poolManager;
        playArea = playAreaBounds;
        views = new CombatantViews(game.World.Events, Attach, Detach);
        stageSubscription = game.Flow.StageEntered.Subscribe(entered =>
        {
            if (currentStage != 0 && currentStage != entered.StageNumber) stagesToRelease.Add(currentStage);
            currentStage = entered.StageNumber;
        });
    }

    // GameRunner가 화면 프레임마다 게임을 진행한 뒤 부른다. alpha = 마지막 단계 이후 흐른 시간의 비율.
    public void Render(float alpha)
    {
        if (views == null) return;

        IReadOnlyList<CombatantHost> hosts = views.All;
        for (int i = 0; i < hosts.Count; i++)
        {
            if (hosts[i] != null && hosts[i].isActiveAndEnabled) hosts[i].ApplyRenderPose(alpha);
        }

        if (stagesToRelease.Count > 0) ReleaseStages();
    }

    private void OnDestroy()
    {
        stageSubscription?.Dispose();
        views?.Dispose();
    }

    private CombatantHost Attach(IReadOnlyCombatant combatant)
    {
        CombatantArchetype archetype = combatant.Archetype;
        GameObject prefab;
        float groundOffset;
        switch (archetype.Kind)
        {
            case CombatantKind.Character:
            {
                CharacterEntry entry = presentation.Character(archetype.Id);
                prefab = entry?.prefab;
                PlayerController player = prefab != null ? prefab.GetComponent<PlayerController>() : null;
                groundOffset = player != null ? player.GroundOffset : 0f;
                break;
            }
            case CombatantKind.Monster:
            {
                MonsterEntry entry = presentation.Monster(archetype.Id);
                prefab = entry?.prefab;
                groundOffset = entry != null ? entry.groundOffset : 0f;
                break;
            }
            default:
                Debug.LogError($"[BattleView] 종류를 모르는 전투원이라 그리지 않습니다({archetype}).");
                return null;
        }

        CombatantHost prefabHost = prefab != null ? prefab.GetComponent<CombatantHost>() : null;
        if (prefabHost == null)
        {
            Debug.LogError($"[BattleView] {archetype}의 프리팹이 없거나 호스트(CombatantHost) 컴포넌트가 없습니다.");
            return null;
        }

        // 피벗 = 몸 중심 − 몸 오프셋. 바닥 높이 + 캐릭터별 보정(피벗이 발 기준이 아닌 스프라이트).
        var position = new Vector3(combatant.X - prefabHost.Anchor.BodyOffset.x, GroundY() + groundOffset, 0f);
        Transform parent = archetype.IsBoss ? spawnRoot : null;
        GameObject instance = pools.Spawn(prefab, position, Quaternion.identity, parent);

        CombatantHost host = instance.GetComponent<CombatantHost>();
        prefabOf[host] = prefab;
        host.Bind(combatant);
        return host;
    }

    private void Detach(IReadOnlyCombatant combatant, CombatantHost host)
    {
        host.Unbind();
        if (prefabOf.TryGetValue(host, out GameObject prefab)) pools.Despawn(prefab, host.gameObject);
    }

    // 떠난 스테이지의 몬스터 풀을 비운다. 지금 그려지는 뷰가 그 프리팹을 쓰면(다음 스테이지에도 나오는 몬스터) 남긴다.
    private void ReleaseStages()
    {
        foreach (int stageNumber in stagesToRelease)
        {
            StageEntry stage = presentation.Stage(stageNumber);
            if (stage?.data == null) continue;
            ReleasePrefab(presentation.Monster(stage.data.NormalMonsterId)?.prefab);
            ReleasePrefab(presentation.Monster(stage.data.BossMonsterId)?.prefab);
        }
        stagesToRelease.Clear();
    }

    private void ReleasePrefab(GameObject prefab)
    {
        if (prefab == null) return;
        IReadOnlyList<CombatantHost> hosts = views.All;
        for (int i = 0; i < hosts.Count; i++)
        {
            if (hosts[i] != null && prefabOf.TryGetValue(hosts[i], out GameObject used) && used == prefab) return;
        }

        // 비울 풀의 인스턴스는 파괴되므로 표에서도 뺀다.
        var released = new List<CombatantHost>();
        foreach (KeyValuePair<CombatantHost, GameObject> pair in prefabOf)
        {
            if (pair.Value == prefab) released.Add(pair.Key);
        }
        foreach (CombatantHost host in released) prefabOf.Remove(host);

        pools.ClearPool(prefab);
    }

    // 항상 바닥(GroundY)에 붙어 있는 것처럼 보이도록. PlayAreaBounds가 없으면(테스트 씬 등) 0.
    private float GroundY() => playArea != null ? playArea.GroundY : 0f;
}
