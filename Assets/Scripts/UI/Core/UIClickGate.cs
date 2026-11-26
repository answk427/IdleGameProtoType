using UnityEngine;

// UI 버튼 클릭은 한 프레임에 하나만 받는다(docs/UI_DESIGN.md 10-6). 모든 UIButton이 지나는 규칙 하나.
//
// 막는 틈: 씬의 EventSystem은 손가락마다 따로 처리한다. 첫 손가락의 클릭이 모달·입력 차단막을 켜도, 같은 프레임에는 새로 켠
// 그래픽이 아직 레이캐스트에 잡히지 않아서(그려지기 전이라 depth -1) 둘째 손가락이 그 밑의 버튼을 누를 수 있다.
// 다음 프레임부터는 차단막과 딤이 터치를, sendNavigationEvents가 키보드를 막는다. 그래서 버튼마다 입력 차단을 묻지 않는다.
//
// 시간 간격 연타 방지는 두지 않는다. 명령은 모델이 검사하고(두 번째 레벨업·학습은 실패로 돌아온다), 창 열기는 AlreadyOpen과
// 입력 차단이 막는다. 업그레이드처럼 빠르게 누르는 버튼은 그대로 빠르게 눌려야 한다.
public static class UIClickGate
{
    private static int acceptedFrame = -1;

    // 이 프레임의 첫 클릭이면 true(그 프레임을 차지한다).
    public static bool TryAccept(int frame)
    {
        if (frame == acceptedFrame) return false;
        acceptedFrame = frame;
        return true;
    }

    // 플레이를 시작할 때 자동으로 불린다(테스트도 쓴다).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset() => acceptedFrame = -1;
}
