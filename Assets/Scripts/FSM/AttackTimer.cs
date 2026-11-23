using UnityEngine;

// 공격 타이머가 간격을 넘었을 때 다음 간격의 시작값(docs/COMBAT_DESIGN.md 3-7). PlayerAttackState와 BossAttackState가 같이 쓴다.
//
// 타이머는 고정 단계(0.02초)마다 늘어서 간격을 조금 넘긴 채로 공격이 시작된다. 넘친 조각을 버리면(timer = 0)
// 간격이 단계 단위로 올림돼서, 공격 간격이 짧을수록 실제 공격이 눈에 띄게 느려진다(간격 0.1초면 최대 20%).
// 그래서 이번 단계에 막 넘은 경우(넘친 양이 한 단계 미만)에는 그 조각을 다음 간격으로 넘긴다.
// 애니메이션이 끝나기를 기다리느라 늦어진 시간은 넘기지 않는다. 넘기면 다음 공격이 앞당겨져 몰아친다.
public static class AttackTimer
{
    public static float NextStart(float timer, float interval)
    {
        float overflow = timer - interval;
        return overflow < Time.deltaTime ? overflow : 0f;
    }
}
