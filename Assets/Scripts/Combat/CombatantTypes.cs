// 전투원 모델의 작은 타입들(docs/COMBAT_ARCHITECTURE.md 8절).

public enum Team
{
    Ally,
    Enemy,
}

// 전투원이 교전하지 않을 때 움직이는 방식. 몬스터는 데이터(MonsterData.movePolicy)로 정한다.
// 3단계(공간 모델) 전까지는 지금 동작을 그대로 옮긴 것이다: 아군은 제자리에 서 있고 세상이 스크롤된다.
// 기본값(0)은 Hold다. 직렬화된 데이터에 값이 없을 때 "움직이지 않음"이 되도록.
public enum MovePolicy
{
    Hold,     // 제자리. 아군이 전진(스크롤)하는 동안에만 화면과 함께 다가온다(일반 몬스터)
    Approach, // 항상 가장 가까운 상대에게 다가간다(보스)
    Advance,  // 전진한다. 3단계 전까지는 "이동 단계면 스크롤"이라는 뜻(플레이어)
}

public enum CombatantPhase
{
    Inactive, // 스폰 전(풀에 있음)
    Moving,
    Engaged,
    Dead,
}

// 전투원의 능력치. 호스트가 데이터(플레이어 진행도 + 캐릭터 표, 몬스터 표 × 스테이지 배율)에서 만들어 넣는다.
public struct CombatantStats
{
    public GameNumber MaxHp;
    public GameNumber AttackDamage;
    public double AttackInterval; // 기본 공격 한 번의 길이(초). 0 이하면 기본 공격이 없다
    public float AttackRange;     // 표면 거리 기준
    public float MoveSpeed;       // 월드 유닛/초. 지금은 스크롤 속도의 원천(플레이어)
    public MovePolicy MovePolicy;
}

// 행동 시간표 조회. ActionTimelineSet(ScriptableObject)이 구현하고, 테스트는 가짜를 쓴다.
public interface IActionTimelineSource
{
    bool TryGet(string key, out ActionTimeline timeline);
}

// 전투원이 무엇인지(12절 D44). 뷰는 이것으로 그릴 프리팹을 고른다. 시뮬레이션 규칙은 이것을 읽지 않는다.
public enum CombatantKind : byte
{
    None,
    Character, // 플레이어 캐릭터(캐릭터 표의 id)
    Monster,   // 몬스터(몬스터 표의 id)
}

public readonly struct CombatantArchetype
{
    public readonly CombatantKind Kind;
    public readonly int Id;
    public readonly bool IsBoss; // 보스는 스폰할 때 정하는 역할이다(같은 몬스터가 일반으로도 나올 수 있다)

    public CombatantArchetype(CombatantKind kind, int id, bool isBoss)
    {
        Kind = kind;
        Id = id;
        IsBoss = isBoss;
    }

    public static CombatantArchetype Character(int id) => new CombatantArchetype(CombatantKind.Character, id, false);
    public static CombatantArchetype Monster(int id, bool isBoss) => new CombatantArchetype(CombatantKind.Monster, id, isBoss);

    public override string ToString() => IsBoss ? $"{Kind} {Id} (보스)" : $"{Kind} {Id}";
}

// 전투원의 몸과 행동 시간표(전투가 쓰는 값만, docs/COMBAT_ARCHITECTURE.md 12절 D50). 게임 데이터(GameData)가 데이터 행과 항목에서 만든다.
// 위치는 몸 중심이라 그림의 기준점(피벗 오프셋)은 여기 없다(뷰의 BodyAnchor).
public readonly struct CombatantBody
{
    public readonly float HalfWidth;
    public readonly IActionTimelineSource Timelines;

    public CombatantBody(float halfWidth, IActionTimelineSource timelines)
    {
        HalfWidth = halfWidth;
        Timelines = timelines;
    }
}
