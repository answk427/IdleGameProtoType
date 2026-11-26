using System;
using System.Collections.Generic;
using NUnit.Framework;

// 씬 없이 실제 게임 데이터로 돌리는 검증(docs/COMBAT_ARCHITECTURE.md 5절 "5단계 후 테스트로", 11절 5-B, R7).
// 예전에는 에디터 플레이에서 Time.captureDeltaTime으로 재던 것들이다. 지금 수치가 아니라 불변식을 본다:
// - 프레임 길이(30fps, 120fps, 렉 섞기)가 전투·흐름의 결과를 바꾸지 않는다(R1).
// - 원래의 정지 재현 시드 1~3(긴 프레임이 섞임)에서 1500초 동안 진행이 멈추지 않는다(R4, docs/COMBAT_DESIGN.md).
// 뷰·애니메이터·이펙트가 하나도 없으니, 이 결과는 연출과 무관하다(R3).
[TestFixture]
public class HeadlessGameTests
{
    // 보스까지 깨며 스테이지를 넘어가는 세이브(사본 플레이 기준선과 같은 것).
    private static PlayerSaveData Strong() => new PlayerSaveData
    {
        stats = new PlayerStatsSaveData
        {
            level = 10, hpUpgradeLevel = 10, attackUpgradeLevel = 10, attackSpeedUpgradeLevel = 5,
            learnedSkillIds = new List<int> { 1, 6 }, equippedSkillSlotIds = new[] { 1, 6, -1, -1, -1 },
        },
        currentStageNumber = 1,
    };

    // 정지를 처음 재현한 세이브(docs/TODO.md "전투"의 재현법).
    private static PlayerSaveData StallRepro() => new PlayerSaveData
    {
        stats = new PlayerStatsSaveData
        {
            level = 1, learnedSkillIds = new List<int> { 1 }, equippedSkillSlotIds = new[] { -1, -1, 1, -1, -1 },
        },
        currentStageNumber = 1,
    };

    // 재현법의 프레임: 1/30초, 100번에 한 번 1/3초(float 값 그대로).
    private static Func<int, double> LagFrames(int seed)
    {
        var random = new Random(seed);
        return _ => random.Next(100) == 0 ? 1f / 3f : 1f / 30f;
    }

    [Test]
    public void FrameLength_DoesNotChangeTheBattleOrTheFlow()
    {
        const double seconds = 90.0;
        string Run(Func<int, double> frames)
        {
            var game = new HeadlessGame(Strong(), challengeBosses: true);
            game.RunUntil(seconds, frames);
            Assert.Greater(game.Kills, 5, "전투가 실제로 일어났다");
            return game.LogBefore(seconds);
        }

        string fps30 = Run(_ => 1f / 30f);
        string fps120 = Run(_ => 1f / 120f);
        string lag = Run(LagFrames(1));

        StringAssert.Contains("S ", fps30, "스테이지 입장(보스 처치 후 전환)까지 들어 있다");
        Assert.AreEqual(fps30, fps120);
        Assert.AreEqual(fps30, lag);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void OriginalStallSeeds_NeverStop_For1500Seconds(int seed)
    {
        const double seconds = 1500.0;
        const double maxGap = 40.0; // 재현법의 판정: 게임 시간 40초 동안 처치가 없으면 정지
        var game = new HeadlessGame(StallRepro(), challengeBosses: false, record: false);

        game.RunUntil(seconds, LagFrames(seed));

        Assert.Greater(game.Kills, 100);
        double previous = 0.0;
        foreach (double t in game.KillTimes)
        {
            Assert.That(t - previous, Is.LessThanOrEqualTo(maxGap), $"{previous:0.00}초 뒤 {t - previous:0.00}초 동안 처치가 없다");
            previous = t;
        }
        Assert.That(seconds - previous, Is.LessThanOrEqualTo(maxGap), "끝까지 진행했다");
    }
}
