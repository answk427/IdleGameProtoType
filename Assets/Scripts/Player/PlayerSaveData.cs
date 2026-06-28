// 저장 파일 전체에 대응하는 최상위 데이터 (JSON 직렬화 대상).
[System.Serializable]
public class PlayerSaveData
{
    public PlayerStatsSaveData stats = new PlayerStatsSaveData();
    public int gold = 0;
    public int currentStageNumber = 1;
}
