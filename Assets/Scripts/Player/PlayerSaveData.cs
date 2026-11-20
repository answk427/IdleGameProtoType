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
}
