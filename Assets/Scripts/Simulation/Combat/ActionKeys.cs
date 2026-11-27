// 행동 시간표의 키. 애니메이터 상태 이름과 같다(docs/COMBAT_ARCHITECTURE.md 7-2).
// 스킬의 키는 SkillData.AnimationTrigger 값을 쓴다(같은 스킬이라도 시전자마다 다른 클립을 쓰기 때문).
public static class ActionKeys
{
    public const string Attack = "Attack";
    public const string Death = "Death";
}
