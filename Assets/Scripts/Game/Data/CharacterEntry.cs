using UnityEngine;

// 캐릭터 1종에 대한 SO 엔트리. prefab은 엑셀 변환 때 characterName으로 자동 연결한다(못 찾으면 기존 연결 유지).
[System.Serializable]
public class CharacterEntry
{
    public CharacterData data;
    public GameObject prefab;

    [Tooltip("이 캐릭터의 행동 시간표(타격 비율, 클립 길이). 전투가 읽는다(프리팹이 아니라 데이터 항목이 갖는다, docs/COMBAT_ARCHITECTURE.md 12절 D50). " +
             "엑셀 변환은 기존 연결을 보존하고, 새 캐릭터는 Assets/Data/Combat/Timelines/<characterName>.asset을 찾아 연결한다.")]
    public ActionTimelineSet timelines;
}
