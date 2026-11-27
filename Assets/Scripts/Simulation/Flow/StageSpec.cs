// 스테이지 클리어 방식. 처리 코드가 있는 것만 둔다(D26): 예전의 SurviveTime·KillCount는 처리 코드가 없어서,
// 데이터에 넣으면 보스를 잡아도 클리어되지 않고 게임이 멈췄다. 새 방식은 처리 코드(EncounterDirector)와 함께 더한다.
// 값은 데이터(.asset)에 정수로 저장되므로 번호를 바꾸지 않는다.
public enum StageClearType
{
    KillBoss,       // 보스 처치
}

// 흐름(EncounterDirector)과 적 풀이 쓰는 스테이지 값(docs/COMBAT_ARCHITECTURE.md 13-4). 순수 C#, 불변.
// 데이터 쪽(GameData)이 데이터 행(StageData)에서 한 번 만든다. 배경은 그림 자산이라 없다(Presentation).
public sealed class StageSpec
{
    public StageSpec(int stageNumber, int normalMonsterId, int bossMonsterId, int monstersPerEncounter, int encountersToComplete,
        float monsterSpacing, float spawnDistance, double bossTimeLimit, StageClearType clearType)
    {
        StageNumber = stageNumber;
        NormalMonsterId = normalMonsterId;
        BossMonsterId = bossMonsterId;
        MonstersPerEncounter = monstersPerEncounter;
        EncountersToComplete = encountersToComplete;
        MonsterSpacing = monsterSpacing;
        SpawnDistance = spawnDistance;
        BossTimeLimit = bossTimeLimit;
        ClearType = clearType;
    }

    public int StageNumber { get; }
    public int NormalMonsterId { get; }
    public int BossMonsterId { get; }
    public int MonstersPerEncounter { get; }
    // 보스 도전이 가능해지기까지 깨야 하는 인카운터 수
    public int EncountersToComplete { get; }
    // 같은 인카운터 안 몬스터 사이 간격(몸 중심 기준)
    public float MonsterSpacing { get; }
    // 대열 앞장의 몸 중심에서 첫 몬스터의 몸 중심까지(D18, 12절 D50)
    public float SpawnDistance { get; }
    // 보스전 제한 시간(초). 0이면 없다. 넘기면 아군 전멸과 같다(D25)
    public double BossTimeLimit { get; }
    public StageClearType ClearType { get; }

    public override string ToString() => $"스테이지 {StageNumber}";
}
