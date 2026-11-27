using System.Collections.Generic;
using UnityEngine;

// 게임이 쓰는 데이터 표 전부를 가리키는 에셋 하나(docs/COMBAT_ARCHITECTURE.md 12절 D46).
// 값의 원천은 엑셀 → 각 데이터베이스 에셋이고, 이 에셋은 그 에셋들을 모아 가리키기만 한다.
// 조립 지점(GameInitializer)과 씬 없는 실행(테스트)이 같은 에셋에서 같은 방법(Prepare)으로 데이터를 만든다.
[CreateAssetMenu(fileName = "GameDataAssets", menuName = "Data/Game Data Assets")]
public class GameDataAssets : ScriptableObject
{
    [SerializeField] private PlayerStatDatabase playerStats;
    [SerializeField] private PlayerUpgradeConfig upgrades;
    [SerializeField] private MonsterDatabase monsters;
    [SerializeField] private StageDatabase stages;
    [SerializeField] private SkillDatabase skills;
    [SerializeField] private CharacterDatabase characters;
    [SerializeField] private ContentUnlockDatabase unlocks;
    [SerializeField] private BalanceConfig balance;

    // 게임이 쓸 표를 준비한다. 에디터 플레이에서는 튜닝 덮어쓰기를 적용한 사본(8-7), 그 밖에서는 원본이다.
    // 빠진 표가 있으면 errors에 이유를 담고 null을 돌려준다(필수 데이터라 기본값으로 대신하지 않는다).
    public GameTables Prepare(List<string> errors)
    {
        int before = errors.Count;
        Require(playerStats, nameof(playerStats), errors);
        Require(upgrades, nameof(upgrades), errors);
        Require(monsters, nameof(monsters), errors);
        Require(stages, nameof(stages), errors);
        Require(skills, nameof(skills), errors);
        Require(characters, nameof(characters), errors);
        Require(unlocks, nameof(unlocks), errors);
        Require(balance, nameof(balance), errors);
        if (errors.Count > before) return null;

        return new GameTables(
            DataTuning.Prepare(playerStats), upgrades, DataTuning.Prepare(monsters), DataTuning.Prepare(stages),
            DataTuning.Prepare(skills), DataTuning.Prepare(characters), DataTuning.Prepare(unlocks), balance);
    }

    private void Require(Object table, string field, List<string> errors)
    {
        if (table == null) errors.Add($"{name}에 {field}가 연결되지 않았습니다.");
    }
}
