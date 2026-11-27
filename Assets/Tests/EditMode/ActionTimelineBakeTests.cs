using NUnit.Framework;
using static ActionTimelineBaking;
using static ActionTiming;

// 클립 타격 표식 → 시간표 비율 굽기와 그 검사 규칙(docs/COMBAT_ARCHITECTURE.md 7-2, 7-13).
// 표식은 AE_OnHit 하나뿐이다. 옛 해제 표식(AE_OnAttackEnd/AE_OnDeathEnd)은 남아 있으면 오류로 알린다.
[TestFixture]
public class ActionTimelineBakeTests
{
    private static Marker M(string name, float time) => new Marker(name, time);

    [Test]
    public void Bake_ConvertsHitTimesToFractionsOfClipLength()
    {
        Result r = Bake(1.2f, new[] { M(HitMarker, 0.7f) });

        Assert.IsTrue(r.Succeeded, r.Error);
        Assert.AreEqual(1, r.HitFractions.Length);
        Assert.AreEqual(0.7f / 1.2f, r.HitFractions[0], 1e-6f);
    }

    [Test]
    public void Bake_SortsMultipleHits_AndIgnoresNonMarkerEvents()
    {
        Result r = Bake(1f, new[] { M(HitMarker, 0.6f), M("PlayFootstep", 0.1f), M(HitMarker, 0.3f) });

        Assert.IsTrue(r.Succeeded, r.Error);
        CollectionAssert.AreEqual(new[] { 0.3f, 0.6f }, r.HitFractions);
    }

    [Test]
    public void Bake_HitAtClipEnd_IsFractionOne()
    {
        Result r = Bake(0.8f, new[] { M(HitMarker, 0.8f) });

        Assert.IsTrue(r.Succeeded, r.Error);
        Assert.AreEqual(1f, r.HitFractions[0]);
    }

    [Test]
    public void Bake_DeathClip_WithoutMarkers_Succeeds()
    {
        Result r = Bake(1.1f, new Marker[0]);

        Assert.IsTrue(r.Succeeded, r.Error);
        Assert.AreEqual(0, r.HitFractions.Length);
    }

    [TestCase("AE_OnAttackEnd")]
    [TestCase("AE_OnDeathEnd")]
    [TestCase("AE_OnHitt")]
    public void Bake_Rejects_UnknownOrRetiredMarker(string marker) =>
        AssertFails(Bake(1f, new[] { M(HitMarker, 0.5f), M(marker, 0.9f) }), "모르는 표식");

    [Test]
    public void Bake_Rejects_HitAtZero() =>
        AssertFails(Bake(1f, new[] { M(HitMarker, 0f) }), "타격 시점");

    [Test]
    public void Bake_Rejects_HitBeyondClip() =>
        AssertFails(Bake(1f, new[] { M(HitMarker, 1.2f) }), "타격 시점");

    [Test]
    public void Bake_Rejects_ZeroLengthClip() =>
        AssertFails(Bake(0f, new[] { M(HitMarker, 0f) }), "클립 길이");

    [Test]
    public void ValidateUsage_AttackNeedsHit_DeathMustNotHaveHit()
    {
        Assert.IsNotNull(ValidateUsage("Attack", 0));
        Assert.IsNull(ValidateUsage("Attack", 1));
        Assert.IsNotNull(ValidateUsage("Death", 1));
        Assert.IsNull(ValidateUsage("Death", 0));
        Assert.IsNotNull(ValidateUsage("", 1));
    }

    [Test]
    public void Signature_ChangesWhenClipTimingChanges_ButNotForOrderOrNonMarkerEvents()
    {
        string a = Signature(1.2f, new[] { M(HitMarker, 0.7f), M(HitMarker, 0.9f) });
        string sameReordered = Signature(1.2f, new[] { M(HitMarker, 0.9f), M("Sound", 0.2f), M(HitMarker, 0.7f) });
        string movedHit = Signature(1.2f, new[] { M(HitMarker, 0.8f), M(HitMarker, 0.9f) });
        string longerClip = Signature(1.3f, new[] { M(HitMarker, 0.7f), M(HitMarker, 0.9f) });

        Assert.AreEqual(a, sameReordered);
        Assert.AreNotEqual(a, movedHit);
        Assert.AreNotEqual(a, longerClip);
    }

    private static void AssertFails(Result result, string expectedErrorPart)
    {
        Assert.IsFalse(result.Succeeded);
        StringAssert.Contains(expectedErrorPart, result.Error);
    }
}
