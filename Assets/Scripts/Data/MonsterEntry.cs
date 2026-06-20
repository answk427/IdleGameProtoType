using UnityEngine;

// 몬스터 1종에 대한 SO 전용 엔트리.
// data는 기존 IData(MonsterData)를 그대로 재사용 (public 필드라 Unity 직렬화 가능).
// prefab은 ExcelToJsonConverter가 import 시점에 monsterName 기준으로 자동 매칭해서 채워준다.
[System.Serializable]
public class MonsterEntry
{
    public MonsterData data;
    public GameObject prefab;
}
