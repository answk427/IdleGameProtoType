using UnityEngine;

// 버튼 클릭 → 바깥(소리) 방향의 정적 사건(docs/UI_DESIGN.md 5-3의 정적 버스 예외, 10-6).
//
// 버튼은 프리팹과 View가 만들고(목록 항목은 실행 중에 생긴다), HUD 버튼은 UIRoot 밑에 있지 않아서 재생기를 넘겨줄 길이 없다.
// 그래서 정적 사건으로 내보낸다(정적 버스는 원칙의 예외, 지금 남은 것은 이것 하나다. 전투의 GlobalCombatEvents는 5-A에서
// 월드의 사건 배달로 바꿔 없앴다). 밖으로 내는 사건만 정적이다: 버튼이 정적으로 무엇을 묻는(입력 차단 등) 길은 두지 않는다.
public static class UIButtonEvents
{
    private static Subject<UIButtonSound> clicked = new Subject<UIButtonSound>();

    // 클릭이 받아들여졌다(한 프레임 하나 규칙을 지난 것만). 값은 그 버튼의 소리 종류.
    public static Observable<UIButtonSound> Clicked => clicked;

    public static void RaiseClicked(UIButtonSound sound) => clicked.OnNext(sound);

    // 구독자를 모두 버린다. 플레이를 시작할 때 자동으로 불린다(테스트도 쓴다).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetSubscribers() => clicked = new Subject<UIButtonSound>();
}
