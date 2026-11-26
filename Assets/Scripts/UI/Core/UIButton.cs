using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 버튼 소리 종류. 소리 자체(클립)는 UISoundSet 에셋에 있다.
public enum UIButtonSound
{
    None,
    Click,
}

// 화면 UI의 모든 버튼(docs/UI_DESIGN.md 10-6). 그냥 Button은 쓰지 않는다(UIButtonUsageTests가 프리팹을 훑는다).
// Button을 상속해서 직렬화 필드가 같다. 그래서 기존 Button의 스크립트만 바꾸면 설정과 참조가 그대로 남는다.
//
// - 한 프레임에 클릭 하나(UIClickGate): 모든 UIButton이 같은 규칙을 지난다.
// - 소리: 받아들인 클릭을 UIButtonEvents로 내보내고, 재생은 UISoundPlayer 한 곳이 한다.
// - Available(모양만 불가): 꺼진 것처럼 보이지만 클릭은 전달한다. 명령이 실패 이유를 돌려주고 Presenter가 토스트로 알린다
//   (10-1 "UI는 이유별로 안내한다"). 정말 눌리면 안 될 때만 interactable을 끈다.
public class UIButton : Button
{
    [SerializeField] private UIButtonSound sound = UIButtonSound.Click;

    private bool available = true;

    public UIButtonSound Sound
    {
        get => sound;
        set => sound = value;
    }

    public bool Available
    {
        get => available;
        set
        {
            if (available == value) return;
            available = value;
            DoStateTransition(currentSelectionState, true);
        }
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (!TryAcceptClick()) return;

        base.OnPointerClick(eventData);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (!TryAcceptClick()) return;

        base.OnSubmit(eventData);
    }

    // 눌리지 않을 클릭(꺼진 버튼)은 프레임을 차지하지 않는다. 같은 프레임의 다른 버튼이 받을 수 있다.
    private bool TryAcceptClick()
    {
        if (!IsActive() || !IsInteractable()) return false;
        if (!UIClickGate.TryAccept(Time.frameCount)) return false;

        UIButtonEvents.RaiseClicked(sound);
        return true;
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(available ? state : SelectionState.Disabled, instant);
    }
}
