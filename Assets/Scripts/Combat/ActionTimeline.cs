using System;
using System.Collections.Generic;
using UnityEngine;

// 캐릭터의 행동 하나(기본 공격, 스킬, 사망)의 애니메이션 시간표 에셋 값(docs/COMBAT_ARCHITECTURE.md 7-2, 7-13).
// 클립과 거기서 구운 값을 갖는 데이터 쪽 형식이다. 시뮬레이션은 이것이 아니라 GameData가 바꾼 ActionTiming을 받는다(13-4).
// 애니메이션이 정하는 것은 타격이 행동의 몇 % 지점인지(비율)와, 재생 속도 계산에 쓸 클립 길이뿐이다.
// 행동의 길이는 게임 데이터가 정한다: 기본 공격 = 공격 간격, 스킬 = SkillData.CastTime. 사망은 클립 길이.
// 그래서 아트를 바꿔도 밸런스가 바뀌지 않는다. 전투 로직은 애니메이션 이벤트나 애니메이터 전이를 읽지 않는다.
// 값은 클립에 찍힌 타격 표식에서 에디터가 굽는다(IdleGame/Combat/Bake Action Timelines).
[Serializable]
public sealed class ActionTimeline
{
    [Tooltip("행동 키이자 애니메이터 상태 이름 (Attack, Skill, Healing, Death)")]
    [SerializeField] private string key;

    [Tooltip("타격 비율의 원천. 타격 표식 이벤트(AE_OnHit)가 찍힌 클립")]
    [SerializeField] private AnimationClip clip;

    [Header("구운 값 (굽기 도구가 채움, 직접 고치지 말 것)")]
    [SerializeField] private float clipLength;
    [SerializeField] private float[] hitFractions = new float[0];
    [SerializeField] private string bakedSignature;

    public string Key => key;
    public AnimationClip Clip => clip;
    public float ClipLength => clipLength;
    public IReadOnlyList<float> HitFractions => hitFractions;
    public string BakedSignature => bakedSignature;

    // 값이 규칙을 지키는가(굽지 않았거나 잘못 구운 시간표는 행동으로 시작하지 않는다)
    public bool IsValid => ValidationError == null;
    public string ValidationError =>
        ActionTiming.Validate(key, clipLength, hitFractions ?? new float[0]);
}
