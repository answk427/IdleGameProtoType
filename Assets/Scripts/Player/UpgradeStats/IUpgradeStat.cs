// 업그레이드 가능한 스탯 1종의 단계(저장 데이터의 어느 칸인지)와 비용 공식.
// 효과(능력치에 더해지는 양)는 PlayerStats.ComputeCombatStats 한 곳에 있다. 화면의 "현재 → 다음"은
// PlayerStats.PreviewUpgrade가 같은 계산으로 만든다(예전처럼 여기서 공식을 따로 쓰면 화면만 조용히 틀릴 수 있다).
// 새 스탯을 더하려면 이 구현과 등록 외에도 세이브 필드(형식 변경), 전투 능력치 계산, 표시가 필요하다(CLAUDE.md "UI 구조").
public interface IUpgradeStat
{
    UpgradeStatType Type { get; }
    int GetLevel();
    void IncrementLevel();
    GameNumber GetCost(int level);
}
