using UnityEngine;

// 모든 창(View)의 베이스. UI 요소 참조와 표시만 갖고 로직은 Presenter가 갖는다.
// 열고 닫기, 정렬, 숨김은 WindowManager가 한다. 창 프리팹에는 Canvas를 넣지 않는다.
//
// 창은 Update를 쓰지 않는다. 숨긴 창(Canvas.enabled = false)에서도 계속 돌기 때문이다.
public abstract class UIWindow : MonoBehaviour
{
    [Header("연출")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);

    public Color DimColor => dimColor;

    public abstract WindowOptions Options { get; }

    // 맨 위 창이 뒤로가기를 먼저 처리할 기회. true면 처리 완료.
    public virtual bool OnBack() => false;

    internal void NotifyInitialized() => OnInitialize();
    internal void NotifyOpening() => OnOpening();
    internal void NotifyClosed() => OnClosed();

    // 인스턴스마다 한 번.
    protected virtual void OnInitialize() { }

    // 열릴 때마다, Presenter가 만들어지기 전. 지난번의 입력 상태(제스처 등)를 비운다.
    protected virtual void OnOpening() { }

    protected virtual void OnClosed() { }
}

public abstract class UIWindow<TArgs> : UIWindow { }

public abstract class UIWindow<TArgs, TResult> : UIWindow { }
