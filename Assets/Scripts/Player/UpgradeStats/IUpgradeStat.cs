// 업그레이드 가능한 스탯 1종이 알아야 하는 동작 전부.
// 새 스탯을 추가하려면 이 인터페이스를 구현하는 클래스 하나만 만들고
// PlayerStats의 등록 목록에 추가하면 된다 (인터페이스라 구현을 빠뜨리면 컴파일이 막힌다).
public interface IUpgradeStat
{
    int GetLevel();
    void IncrementLevel();
    int GetCost(int level);
    float GetCurrentValue();
    float GetNextValue();
}
