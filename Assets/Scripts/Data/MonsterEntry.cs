using UnityEngine;

// 몬스터 1종에 대한 SO 전용 엔트리.
// data는 기존 IData(MonsterData)를 그대로 재사용 (public 필드라 Unity 직렬화 가능).
// prefab은 ExcelToJsonConverter가 import 시점에 monsterName 기준으로 자동 매칭해서 채워준다.
[System.Serializable]
public class MonsterEntry
{
    public MonsterData data;
    public GameObject prefab;

    [Tooltip("스프라이트 피벗이 발 기준이 아닐 때, 발이 바닥(GroundY)에 닿아 보이도록 보정하는 값(월드 유닛). " +
             "피벗이 스프라이트 중앙이면 보통 '스프라이트 높이의 절반'을 넣는다.")]
    public float groundOffset = 0f;
}
