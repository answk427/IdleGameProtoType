using System;

// 스테이지 안내의 로직(docs/COMBAT_ARCHITECTURE.md 10-5). 흐름의 스테이지 번호가 바뀔 때(처음 값 포함) 재생한다.
// 같은 스테이지를 다시 시작하면 값이 같아서 뜨지 않는다. 0은 아직 스테이지에 들어가지 않은 것이다(게임 시작 전).
public sealed class StageAnnouncementPresenter : IDisposable
{
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    public StageAnnouncementPresenter(IStageAnnouncementView view, IReadOnlyReactiveProperty<int> stageNumber)
    {
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (stageNumber == null) throw new ArgumentNullException(nameof(stageNumber));

        view.HideNow();
        stageNumber.Subscribe(number =>
        {
            if (number > 0) view.Play($"Stage {number}");
        }).AddTo(disposables);
    }

    public void Dispose() => disposables.Dispose();
}
