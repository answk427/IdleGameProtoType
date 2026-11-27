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

    // 흐름의 대기 시간(D24, docs/COMBAT_ARCHITECTURE.md 10-3). 모든 스테이지 공통이다. 0이면 그 대기가 없다.
    // 흐름은 고정 단계 경계에서 판단해서 대기를 단계 수로 센다. double인 이유: float면 0.1 같은 값이 한 단계 늘어난다.
    [Header("흐름 시간(초) — 시뮬레이션 시간이라 배속에서 같이 빨라진다")]
    [Tooltip("인카운터의 적이 모두 죽은 뒤 시체를 남겨 두는 시간. 끝나면 정리하고 다음 인카운터")]
    public double encounterClearDelay = 0.5;

    [Tooltip("보스 도전을 받은 뒤 보스가 나오기까지")]
    public double bossIntroDelay = 1.0;

    [Tooltip("스테이지 전환 전체 길이. 가운데에서 스테이지가 바뀐다(페이드 아웃 → 바뀜 → 페이드 인)")]
    public double transitionDuration = 1.0;
}
