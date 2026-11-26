using System.Collections.Generic;

// 화면 UI의 지금 상태(읽기 전용 사본, docs/UI_DESIGN.md 10-6). 개발자 도구의 UI 상태 표시가 읽는다.
// 게임 쪽은 채워 주기만 하고 무엇을 보일지는 모른다. 목록은 다시 쓴다(매 프레임 새로 만들지 않는다).
public sealed class UIDebugSnapshot
{
    public readonly struct WindowInfo
    {
        public readonly string Name;
        public readonly bool IsLoaded;   // false면 로드 중(구분을 아직 모른다)
        public readonly UISortBand Band;
        public readonly int Order;
        public readonly bool IsOpen;     // false면 여는 중(Presenter 전)
        public readonly bool Modal;

        public WindowInfo(string name, bool isLoaded, UISortBand band, int order, bool isOpen, bool modal)
        {
            Name = name;
            IsLoaded = isLoaded;
            Band = band;
            Order = order;
            IsOpen = isOpen;
            Modal = modal;
        }
    }

    public readonly struct BlockInfo
    {
        public readonly string Reason;
        public readonly float Seconds;   // 걸린 뒤 지난 시간(화면 시간)

        public BlockInfo(string reason, float seconds)
        {
            Reason = reason;
            Seconds = seconds;
        }
    }

    // 스택 순서(0이 바닥, 마지막이 맨 위 = 뒤로가기를 먼저 받는 창).
    public readonly List<WindowInfo> Windows = new List<WindowInfo>();

    // 공용 딤의 sortingOrder. 딤이 없으면 null.
    public int? DimOrder;

    // 입력 차단을 쥔 이유들(걸린 순서).
    public readonly List<BlockInfo> Blocks = new List<BlockInfo>();

    // 지금 보이는 토스트 글. 없으면 null.
    public string Toast;

    public void Clear()
    {
        Windows.Clear();
        Blocks.Clear();
        DimOrder = null;
        Toast = null;
    }
}
