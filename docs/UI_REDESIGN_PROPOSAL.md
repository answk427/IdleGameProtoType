# UI_DESIGN 보완안 (2026-11-22, 재설계 초안을 철회하고 다시 씀)

**기준은 [UI_DESIGN](UI_DESIGN.md)이다. 구조와 11장의 결정은 바꾸지 않는다.** 이 문서는 그 위에 더할 정정 2개와 보완 10개의 제안이며, **A1, A2, B1~B7, B10은 승인되어 UI_DESIGN에 반영됐고 4장, 5장대로 슬라이스가 구현됐다. 남은 것은 B8, B9뿐이다**(세이브 오류 창을 만들 때 확인). 그때 이 파일은 지운다.

같은 날 쓴 첫 초안은 "창 시스템의 바닥을 다시 설계한다"고 했으나 틀렸다. 대안이 동작하는지만 검사하고, 원안을 유지한 채 고칠 수 있는지는 검사하지 않았기 때문이다. 원안으로 다시 시험하니 구조 변경이 필요한 곳이 없었다.

근거 표기: **[재현]** 사본에서 실행 · **[코드]** 코드/문서를 읽어 확인 · **[미확인]**

## 1. 정정 2개 (문장이 사실과 다름)

| | 위치 | 수정 | 이유 | 시점 |
|---|---|---|---|---|
| A1 | 5-3 | "슬롯 6개" → "슬롯 5개(`SkillSlotCount`)" | 코드가 5개 [코드] | 슬라이스 |
| A2 | 5-5 | "깊이 제한" → "알림 한 번이 일으킨 총 전달 수 제한" | 구독자 둘이 값을 바꾸면 알림이 갈라져 깊이 17에서 30만 번을 넘는다. 100에 닿기 전에 멈춘다 [재현]. 개발 빌드 한정이라는 결정은 유지 | 슬라이스 전 |

## 2. 보완 10개 (결정은 그대로, 빠진 규칙만 추가)

| | 위치 | 추가할 규칙 | 없으면 | 시점 |
|---|---|---|---|---|
| B1 | 4-1 | `overrideSorting`은 WindowManager가 창을 활성화한 뒤 설정 | 비활성 상태의 설정은 무시된다 [재현] | 슬라이스 |
| B2 | 4-1 | Fx Canvas에는 GraphicRaycaster를 두지 않음 | 데미지 숫자가 터치를 가로챈다 [재현] | 슬라이스 |
| B3 | 6-3 | 숨길 때 자식 Canvas와 Raycaster까지 끄고 `CanvasGroup.interactable=false` | `+1~+8`칸의 자식이 터치를, 숨은 버튼이 Submit을 받는다 [재현] | 슬라이스 |
| B4 | 6-3 | `Closed`는 읽을 때마다 새 `Awaitable`을 줌 | 같은 `Awaitable`을 두 번 await하면 예외 [재현] | 슬라이스 |
| B5 | 6-3 | 인자 타입이 자기 창을 선언(`IWindowArgs<TWindow>`) | 다른 창의 인자를 넘겨도 막을 방법이 문서에 없다 [코드] | 슬라이스 |
| B6 | 5-7 | Presenter 생성 함수는 UIRoot에서 창 타입별로 등록 | Presenter를 누가 만드는지 문서에 없다 [코드] | 슬라이스 |
| B7 | 7 | 코루틴 지역변수로 `using`을 쓰지 않음. `async` + `destroyCancellationToken`이거나 객체 필드로 보유 | 코루틴이 중단되면 해제되지 않는다 [재현] | 슬라이스 |
| B8 | 6-4 | 스택에는 정렬 구분 순서를 지켜 삽입. Exclusive는 닫을 수 없는 System 창을 남김 | 오류창 밑의 창이 뒤로가기로 닫히거나, 탭 전환이 오류창을 닫는다 [코드] | 첫 System 창 |
| B9 | 3, 10-2 | 로드 실패 시에는 세션 없이 UIRoot만 준비해 오류창 표시 | 실패를 보여 줄 UI가 없다 [코드] | 세이브 오류창 |
| B10 | 9 | SafeArea는 콘텐츠 루트에만 적용. 딤과 전체 화면 배경은 제외 | 노치 쪽에 어두워지지 않은 띠가 남는다 [코드] | 3차 (**UI_DESIGN에 반영됨**) |

## 3. 철회한 첫 초안의 변경

| 첫 초안 | 유지하는 원안 | 철회 이유 |
|---|---|---|
| 창마다 루트 Canvas | 창마다 중첩 Canvas + `overrideSorting` | B1이면 값이 유지되고 터치 순서도 맞다 [재현] |
| 스택을 없애고 그리는 순서로 통일 | 스택과 정렬 구분 분리 | B8이면 스택 맨 위와 화면 맨 위가 일치한다 [재현: 규칙 시뮬레이션] |
| 딤 칸 재배치 | `−1` 딤, `+1~+8` | 원안의 칸은 겹치지 않는다 [재현]. 첫 초안의 계산 착오 |
| 정렬 구분 2개 추가 | 5개 | 페이드와 차단은 이미 System에 정의돼 있다 |
| `SetActive(false)`로 숨김 | `Canvas.enabled` | B3이면 터치와 Submit이 막힌다 [재현] |
| `WindowDefinition`으로 옵션 이동 | `UIWindow`의 가상 프로퍼티 | 11장에서 이미 결정. B6만 있으면 된다 |
| `Task`로 교체 | `Awaitable` | B4면 여러 번 기다릴 수 있다 [재현] |
| `Open(request)`로 API 교체 | `Open<SkillManageWindow>(args)` | B5면 원안 그대로 컴파일되고 잘못된 인자는 컴파일 에러다 [재현] |
| `OnBack`, `Tick`, Exclusive, 스피너 제외 | 모두 1차 범위 | 근거 없이 범위를 줄였던 것 |
| 관리 팝업의 쿨다운 표시 제거 | `Tick()`으로 폴링 | 원안이 이 경우를 위해 만든 장치다 |
| 릴리스에서도 재진입 가드 | 개발 빌드만 | 바꿀 근거가 없다 |
| 저장과 상태 분리를 새 결정으로 | 5-3의 상태/사건 구분 | 원안에 있다. 구현 시 저장을 상태에 구독하지 않으면 된다 |
| 뒤로가기 입력 규칙 이관 | 6-4 | 이미 같은 내용이 있다 |
| "팝업이 탭바를 막는가"를 새로 질문 | `ModalPopup` | 이미 결정돼 있다 |

## 4. 창 API (원안 모양, 컴파일 확인)

```csharp
public interface IWindowArgs<TWindow> where TWindow : UIWindow { }                      // B5
public interface IWindowArgs<TWindow, TResult> where TWindow : UIWindow { }

public abstract class UIWindow : MonoBehaviour
{
    public abstract WindowOptions Options { get; }
    public virtual bool OnBack() => false;
}
public abstract class UIWindow<TArgs> : UIWindow { }
public abstract class UIWindow<TArgs, TResult> : UIWindow { }

public interface IWindowPresenter : IDisposable { void Tick(); }
public interface IWindowContext { void Close(); }
public interface IWindowContext<TResult> { void Close(TResult result); }

public sealed class WindowHandle    { public Awaitable<WindowStatus> Closed { get; } }  // B4
public sealed class WindowHandle<T> { public Awaitable<WindowResult<T>> Closed { get; } }

public sealed class WindowManager
{
    // B6
    public void Bind<TWindow, TArgs>(Func<TWindow, TArgs, IWindowContext, IWindowPresenter> create)
        where TWindow : UIWindow<TArgs> where TArgs : IWindowArgs<TWindow>;
    public void Bind<TWindow, TArgs, TResult>(Func<TWindow, TArgs, IWindowContext<TResult>, IWindowPresenter> create)
        where TWindow : UIWindow<TArgs, TResult> where TArgs : IWindowArgs<TWindow, TResult>;

    public WindowHandle Open<TWindow>(IWindowArgs<TWindow> args) where TWindow : UIWindow;
    public WindowHandle<TResult> Open<TWindow, TResult>(IWindowArgs<TWindow, TResult> args) where TWindow : UIWindow;
    public bool Back();
}
```

```csharp
windows.Open<SkillManageWindow>(new SkillManageArgs());                     // UI_DESIGN 6-3 그대로
WindowResult<bool> answer = await windows.Open(new ConfirmArgs()).Closed;   // 결과가 있는 창
```

## 5. 기존 UIManager와 공존하는 동안

UI_DESIGN은 최종 구조만 정하므로 이 절은 이관 기간에만 쓰는 임시 규칙이다.

| 항목 | 방식 | 이유 |
|---|---|---|
| 화면 주인 | SkillManage만 WindowManager, 나머지는 UIManager | 한 화면에 주인이 둘이면 상태가 어긋남 |
| 그리는 순서 | UIRoot 루트 Canvas order 1, 기존 UIManager 0. 창은 그 안의 중첩 Canvas | 원안 구조 그대로 기존 탭바를 막음 [재현: 터치] |
| 열기 | `SkillSlotBar`가 요청 사건 → UIRoot가 `Open` | 옛 코드가 새 시스템을 모르게 |
| 탭 전환 | `UIManager.OnTabChanged` → 팝업 닫기 | 탭 이관 전까지 Exclusive 규칙을 대신함 |
| 뒤로가기 | WindowManager 먼저, 처리하지 않으면 UIManager | 새 창이 항상 위 |
| 페이드 | 원안대로 System 구분으로 이동 | 그대로 두면 팝업(3000) 밑에 깔림 |
| 옛 팝업 경로 | `UIPopup`과 `Overlay` 분기 삭제 | 사용처가 없어지고 스택 두 개가 섞이지 않음 |

프리팹의 anchor를 씬 override 값(0.15~0.85)으로 맞춘 뒤 씬 인스턴스를 지운다. 안 맞추면 Registry 로드 시 팝업 폭이 줄어든다 [재현].

## 6. UI 설계와 별개로 발견한 것

| 문제 | 근거 | 처리 제안 |
|---|---|---|
| 손상/불완전 세이브가 기본값으로 로드된 뒤 덮어써짐 | 재현 | **수정됨** (`SaveDataValidator`) |
| 플레이 중 Reset Save가 종료 때 되살아남 | 재현 | **수정됨** (플레이 중 메뉴 비활성) |
| 슬롯을 옮기면 쿨다운 0 | 재현 | 별도 수정 |
| 사망 후 보스 버튼이 남음 | 재현 | 5-3의 `BossAvailable` 상태 전환 때 해결 |
| `.asset`과 JSON 불일치(몬스터 2002, 스킬 6, 1스테이지 보스) | 코드 | 원본 엑셀 확인 전 재동기화 금지 |

## 7. 검증 범위

- 원안 + 보완 규칙 검사 19개 통과, 잘못된 인자의 컴파일 에러, 4장 API 컴파일.
- 배치 모드라 그려진 화면은 [미확인]. 중첩 Canvas의 화면상 순서는 `renderOrder` 값과 터치 순서로만 확인했다.
- Android/WebGL, 성능, 슬라이스 전체 통합은 [미확인].
