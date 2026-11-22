using UnityEngine;

// 스테이지 진행에 따른 난이도/보상 성장 곡선.
//
// 방치형에서 이 값들은 디자이너가 가장 자주 만지는 수치인데 전에는 MonsterSpawner 메서드에
// 하드코딩돼 있어서 밸런스 조정에 재컴파일이 필요했다. SO로 빼서 에디터에서 바로 조정 가능하게 한다.
//
// 현재는 모든 스테이지에 공통 적용되는 전역 곡선이다. 나중에 특정 스테이지만 다른 곡선이
// 필요해지면 StageData에 오버라이드 컬럼을 추가해서 이 값을 덮는 방식으로 확장한다.
[CreateAssetMenu(fileName = "BalanceConfig", menuName = "Game/Balance Config")]
public class BalanceConfig : ScriptableObject
{
    [Header("스테이지 성장 계수 — 최종 배수 = base^stageNumber")]
    [Tooltip("몬스터 체력 성장 배수")]
    public float hpGrowthBase = 1.15f;

    [Tooltip("몬스터 공격력 성장 배수")]
    public float damageGrowthBase = 1.10f;

    [Tooltip("골드 보상 성장 배수")]
    public float goldGrowthBase = 1.05f;
}
