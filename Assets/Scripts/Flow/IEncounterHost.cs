// 흐름의 뷰 쪽(docs/COMBAT_ARCHITECTURE.md 10-3). 규칙(무엇을, 몇 마리, 어디에, 언제)은 EncounterDirector가 정하고,
// 호스트(MonsterSpawner)는 그 결정을 화면의 객체로 만든다. 테스트는 가짜 호스트를 쓴다.
public interface IEncounterHost
{
    // monsterId 몬스터를 피벗 x에 꺼내 능력치(몬스터 표 × 스테이지 배율)를 넣은 전투원을 돌려준다. 실패하면 null.
    // 월드 등록은 디렉터가 한다.
    Combatant Spawn(int monsterId, StageData stage, float pivotX, bool isBoss);

    // 전투원을 화면에서 치운다(풀로 돌려놓는다). 디렉터가 월드에서 먼저 뺀다.
    void Despawn(Combatant combatant);

    // 스테이지를 떠날 때 그 스테이지의 자원(몬스터 풀)을 비운다.
    void ReleaseStage(StageData stage);
}
