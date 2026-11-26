using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using static CombatTestKit;
using Is = UnityEngine.TestTools.Constraints.Is;

// R6: 전투 진행 중 할당 0(docs/COMBAT_ARCHITECTURE.md 8-6). 연출은 사건을 세는 가짜 구독자라서, 측정 범위는 전투 규칙 전체와 사건 배달이다
// (CombatWorld의 사건·단계 전이·이동(sweep), 원점 재설정, CombatActions, ActionRunner, 스킬 효과, HP 계산, CombatEvent 쌓기와 배달).
[TestFixture]
public class CombatAllocationTests
{
    [Test]
    public void SteppingABattle_WithBasicAttacksAoeSkillsAndDeaths_AllocatesNothing()
    {
        CombatWorld world = World();
        Skill aoe = TestSkills.Create(id: 2, cooldown: 0.3f, effectType: SkillEffectType.AoeDamage, castTime: 0.1f);
        Skill strike = TestSkills.Create(id: 1, cooldown: 0.7f, castTime: 0.2f);
        Skill bossSkill = TestSkills.Create(id: 3, cooldown: 1.1f, castTime: 0.3f);

        Combatant ally = Spawn(world, Team.Ally, 0f, 0.4f, Stats(hp: 1e12, damage: 50, interval: 0.05, range: 0.2f, policy: MovePolicy.Advance, speed: 3f),
            timelines: Timelines(death: true), skills: Loadout(strike, aoe), host: new FakeHost());

        // 다가오는 적(공격함), 화면과 함께 오는 적(공격 안 함), 금방 죽는 적이 섞여 있다.
        for (int i = 0; i < 5; i++)
        {
            bool attacker = i % 2 == 0;
            Spawn(world, Team.Enemy, 1.2f + i * 0.9f, 0.4f,
                Stats(hp: i == 1 ? 300 : 1e9, damage: 3, interval: attacker ? 0.4 : 0, range: 0.1f,
                    policy: attacker ? MovePolicy.Approach : MovePolicy.Hold, speed: attacker ? 1f : 0f),
                timelines: Timelines(death: true),
                skills: attacker ? Loadout(bossSkill) : null,
                host: new FakeHost());
        }

        // 첫 실행에서 생기는 일회성 할당(정적 초기화, JIT 등)을 빼고 잰다.
        Run(world, 200);

        // 원점 재설정(인카운터마다 하는 일)도 측정 범위에 넣는다.
        Assert.That(() =>
        {
            for (int i = 0; i < 30; i++)
            {
                Run(world, 100);
                world.RecenterOn(ally);
            }
        }, Is.Not.AllocatingGCMemory());
    }
}
