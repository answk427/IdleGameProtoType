using System.Collections.Generic;
using UnityEngine;

// 콘텐츠 해금(docs/UI_DESIGN.md 5-8, 10-6). 게임 규칙이라 게임 쪽(GameSession)이 갖고, UI(탭바)는 결과를 구독만 한다.
//
// 해금은 저장하지 않는다. 조건이 이미 저장되는 진행도(레벨, 저장된 스테이지 번호)로 계산되는 파생 상태라서
// 세이브 형식이 바뀌지 않고(CLAUDE.md "저장 시스템" ⚠️), 진행도를 교체하거나 되돌려도(Apply) 저절로 맞는다.
// 대가: 진행도가 내려가면(개발자 도구, 롤백) 다시 잠긴다. "한 번 열리면 계속"인 조건(구매, 튜토리얼 완료)이 생기면
// 그때 저장된 사실로 더한다(세이브 버전이 먼저, docs/TODO.md "저장").
//
// 표에 행이 없는 콘텐츠는 잠기고 에러를 한 번 남긴다(조용히 열리지 않게. 데이터 검사도 같은 것을 잡는다).
public sealed class ContentUnlocks
{
    private readonly ContentUnlockDatabase table;
    private readonly IReadOnlyReactiveProperty<int> level;
    private readonly IReadOnlyReactiveProperty<int> stageNumber;
    private readonly Dictionary<GameContent, DerivedProperty<bool>> unlocked = new Dictionary<GameContent, DerivedProperty<bool>>();
    private readonly HashSet<GameContent> reportedMissing = new HashSet<GameContent>();

    // level: 플레이어 레벨, stageNumber: 저장된 스테이지 번호. 둘 다 진행도의 상태다.
    public ContentUnlocks(ContentUnlockDatabase table, IReadOnlyReactiveProperty<int> level, IReadOnlyReactiveProperty<int> stageNumber)
    {
        this.table = table;
        this.level = level;
        this.stageNumber = stageNumber;
    }

    // 같은 콘텐츠에는 같은 상태 객체를 준다.
    public IReadOnlyReactiveProperty<bool> IsUnlocked(GameContent content)
    {
        if (!unlocked.TryGetValue(content, out DerivedProperty<bool> state))
        {
            state = new DerivedProperty<bool>(() => Evaluate(content), level, stageNumber);
            unlocked[content] = state;
        }
        return state;
    }

    // 안내 문장용. 행이 없으면 None(그 콘텐츠는 잠겨 있다).
    public UnlockCondition ConditionOf(GameContent content)
    {
        ContentUnlockData data = table != null ? table.Get(content) : null;
        return data != null ? data.Condition : UnlockCondition.None;
    }

    // 표의 값이 바뀌었을 때(에디터의 튜닝 덮어쓰기).
    public void Recompute()
    {
        foreach (DerivedProperty<bool> state in unlocked.Values) state.Recompute();
    }

    private bool Evaluate(GameContent content)
    {
        ContentUnlockData data = table != null ? table.Get(content) : null;
        if (data == null)
        {
            if (reportedMissing.Add(content))
                Debug.LogError($"[ContentUnlocks] ContentUnlockDatabase에 {content}의 행이 없어 잠급니다. 조건이 없으면 conditionType을 None으로 적으세요.");
            return false;
        }

        return data.Condition.IsMetBy(level.Value, stageNumber.Value);
    }
}
