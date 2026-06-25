using UnityEngine;

// StageData가 이제 [SerializeField] 필드 기반이라 Unity 직렬화가 되므로,
// MonsterEntry와 동일한 패턴으로 필드를 복제하지 않고 그대로 참조한다.
// backgroundTexture는 StageData.bgTexturePath(문자열, 동기화 입력값)를 기준으로
// StageDatabaseSyncer가 자동 매칭해서 채워주는 결과물이다. 게임플레이 코드는
// bgTexturePath를 직접 읽지 말고 항상 이 backgroundTexture만 사용해야 한다
// (런타임 Resources.Load 경로 오타/누락 문제를 구조적으로 막기 위함).
[System.Serializable]
public class StageEntry
{
    public StageData data;
    public Texture2D backgroundTexture;
}

