using UnityEngine;

// 몬스터의 호스트. 일반 몬스터와 보스가 모두 이 클래스다(docs/COMBAT_ARCHITECTURE.md 8-3).
// 전투 규칙은 Combatant가 갖고, 둘의 차이는 데이터다:
//   공격 간격 > 0이면 기본 공격, skillId가 있으면 스킬, movePolicy가 Approach면 자기 이동 속도(moveSpeed)로 다가온다.
// 보스인지는 스폰할 때 정하는 역할이다(흐름이 어느 전투원이 보스인지 안다).
// 처치 보상은 스폰할 때 전투원에 넣고(사망 사건이 싣고 간다), 이 뷰는 보상을 모른다.
public class Monster : CombatantHost
{
    [SerializeField] private FloatingHpBar hpBar;

    private readonly int[] skillSlot = { SkillLoadout.EmptySlot };
    private SkillLoadout skills;

    protected override Team Team => Team.Enemy;

    public GameObject OriginPrefab { get; set; }

    protected override void Awake()
    {
        base.Awake();
        if (hpBar == null) hpBar = GetComponentInChildren<FloatingHpBar>();
        skills = new SkillLoadout(1, id => GameDatabaseManager.Instance?.GetSkill(id));
    }

    private void OnDisable()
    {
        // 정상 경로는 스포너가 풀에 돌려주기 전에 월드에서 뺀다. 다른 경로로 꺼져도 월드에 남지 않게 한다.
        Combatant?.World?.Remove(Combatant);
    }

    // 스포너가 위치를 정한 뒤에 부른다(풀에서 꺼낸 같은 객체를 다시 쓰므로 이전 상태를 모두 지운다).
    // 능력치와 처치 보상의 계산은 CombatantSetup 한 곳(씬 없는 실행도 같은 계산).
    public void Spawn(MonsterData data, double hpMultiplier, double damageMultiplier, double goldMultiplier)
    {
        CombatReward reward = CombatantSetup.RewardOf(data, goldMultiplier);

        skillSlot[0] = data.SkillId > 0 ? data.SkillId : SkillLoadout.EmptySlot;
        skills.SetSlots(skillSlot);
        skills.ResetCooldowns();

        CombatantStats stats = CombatantSetup.ForMonster(data, hpMultiplier, damageMultiplier);

        // 체력바가 먼저 켜져 있어야 등록(Joined) 때 HP 알림을 받는다.
        if (hpBar != null) hpBar.gameObject.SetActive(true);
        Combatant.Spawn(stats, BodyCenterX(transform.position.x), hitboxProfile.HalfWidth, timelines, skills, reward);
    }

    protected override void HandleDied()
    {
        base.HandleDied();

        // 시체는 인카운터가 끝날 때까지 화면에 남으므로 빈 체력바를 숨긴다.
        // 보상 지급은 사망 사건을 받은 GameManager가, 동전 연출은 CombatEffectManager가 한다.
        // 보스 처치(스테이지 클리어)는 흐름(EncounterDirector)이 다음 단계 ①에서 월드에 묻는다.
        if (hpBar != null) hpBar.gameObject.SetActive(false);
    }
}
