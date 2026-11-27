// 흐름이 적 전투원을 얻는 곳(docs/COMBAT_ARCHITECTURE.md 10-3, 12절 D44). 규칙(무엇을, 몇 마리, 어디에, 언제)은 EncounterDirector가 정하고,
// 적 풀(EnemyRoster, 순수 C#)이 그 결정대로 전투원을 꺼내 데이터를 넣는다. 화면은 모른다. 테스트는 가짜 풀을 쓴다.
// (예전 이름 IEncounterHost: 몬스터 GameObject 스포너가 구현했다.)
public interface IEnemyRoster
{
    // monsterId 몬스터를 몸 중심 x에 꺼내 능력치(몬스터 표 × 스테이지 배율)를 넣은 전투원을 돌려준다. 실패하면 null.
    // 월드 등록은 디렉터가 한다. 위치는 몸 중심이라 그림(피벗)과 무관하다(docs/COMBAT_ARCHITECTURE.md 12절 D50).
    Combatant Spawn(int monsterId, StageData stage, float x, bool isBoss);

    // 전투원을 화면에서 치운다(풀로 돌려놓는다). 디렉터가 월드에서 먼저 뺀다.
    void Despawn(Combatant combatant);

    // 스테이지를 떠날 때 그 스테이지의 자원(몬스터 풀)을 비운다.
    void ReleaseStage(StageData stage);
}
