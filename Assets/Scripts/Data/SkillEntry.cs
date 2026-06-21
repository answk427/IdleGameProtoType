using UnityEngine;

// 스킬 1종에 대한 SO 전용 엔트리.
// data는 기존 IData(SkillData)를 그대로 재사용.
// icon/vfx/sfx는 엑셀 변환만으로는 자동 매칭하지 않고, 디자이너가 Inspector에서 직접 연결한다.
// (SkillDatabaseSyncer가 재동기화할 때도 이 참조 필드들은 보존된다.)
[System.Serializable]
public class SkillEntry
{
    public SkillData data;

    [Header("시각/청각 표현 (수동 연결)")]
    public Sprite icon;
    public GameObject castVfxPrefab;
    public GameObject hitVfxPrefab;
    public AudioClip castSfx;
    public AudioClip hitSfx;
}
