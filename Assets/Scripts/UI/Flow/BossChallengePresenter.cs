using System;
using UnityEngine;

// 보스 도전 버튼의 로직(docs/COMBAT_ARCHITECTURE.md 10-5, docs/UI_DESIGN.md 5-3).
// 보이기는 흐름의 상태(BossChallengeAvailable) 하나가 정하고, 누르면 명령을 보낸다. 버튼이 스스로 숨지 않는다:
// 받아들이면 그 상태가 바로 꺼지고, 전멸·전환·스테이지 입장에서도 꺼지므로 버튼과 상태가 어긋날 수 없다.
public sealed class BossChallengePresenter : IDisposable
{
    private readonly IBossChallengeView view;
    private readonly IEncounterFlow director;
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    public BossChallengePresenter(IBossChallengeView view, IEncounterFlow director)
    {
        this.view = view ?? throw new ArgumentNullException(nameof(view));
        this.director = director ?? throw new ArgumentNullException(nameof(director));

        director.BossChallengeAvailable.Subscribe(view.SetVisible).AddTo(disposables);
        view.Clicked += HandleClicked;
    }

    private void HandleClicked()
    {
        BossChallengeResult result = director.RequestBossChallenge();
        if (result != BossChallengeResult.Accepted) Debug.Log($"[BossChallenge] 보스 도전을 받지 않았습니다: {result}");
    }

    public void Dispose()
    {
        view.Clicked -= HandleClicked;
        disposables.Dispose();
    }
}
