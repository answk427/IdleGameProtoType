using UnityEngine;

// 캐릭터 1종에 대한 SO 엔트리. prefab은 엑셀 변환 때 characterName으로 자동 연결한다(못 찾으면 기존 연결 유지).
[System.Serializable]
public class CharacterEntry
{
    public CharacterData data;
    public GameObject prefab;
}
