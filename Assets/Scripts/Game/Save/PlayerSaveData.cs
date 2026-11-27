// 저장 파일 전체에 대응하는 최상위 데이터 (JSON 직렬화 대상).
[System.Serializable]
public class PlayerSaveData
{
    public PlayerStatsSaveData stats = new PlayerStatsSaveData();
    // 런타임에서는 GameNumber지만, 저장 형식은 기본 타입(double)으로 둔다.
    // 구조체를 넣으면 JSON 모양이 바뀌어 기존 세이브 값이 조용히 사라지기 때문(직렬화 함정).
    // 이전 형식(int)으로 저장된 정수 값도 그대로 읽힌다.
    public double gold = 0;
    public int currentStageNumber = 1;

    // 깊은 복사. 진행도(GameSession)는 경계에서 복사해 받고 복사해 준다(docs/COMBAT_ARCHITECTURE.md 12절 D49):
    // 넘긴 쪽이 나중에 고쳐도, 비동기 저장이 쓰는 도중에 진행도가 바뀌어도 서로 섞이지 않는다.
    public PlayerSaveData Clone() => new PlayerSaveData
    {
        stats = stats != null ? stats.Clone() : null,
        gold = gold,
        currentStageNumber = currentStageNumber,
    };
}
