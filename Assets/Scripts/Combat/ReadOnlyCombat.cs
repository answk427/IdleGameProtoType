using System.Collections.Generic;

// 바깥(뷰, 이펙트, UI, 개발자 도구)이 보는 전투의 읽기 전용 접점(docs/COMBAT_ARCHITECTURE.md 13-6, D52).
// 전투를 만들고 진행하는 것(스폰, 등록, 단계 진행, 배달)은 주인(Game)과 시뮬레이션 자신만 한다. 바깥은 Game에서 이 접점만 받고,
// 사건(CombatEvent)에 실린 전투원도 이 접점이다. 바깥 어셈블리가 구체 형식을 필드·인자·반환형으로 갖지 않는지는 ArchitectureTests가 본다.

// 전투원 하나의 읽기 값
public interface IReadOnlyCombatant
{
    Team Team { get; }
    // 무엇인지(캐릭터/몬스터, id, 보스). 뷰가 그릴 프리팹을 고른다.
    CombatantArchetype Archetype { get; }
    CombatantPhase Phase { get; }
    bool IsAlive { get; }

    // 판정 몸의 중심(월드 x)과 반너비, 사거리(표면 거리)
    float X { get; }
    float HalfWidth { get; }
    float AttackRange { get; }

    GameNumber Hp { get; }
    GameNumber MaxHp { get; }

    // 직전 두 고정 단계 사이의 보간 위치(alpha 0~1)
    float GetRenderX(float alpha);

    // 그 전투원의 행동 시간표(뷰가 클립 재생 속도를 맞춘다)
    bool TryGetTiming(string key, out ActionTiming timing);
}

// 전투 월드의 읽기 값과 사건
public interface IReadOnlyCombatWorld
{
    // 전투 사건. 고정 단계 끝(⑥)과 화면 프레임 끝에 일어난 순서대로 온다.
    Observable<CombatEvent> Events { get; }
    // 등록된 전투원 전부(아군 → 적, 각 팀은 등록 순)
    IReadOnlyList<IReadOnlyCombatant> Combatants { get; }
    // 대열의 앞장(살아 있는 아군 중 가장 앞). 없으면 null.
    IReadOnlyCombatant LeadAlly { get; }
    // 원점 재설정으로 뺀 거리의 합(배경의 연속성)
    double TotalShift { get; }
    // 시뮬레이션 시각(초)
    double Now { get; }
}
