# UI 시스템 설계

화면 UI의 뼈대 설계 문서. 대화로 검토하며 확정한 내용과, 도중에 뒤집힌 결정과 그 이유를 함께 남긴다.
**상태: 설계 확정. 0단계와 1차 수직 슬라이스(스킬 관리 창) 구현 완료, 나머지 1차 이관 전.** `refactor/ui-stack` 브랜치에 남아 있는 `UIBase`/`UIManager`(HUD, 탭)는 이 설계 이전 단계의 결과물이며, 나머지 1차 이관에서 대체된다.

## 1. 목표와 원칙

- 상용 게임을 서비스하는 회사 수준의 구조를 목표로 한다. 단, **기능을 다 만드는 것이 아니라 나중에 붙일 자리가 구조에 있는 것**을 기준으로 삼는다. 지금 쓰지 않는 것은 만들지 않는다.
- 개별 증상(예: 버튼 연타)을 그 자리에서 막지 않고, 같은 종류의 문제를 모두 해결하는 시스템(예: 입력 차단)으로 푼다.
- 나중에 바꾸면 비용이 커지는 결정(모델 API, 창 API 모양)은 지금 정한다.

## 2. 프레임워크: uGUI

UI Toolkit도 검토했으나 uGUI를 유지한다.

- 방치형은 이펙트가 많은 UI가 핵심인데, UI Toolkit은 UI 위 파티클/이펙트 처리가 약하고 월드 스페이스 지원 사례가 적다.
- 기존 프리팹이 전부 uGUI이고, 모바일 게임 업계에서 가장 검증된 방식이다.
- UI Toolkit의 최대 장점인 내장 데이터 바인딩은 자체 반응형 타입(5장)으로 대체한다.
- 대신 **Presenter가 uGUI 타입(`Image`, `Button` 등)을 모르게** View를 인터페이스 뒤에 둔다. 프레임워크를 바꿔도 View만 다시 만들면 된다.

## 3. 전체 구조

```
GameInitializer (조립 지점 = Composition Root, 씬 수명) — 생성 순서를 한 곳에서 결정
  1. 세이브 로드
  2. GameSession 생성 (PlayerStats, GoldWallet, 스테이지 진행 — 10-2)
  3. 플레이어 캐릭터 스폰 (모델을 주입받음)
  4. UIRoot 조립 (모델/서비스를 주입)

UIRoot (화면 UI의 유일한 진입점)
  ├ HUD 위젯          씬에 배치. PlayerHud, TabBar, BossButton
  ├ WindowManager    창 로드/열기/닫기, 스택, 정렬 순서, 뒤로가기, 열린 창 Tick
  └ InputBlocker     참조 카운트 입력 차단 + 지연 스피너

창 하나의 구성
  View (MonoBehaviour, 프리팹)  ←값 표시 / 사용자 이벤트→  Presenter (순수 C#)
                                                              ↑ 명령     ↓ 상태·사건 구독
                                                                  모델 / 게임 서비스
```

월드 스페이스 HP바(`HpBar.prefab`)는 UIRoot 밖, 지금처럼 캐릭터에 부착한다.

## 4. 그리는 순서

### 4-1. 정렬 구분: `UISortBand` (코드 enum → sortingOrder 구간)

화면 UI는 Screen Space - Overlay다. Overlay Canvas에는 Sorting Layer가 적용되지 않으므로 **sortingOrder 구간으로만 순서를 정한다.**

| `UISortBand` | 기준값 | 내용 |
|---|---|---|
| Fx | 0 | 데미지 숫자 등 런타임 이펙트 텍스트 (HUD를 덮지 않도록 가장 아래) |
| Hud | 1000 | 상시 위젯 |
| Window | 2000 | 탭 내용 등 창 |
| Popup | 3000 | 모달 팝업 |
| System | 9000 | 입력 차단 스피너, 화면 전환 페이드, 치명 오류 |

- 지금 쓰는 것만 정의하고 간격을 1000으로 넓게 둔다. sortingOrder 범위가 -32768~32767이라 아낄 이유가 없다. 토스트/튜토리얼은 2차에서 4000 근처에 추가한다.
- 창의 sortingOrder = `구분 기준값 + 깊이 × 10`. 창마다 자기 Canvas(`overrideSorting`)를 가진다.
  - **`overrideSorting`은 WindowManager가 활성 상태에서 설정한다.** 비활성 상태에서 넣은 값은 무시되고 order 0으로 남는다. 사람이 프리팹에서 켜는 값이 아니다.
  - **깊이는 같은 구분 안에서 센다.** 창 2개 위에 처음 뜬 팝업은 깊이 0이라 `3000 + 0`이다.
  - **×10은 창 하나가 쓰는 칸이다.** `−1`: 공용 딤(맨 위 모달 바로 아래), `0`: 창 본체, `+1 ~ +8`: 창 안에서 자기 Canvas로 위에 떠야 하는 요소(드롭다운 목록, 툴팁 등).
  - **한도:** 구분마다 깊이 100까지(간격 1000 ÷ 10). 넘으면 한도에서 멈추고 에러 로그를 낸다. 다른 구분을 조용히 침범하는 대신 바로 드러나게 한다.
  - **주의:** uGUI 기본 드롭다운(`Dropdown`/`TMP_Dropdown`)은 펼친 목록용 Canvas를 sortingOrder 30000으로 만드는 것으로 알고 있다(구현 시 확인). 그대로 두면 System 구분까지 뚫고 올라가므로 창의 `+1~+8` 칸으로 맞춰 준다.
- **Fx Canvas에는 GraphicRaycaster를 두지 않는다.** 데미지 숫자가 버튼과 팝업의 터치를 가로채지 않게 한다. 텍스트마다 `raycastTarget`을 끄는 것에 기대지 않는다.
- **런타임에 나타나는 것의 순서는 시스템이 정한다.** 하이어라키 배치 순서에 의존하지 않는다.
- **창 프리팹 내부 요소는 하이어라키 순서를 쓴다.** 디자이너가 저작 시점에 정하는 순서라 정당하다.
- 스택은 "닫는 순서", 정렬 구분은 "그리는 순서"를 정한다. 둘은 분리한다.

### 4-2. 이름이 `UISortBand`인 이유

Unity 용어와 겹치지 않으면서 실체("sortingOrder 숫자의 구간")를 드러내는 이름을 골랐다.

| 후보 | 기각 이유 |
|---|---|
| `UILayer` | 업계에서 가장 흔한 이름이지만 GameObject Layer, Sorting Layer와 충돌. 설계 논의 중 실제로 혼동이 생김 |
| `UISortGroup` | Unity의 `SortingGroup` 컴포넌트와 충돌 |
| `UIDepth` | `Camera.depth`, Z 깊이와 혼동 |
| `UIPlane` | Canvas의 `planeDistance`와 혼동 |

### 4-3. Sorting Layer를 쓰지 않는 이유

- Overlay Canvas에는 적용되지 않는다.
- Camera 모드였더라도: 프로젝트 전역 설정(`TagManager.asset`)에 UI 내부 구분이 섞이고, 코드가 문자열/ID로 참조해서 오타나 삭제 시 에러 없이 `Default`로 떨어진다. enum + sortingOrder 구간은 같은 2단계 비교를 컴파일러 검사 아래에서 한다.
- Sorting Layer가 필요한 곳은 월드 렌더링(배경/캐릭터/이펙트/HP바)이며 [TODO](TODO.md)에 별도로 기록했다.

### 4-4. 창마다 Canvas를 두는 비용

Canvas끼리는 배칭이 합쳐지지 않으므로 **보이는 Canvas마다 최소 1개의 배치가 늘어난다.** 의도적인 교환이다.

- 하나로 합쳐도 1번이 되지 않는다. TMP 텍스트와 이미지가 번갈아 겹치면 Canvas 안에서도 배치가 끊기고, 겹친 창끼리는 순서를 지켜야 해서 합쳐지지 않는다.
- 숨긴 창(`Canvas.enabled = false`)은 비용이 0이다. 동시에 보이는 Canvas는 5~6개 수준이다.
- 대신 얻는 것: **리빌드 범위 축소.** 하나의 큰 Canvas면 골드 숫자 하나가 바뀔 때 열린 모든 창의 메시를 다시 만든다.
- 오버드로(전체 화면 배경/딤 중첩)는 Canvas 분리와 무관하며, "전체 화면 창이면 아래 창 숨김" 옵션으로 해결한다.
- 1차 구현 후 Frame Debugger와 Profiler UI 모듈로 실측한다.

## 5. 데이터 흐름

### 5-1. 상태와 사건, 그리고 반응형

- **상태:** 언제 물어봐도 현재 값이 있는 것 (골드, 레벨, HP).
- **사건:** "방금 일어난 일". 현재 값이라는 개념이 없다 (몬스터 사망, 레벨업 순간).
- 판단 기준 한 줄: **화면이 보여주는 게 "지금 값"이면 상태, "방금 일어난 일"이면 사건.** 같은 골드에서도 상단 골드 숫자는 상태, 떠오르는 "+100 골드"는 사건이다.

C# 이벤트만으로 상태를 다루면 사람이 기억해야 하는 지점이 세 곳 생긴다: 값을 바꾸는 모든 경로에서 알림 호출, 구독 직후 현재 값으로 한 번 그리기, 닫을 때 해제. 빠뜨려도 컴파일 에러가 없고 화면에 틀린 값이 조용히 보인다. (예: 현재 `PlayerHud`는 구독 5줄, 직접 그리기 4줄, 해제 5줄이 서로 짝이 맞아야 한다.)

`ReactiveProperty<T>`는 **값 + 이벤트를 묶은 작은 타입**이다. 값이 바뀌면 자동으로 알리고, 구독하는 순간 현재 값을 한 번 주고, 같은 값은 무시하고, 구독 해제 손잡이(`IDisposable`)를 돌려준다. 여러 상태를 조합한 파생 값(예: `CanLevelUp = exp >= requiredExp`)도 선언 한 줄로 만들 수 있다.

### 5-2. 하나의 구독 모양으로 통일

종류 구분 자체는 없앨 수 없다(사건을 반응형 값으로 만들면 구독 시 지난 사건이 재생되고, 같은 값이 연속되면 누락된다). 대신 **구독하는 쪽의 모양을 통일한다.**

```
Observable<T>                 공통 모양: Subscribe(콜백) → IDisposable
├─ ReactiveProperty<T>        상태: .Value 있음, 구독 시 현재 값 줌, 같은 값은 무시
└─ Subject<T>                 사건: 현재 값 없음, 구독 이후 일어난 것만, 같은 값도 매번
```

```csharp
// 모델 — 종류는 여기서 단 한 번 정함
public IReadOnlyReactiveProperty<int> Gold => gold;       // 상태
public Observable<int> GoldGained => goldGained;          // 사건

// UI — 종류와 상관없이 같은 모양
wallet.Gold.Subscribe(view.SetGold).AddTo(disposables);
wallet.GoldGained.Subscribe(view.PlayGainEffect).AddTo(disposables);
```

- 구독하는 쪽(창 수십 개)은 고를 것이 없다. 사건도 `IDisposable`을 주므로 해제 누락이 사라진다.
- 종류를 고르는 곳은 모델 정의 한 곳뿐이다.
- 타입이 잘못된 사용을 막는다: 사건에는 `.Value`가 없고, 외부에 읽기 전용으로 노출된 상태는 밖에서 바꿀 수 없다.

### 5-3. 적용 범위 규칙

```
Q1. 아무 일이 없어도 시간이 흐르는 것만으로 계속 바뀌나?   예 → 매 프레임 값 (폴링)
Q2. "지금 값이 뭐야?"에 답이 있나?                         아니오 → 사건 (Subject)
Q3. 다른 상태들로부터 계산되나?                            예 → 파생 상태 / 아니오 → 상태
```

| 종류 | 예 | 수단 |
|---|---|---|
| 상태 | Level, Exp, Gold, Hp, 업그레이드 단계, 장착 슬롯, BossAvailable | `ReactiveProperty<T>` (외부엔 읽기 전용) |
| 파생 상태 | CanLevelUp, 항목별 구매 가능, RedDot | 원본에서 계산되는 읽기 전용 값 |
| 사건 | 레벨업 순간, 골드 획득, 몬스터 사망, 스테이지 전환 | `Subject<T>` |
| 매 프레임 값 | 스킬 쿨다운 비율, 보스 제한 시간 | 폴링 (원인은 상태로: "쿨다운 끝나는 시각") |

- **모음 데이터**(배운 스킬, 슬롯 5개 = `PlayerStatsSaveData.SkillSlotCount`)는 목록 전체를 새 목록으로 교체하는 방식(`ReactiveProperty<IReadOnlyList<T>>`). 항목 단위 반응형 컬렉션은 이 규모에서 얻는 것이 없다.
- **파생 상태의 위치:** 게임 규칙(레벨업 가능, 구매 가능, 해금)은 모델/게임 서비스에, 표시용 가공(`"EXP 45.20%"`, `"1.2K"`)은 Presenter에.
- **상태와 사건을 겸하던 이벤트는 둘로 나눈다.** `OnLevelUp` → `Level` + `LevelReached`, `OnStageChanged` → `CurrentStageNumber` + `StageChanged`.
- **재분류: 보스 도전 가능.** 지금은 `OnBossChallengeAvailable` 사건이지만 "지금 도전 가능한가?"에 답이 있으므로 상태다(`StageManager.BossAvailable`이 이미 존재). 버튼은 `BossAvailable && !보스전 중`을 구독해 켜지고 꺼지기만 하며, 클릭 시 스스로 `Hide()`하던 코드가 사라진다.
  - **구현(2026-11-25, 전투 4-A):** 주인은 흐름(`EncounterDirector.BossChallengeAvailable`)이다. 도전을 받는 순간, 전환이 시작될 때, 스테이지에 들어갈 때 꺼지므로 "보스전 중"을 따로 볼 필요가 없다. 버튼은 View + Presenter(`BossChallengePresenter`)이고 누르면 명령(`RequestBossChallenge`)의 결과를 받는다([COMBAT_ARCHITECTURE.md](COMBAT_ARCHITECTURE.md) 10절).
- **적용 경계:** 시스템 경계를 넘는 알림(모델 → UI, `GlobalGameEvents`/`GlobalCombatEvents` 이벤트 버스)은 전부 이 타입들로 통일한다. 한 오브젝트 안의 내부 신호(애니메이션 이벤트 `OnHitEvent` 등)는 C# 이벤트로 둔다.
- **정적 버스에는 사건만 둔다. 상태는 그 상태의 주인이 `ReactiveProperty`로 갖는다.** 정적 버스에 상태를 두면 어디서든 읽고 쓰는 전역 변수가 되어 정적의 단점이 커진다. 현재 버스 항목의 분류:

  | 현재 항목 | 종류 | 갈 곳 |
  |---|---|---|
  | `OnMonsterDied`, `OnAnyTargetDamaged`, `OnHealed`, `OnBossKilled`, `OnStageCleared`, `OnPlayerDied`, `OnStageChanged`(전환 순간) | 사건 | 버스에 `Subject`로 (이름은 과거형: `MonsterDied` 등) |
  | `OnScrollChanged(bool)` | 상태 | `GameManager.IsScrolling` → `ReactiveProperty` |
  | `OnBossChallengeAvailable` | 상태 | `StageManager.BossAvailable` → `ReactiveProperty` (4-A: `EncounterDirector.BossChallengeAvailable`) |
  | `OnGameStarted` | **없앰(4-A)** | GameInitializer가 모든 구독을 연결한 뒤 흐름을 시작(`director.Begin`)해서 처음 값이 반응형으로 전달된다. HUD는 조립 끝에 `UIRoot`가 켠다 |

  4-A 결과: 흐름 쪽 항목(`OnBossKilled`, `OnStageCleared`, `OnPlayerDied`, `OnStageChanged`)은 버스로 옮기지 않고 없앴다. 흐름이 지난 단계의 결과를 월드에 직접 묻고(처치, 전멸), 스테이지는 `EncounterDirector`의 상태(`CurrentStageNumber`)와 사건(`StageEntered`)이 됐다. `GlobalGameEvents`는 삭제했다. `GlobalCombatEvents`의 `Subject`화는 4-B다.

- **정적 이벤트 버스는 원칙의 예외로 유지한다.** "예외"는 접근 방식(주입 대신 정적)에 대한 것이고, 알림 수단은 다른 경계 알림과 같이 반응형(`Subject`)이다. `GlobalGameEvents`/`GlobalCombatEvents`는 GameInitializer가 넘겨주는 대신 정적으로 남긴다(풀에서 생성되는 몬스터까지 참조를 넘기는 비용 대비 얻는 것이 적다). 대신 `[RuntimeInitializeOnLoadMethod]`로 플레이 시작 시 구독자를 비운다. 도메인 리로드를 끄면(Enter Play Mode Options) 정적 상태가 플레이 세션 사이에 남아, 지난 플레이의 파괴된 구독자에게 알림이 가기 때문이다. 현재 프로젝트 설정은 도메인 리로드를 하므로 지금은 안전하지만 설정이 바뀌어도 안전하게 해 둔다.

### 5-4. 이름 규칙

종류를 잘못 고르는 실수는 이름에서 드러나게 한다.

- 상태는 명사/형용사: `Gold`, `Level`, `BossAvailable`, `CanLevelUp`
- 사건은 과거형: `GoldGained`, `LevelReached`, `MonsterDied`, `StageChanged`
- 모델을 정의하거나 리뷰할 때 확인 질문: "지금 값이 있나?"

### 5-5. 반응형 타입 구현 규약

- **자체 구현, R3와 같은 모양.** `Observable<T>`, `ReactiveProperty<T>`, `Subject<T>`, `.Value`, `Subscribe()` → `IDisposable`, `AddTo()`. 시간 기반 연산자(디바운스 등)가 필요해지면 R3로 옮기기 쉽게 하기 위해서다. 직접 만드는 이유: 필요한 범위가 작고, R3는 NuGet 경유 설치라 Unity 도입에 마찰이 있으며 기능 대부분이 불필요하다.
- **알림 중 구독/해제:** 알림은 구독자 목록의 복사본을 순회한다. 콜백 안에서 구독을 해제해도 순회가 깨지지 않는다.
- **알림 중 값 변경(재진입):** 콜백 안에서 같은 속성의 값을 바꾸면 그 변경은 현재 알림이 끝난 뒤 이어서 알린다. 무한 루프는 개발 빌드에서 알림 한 번이 이어서 일으킨 **총 전달 수**(1000)로 감지해 에러를 낸다. 깊이로는 못 잡는다 — 구독자 둘이 각각 값을 바꾸면 알림이 갑절로 늘어 깊이 100에 닿기 전에 멈춘다.
- **예외 격리:** 한 구독자의 예외가 다른 구독자 알림을 막지 않는다. 예외는 로그로 남긴다.

### 5-6. 저장 DTO는 반응형으로 바꾸지 않는다

`PlayerSaveData`/`PlayerStatsSaveData`는 평범한 필드로 유지한다. `JsonUtility`는 필드 이름으로 매칭하므로 구조를 바꾸면 기존 세이브가 조용히 초기화된다(CLAUDE.md "직렬화 함정"과 같은 종류). 모델이 로드 시 DTO 값을 반응형 값에 옮겨 담고, 저장 시 반대로 꺼낸다.

### 5-7. View / Presenter (Passive View)

- **View:** UI 요소 참조만 갖는다. 값을 받아 표시하고 사용자 이벤트를 내보낸다. 로직 없음.
- **Presenter:** 순수 C#. 서비스를 생성자로 주입받는다(`GameManager.Instance`를 찾지 않는다). 모델 상태/사건을 구독해 View에 넣고, 사용자 입력을 모델 명령으로 바꾼다.
- **UI는 모델을 직접 바꾸지 않는다.** 명령을 보내고 결과를 받는다. 명령은 동기 + 실패 이유를 담은 결과 반환(10-1).
- **구독은 창 수명에 묶는다.** 창마다 `IDisposable` 묶음을 두고 닫힐 때 일괄 해제한다.
- **Presenter 생성:** 창 타입별 생성 함수를 UIRoot가 WindowManager에 등록한다(`Bind`). View는 자기 Presenter를 모른다. Presenter는 열 때마다 새로 만들고 닫을 때 버린다.
- **의존성 주입:** DI 프레임워크 없이 GameInitializer 하나가 서비스를 만들어 넘긴다. 규모가 커지면 VContainer 등으로 옮길 수 있는 모양이다.
- **테스트:** Presenter가 순수 C#이라 구조상 단위 테스트가 가능하다. 단, 이 프로젝트는 명시적 요청 없이 새 테스트를 작성하지 않는다는 규칙을 따른다.

### 5-8. 게임 규칙은 게임 쪽이 소유

해금 조건("스테이지 3 클리어 시 장비 탭 해금"), 레드닷 조건("레벨업 가능")은 게임 규칙이므로 게임 쪽 서비스가 판단하고, UI는 그 결과(반응형 값)를 구독만 한다.

## 6. 창 (Window)

### 6-1. 옵션: 구조는 코드, 연출은 프리팹

창의 성격은 서로 독립적인 여러 축이라 서브클래스로 조합하면 클래스 수가 곱으로 늘어난다. 그래서 `UIWindow` 베이스 하나 + `WindowOptions`로 표현한다. 축마다 **"이 값을 바꾸면 코드의 가정이 깨지는가?"**로 위치를 정한다.

| 축 | 위치 |
|---|---|
| 정렬 구분, 스택 방식, 모달, 바깥 터치로 닫기, 뒤로가기 처리, 전체 화면, 캐시 정책 | 코드 (가상 프로퍼티 + 프리셋) |
| 딤 색/투명도, 열기/닫기 애니메이션, 시간 | 프리팹 (직렬화) |

```csharp
public class SkillManageWindow : UIWindow<SkillManageArgs>
{
    public override WindowOptions Options => WindowOptions.ModalPopup;   // 프리셋 + 필요시 수정
}
```

- 구조를 프리팹에 두지 않는 이유: 동작이 프리팹 YAML에 숨어 코드 리뷰에서 안 보이고, 코드와 프리팹이 에러 없이 어긋날 수 있으며, 필드 이름 변경 시 직렬화 함정이 생긴다.
- `WindowRegistry`에 두지 않는 이유: Registry는 "어떤 프리팹을 어떻게 로드하나"만 담당한다.
- 어트리뷰트 방식(`[Window(...)]`)도 검토했다. 로드 전에 타입만으로 옵션을 알 수 있다는 장점이 있지만, 로드 중에는 InputBlocker가 입력을 막으므로 결정적이지 않고, 프리셋 재사용이 어려워 기각.

### 6-2. 로드 규칙

- **HUD:** 항상 떠 있으므로 씬에 배치한다.
- **창(탭 내용, 팝업):** `WindowRegistry`(ScriptableObject, 창 타입 → 프리팹)로 필요할 때 생성한다. 로더는 인터페이스로 두어 Addressables로 교체 가능하게 한다.

### 6-3. 수명 주기와 API

- 로드 → 초기화(1회) → 열림(타입 있는 인자 받음) → 열림 완료(전환 후) → 닫힘 → 닫힘 완료 → 캐시 또는 파괴.
- 인자는 창이 선언한다: `Open<SkillManageWindow>(new SkillManageArgs(...))`. 인자 타입이 자기 창을 가리키므로(`SkillManageArgs : IWindowArgs<SkillManageWindow>`) 다른 창의 인자를 넘기면 컴파일되지 않는다.
- **결과와 닫힘 대기.** 열기는 핸들을 돌려주고, 핸들로 닫힐 때까지 기다리거나(`await handle.Closed`) 타입 있는 결과를 받는다(`UIWindow<TArgs, TResult>`). 확인창(`bool ok = await dialogs.Confirm(...)`)과 시작 팝업 순서(오프라인 보상 → 공지 → 출석: 하나 닫히면 다음)가 이것으로 해결된다. 순서 처리는 호출하는 쪽의 연속된 `await`로 한다. 아래 "요청 큐 없음"과는 다른 것이다 — 큐는 경쟁 상태 방지 장치였고, 이건 의도한 순서대로 보여 주는 기능이다.
- API는 비동기 모양(Unity 6 내장 `Awaitable`). 지금은 동기 로드라도, Addressables/전환 애니메이션이 들어와도 호출부가 바뀌지 않게 한다.
- **`Closed`는 읽을 때마다 새 `Awaitable`을 준다.** `Awaitable`은 풀에서 재사용돼 같은 인스턴스를 두 번 await하면 예외가 난다. `await handle.Closed`는 여러 곳에서 여러 번 써도 되고, 꺼낸 `Awaitable`을 변수에 담아 두 번 await하지는 않는다.
- **요청 큐는 두지 않는다.** 경쟁 상태의 대부분은 사용자 연타이며, 창 전환 중 InputBlocker로 막는다. 나머지는 창별 상태(Opening/Open/Closing/Closed) 확인으로 처리한다.
- **실패와 취소:** 로드 실패 시 에러를 남기고 열기 결과로 실패를 돌려준다(예외로 흐름을 끊지 않음). 로드 중에 닫기 요청이 오거나 UIRoot가 파괴되면 `CancellationToken`으로 열기를 취소하고, 늦게 도착한 프리팹은 바로 정리한다.
- 숨김은 `SetActive(false)` 대신 `Canvas.enabled = false` + `GraphicRaycaster` 비활성화. 재표시 비용(`OnEnable`, 레이아웃 재계산)이 없다.
  - **창 안의 자식 Canvas와 GraphicRaycaster까지 끄고 `CanvasGroup.interactable = false`로 둔다.** 자식 Canvas(`+1~+8`칸)는 부모를 꺼도 터치를 받고, 안 보이는 버튼도 Submit에는 눌린다. 다시 보일 때는 숨길 때 껐던 것만 켠다.
- **창은 `Update`를 쓰지 않는다.** 숨긴 창도 `Update`는 계속 돌기 때문이다. 갱신은 반응형 구독으로 하고, 매 프레임 값(쿨다운)이 필요한 창은 **WindowManager가 열린 창에만 호출하는 `Tick()`**에서 폴링한다. (HUD 위젯은 항상 보이므로 `Update` 사용 가능.)

### 6-4. 내비게이션

- **통합 스택:** 탭 = Exclusive(스택을 비우고 바닥이 됨), 팝업 = Overlay(쌓임).
- **뒤로가기:** Input System 액션 하나로 받아 `WindowManager`에 보낸다. 맨 위 창이 먼저 처리할 기회를 받고(`OnBack()`이 `true`면 처리 완료), 아니면 닫힌다. 입력 차단 중에는 무시한다. (구현 시 런타임 asmdef에 `Unity.InputSystem` 참조 추가 필요 — 현재 없음.)
- **스택이 비었을 때:** 종료 확인창(2차, DialogService).

### 6-5. 시간 규칙

- UI 애니메이션, 지연 스피너, 입력 차단 타임아웃은 `unscaledTime`을 쓴다. 게임 시간이 멈춰도 UI는 움직여야 한다.
- 스킬 쿨다운의 "끝나는 시각" 같은 게임 쪽 시간은 게임 시간(scaled)을 쓴다. 게임이 멈추면 같이 멈춰야 한다.
- **창은 `timeScale`을 바꾸지 않는다.** 방치형은 팝업이 떠 있어도 전투가 계속된다.
- **실제 시각(날짜, 경과 시간)이 필요하면 `IClock`을 통해서만 얻는다.** `DateTime.Now`를 직접 쓰지 않는다. 오프라인 보상처럼 시간 기반 보상은 서버 시간을 기준으로 하기 때문이다([SERVER_DESIGN.md](SERVER_DESIGN.md) 1장 규칙 3). 지금은 해당 코드가 없어 규칙만 둔다.

## 7. 입력 차단 (InputBlocker)

```csharp
using (inputBlocker.Acquire("StageTransition"))   // 스코프를 벗어나면 자동 해제
{
    await fade.PlayAsync();
}
```

- 참조 카운트: 여러 곳에서 동시에 걸어도 마지막 해제 때 풀린다.
- 스코프 기반 해제: 예외가 나도 잠긴 채 남지 않는다.
  - **코루틴의 지역변수로 `using`하지 않는다.** `StopCoroutine`이나 오브젝트 파괴로 끊기면 `finally`가 실행되지 않는다. `async` + `destroyCancellationToken`에서 쓰거나, 수명이 있는 객체가 필드로 들고 `OnDisable`에서도 해제한다.
- 지연 스피너: 차단은 즉시, 스피너는 일정 시간(예: 0.3초) 넘을 때만 표시해 깜빡임을 없앤다.
- 이유 기록 + 타임아웃 경고: 영구 잠금 디버깅용.
- 사용처: 창 전환(자동), 화면 페이드(현재 `FadeController`의 `blocksRaycasts`를 대체), 서버가 필요한 명령(결제 등, 생길 때) 대기, 튜토리얼(전체 차단에 구멍 하나를 뚫는 변형, 2차 이후).

## 8. 성능

- 창 단위 Canvas (4-4 참고).
- 매 프레임 바뀌는 위젯(쿨다운 게이지)의 서브 Canvas 분리는 프로파일러 측정 후 적용.
- 긴 목록 재사용 스크롤은 목록이 길어질 때 도입.

## 9. 단계

| 단계 | 내용 |
|---|---|
| **0단계 (게임 쪽 선행)** | GameInitializer와 GameSession(10-2), `GameNumber`(10-3). 게임 동작이 바뀌지 않는 리팩터, UI 작업과 별도 커밋 |
| **1차 (뼈대)** | 반응형 타입과 모델 전환, UIRoot와 `UISortBand`, WindowManager(옵션 기반 창, 스택, 정렬, 뒤로가기, Tick), 창 수명 주기와 타입 있는 인자, WindowRegistry, InputBlocker, View/Presenter와 구독 자동 해제, 기존 패널 이관 |
| **2차 (서비스)** | DialogService, ToastService, 공용 버튼(`UIButton`: 사운드, 연타 방지, 차단 인지), RedDot, 해금(탭 잠금 이관), 디버그 도구(창 스택·입력 차단 이유 오버레이. 치트 메뉴는 개발자 도구로 앞당겨 만들었다, [DEV_TOOLS.md](DEV_TOOLS.md)) |
| **3차 (품질)** | 전환 애니메이션, SafeArea, 하단 UI 영역을 `PlayAreaBounds`에 전달, 재사용 스크롤, 서브 Canvas, Addressables 로더, 현지화, 어셈블리 분리 |
| **4차 (서버 판정 기능: 가챠)** | 서버가 판정하는 첫 기능으로 가챠를 붙이고, 그 기능과 함께 서버 대비 부품을 만든다: `FakeGameServer`(지연·실패율·타임아웃 조절), `ServerCommandService`(요청 ID, 재시도, 타임아웃), `ServerOwnedState`와 전용 저장 DTO, 비동기 명령 + InputBlocker + 지연 스피너, 실패/재시도 안내, 오프라인 시 버튼 비활성화. 상세는 [SERVER_DESIGN.md](SERVER_DESIGN.md) 3·5장 |

**1차 진행 방식은 수직 슬라이스.** `SkillManagePopup` 하나를 Registry 로드 → WindowManager → Presenter → 반응형 구독/자동 해제 → InputBlocker까지 처음부터 끝까지 새 구조로 통과시켜 인터페이스를 확정한 뒤, 나머지 창을 옮긴다.

3차 항목 보충:
- **SafeArea:** 보정은 창의 콘텐츠 루트와 HUD/탭바의 콘텐츠에 적용한다. Canvas 루트, 딤, 전체 화면 배경은 보정 밖에 둔다. 루트에 걸면 노치와 홈 인디케이터 쪽에 어두워지지 않은 띠가 남기 때문이다.
- **현지화:** 지금은 `"Stage {n}"`, `"Lv."` 같은 문자열이 코드에 박혀 있다. 표시 문자열은 Presenter의 포맷팅에서만 만든다는 규칙을 1차부터 지키면, 나중에 Unity Localization을 그 한 곳에 연결하면 된다.
- **부트 씬:** 지금은 씬이 하나라 GameInitializer가 씬 수명이어도 된다. 타이틀/로그인 씬이 생기면 앱 수명(세이브 저장소, 설정)과 게임 수명(세션, UI)으로 나눈다.
- **4차 가챠를 따로 두는 이유:** 가짜 서버만 먼저 만들면 부르는 코드가 없어 검증할 수 없는 죽은 코드가 된다. 서버가 판정하는 기능 하나와 함께 만들어야 비동기 명령, 입력 차단, 요청 ID, 실패와 재시도가 실제로 돌아간다. 4차 시작 시 정할 것: 가챠 결과물(스킬, 동료, 장비 중 무엇), 가챠에 쓸 서버 소유 재화(예: 보석 — 골드로 돌리면 판정 주체가 섞임, SERVER_DESIGN 1장 규칙 2)와 그 재화의 지급 경로.
- **어셈블리 분리:** 지금은 런타임 코드 전체가 `IdleGameProtoType.Runtime` 하나다. "모델은 UI를 모른다"는 방향을 컴파일 단계에서 강제하려면 반응형 코어 / 게임 / UI asmdef로 나눈다. 1차 이관으로 UI → 게임 단방향이 된 뒤에 한다.

## 10. 게임 쪽 선행 결정

### 10-1. UI 명령은 동기 + 결과 반환

UI 명령이란 UI가 게임 모델에 보내는 요청이다: 업그레이드, 레벨업, 스킬 학습/장착/해제, 보스 도전.

- **창 API(6-3)와 다른 것이다.** 창 열기는 UI 자신이 시간이 걸리는 일(프리팹 로드, 전환 애니메이션 — 3차에 확정적으로 들어옴)이라 비동기 모양으로 잡는다. 명령은 로컬이 즉시 판정하므로 동기다. 기준은 같다: **"실제로 시간이 걸리는 일이 생길 것이 확실한가."**
- 서버 권한 구조로 바꾸는 일은 판정 로직 서버 이동, 응답 대기 중 상태, 실패 롤백, 오프라인 보상 계산까지 바뀌는 전체 구조 변경이다. UI 모양은 그중 작은 부분이라 미리 비동기로 잡아도 얻는 것이 적다. 반대로 지금 비동기로 하면 실제로 실행되지 않는 경로(응답 사이 상태 변화, 실패 처리)를 검증 없이 안고 간다.
- 서버가 꼭 필요한 명령(결제, 가챠 등)이 생기면 **그 명령만** 비동기로 만든다. 변경 범위는 그 명령의 호출부다.
- 결과는 실패 이유를 담는다. 지금 `PlayerController.TryUpgrade`는 `bool`이라 "최대 단계"와 "골드 부족"을 구분하지 못한다 → 예: `UpgradeResult { Success, MaxLevel, NotEnoughGold }`. UI는 이유별로 안내한다.
- 서버가 생겨도 이 결정은 유지된다. 동기/비동기를 가르는 기준은 "서버가 있는가"가 아니라 **"UI가 결과를 기다려야 하는가"**다. 업그레이드처럼 자주 누르는 명령은 서버가 있어도 클라이언트가 즉시 판정하고 서버는 검증만 하며, UI가 결과를 기다려야 하는 명령(가챠, 결제 등)이 생기면 그 명령만 비동기로 한다. 상세는 [SERVER_DESIGN.md](SERVER_DESIGN.md).

### 10-2. 진행도는 GameSession이 소유하고 GameInitializer가 생성한다

**현재:**
- `PlayerController.Awake()`가 세이브를 로드하고 `PlayerStats`를 초기화한다. 밸런스 데이터(`statData`, `upgradeConfig`)도 캐릭터 프리팹의 필드다.
- 골드는 `GameManager`(`GoldWallet`), 스테이지는 `StageManager`가 들고 있어 세이브 파일 하나에 주인이 셋이다. `SaveGame()`이 세 곳에서 값을 모아 `player.SaveData`에 써넣는다.
- UI 패널은 열릴 때 `GameManager.Instance.GetPlayer()`로 모델을 찾는다.
- 업그레이드 규칙(골드 차감 + 스탯 증가)이 캐릭터(`PlayerController.TryUpgrade`)에 있고 `GameManager.Instance.Wallet`에 직접 접근한다.

**문제:** 진행도의 수명이 캐릭터 오브젝트에 묶여 있고(캐릭터 교체/씬 재로드 시 위험), 생성 순서가 여러 `Awake`/`Start`의 암묵 규칙에 흩어져 있으며, UI가 싱글턴을 거쳐야만 모델에 닿아 주입할 수 없다.

**변경:**

```
GameInitializer.Start()  — 씬에 하나, [DefaultExecutionOrder(-1000)]로 다른 Start보다 먼저
  saveData = storage.Load()
  session  = new GameSession(밸런스 데이터)              // PlayerStats, GoldWallet, 스테이지 진행
  session.Apply(saveData)                              // 로드도 Apply 경로로
  player   = spawner.Spawn(session)                    // 캐릭터는 스탯을 읽기만
  gameManager.Initialize(session, player)
  uiRoot.Initialize(session, 서비스)
```

- 저장 데이터 조립은 `session.ToSaveData()` 한 곳, 반대 방향은 `session.Apply(스냅샷)` 한 곳.
- **`Apply`는 객체를 새로 만들지 않고 값만 덮어쓴다.** Presenter들이 GameSession과 그 안의 반응형 값을 구독하고 있어서, 객체를 새로 만들면 구독이 옛 객체에 남는다. 값만 덮어쓰면 반응형 구조라 화면이 자동으로 따라온다(레벨·경험치처럼 아직 반응형이 아닌 값은 `PlayerStats.OnReloaded`를 받아 다시 읽는다. 실행 중 교체를 처음 써 보고 찾은 빈틈이다, [DEV_TOOLS.md](DEV_TOOLS.md) T6). 세이브 로드와 서버 거절 시 되돌리기([SERVER_DESIGN.md](SERVER_DESIGN.md) 2장)가 같은 코드가 된다. GameSession 설계 자체에 관한 것이라 0단계에서 이렇게 만든다.
- 업그레이드 같은 게임 규칙 명령은 캐릭터에서 GameSession 쪽으로 옮긴다.
- 규칙: **다른 객체에 의존하는 초기화는 각자의 `Awake`/`Start`가 아니라 GameInitializer가 부르는 `Initialize()`에서 한다.**
- **Awake가 아니라 Start인 이유(구현 중 수정):** 처음에는 Awake로 적었으나, 그러면 다른 객체의 Awake(UI의 `OnGameStarted` 구독, 싱글턴 등록, `PlayAreaBounds`의 바닥 위치 계산)보다 먼저 돌 수 있다. 예: 게임 시작 알림이 HUD의 구독보다 먼저 나가 HUD가 안 뜨고, 플레이어가 바닥 위치 계산 전에 스폰된다. Unity 규약대로 "Awake = 자기 자신, Start = 다른 객체와 연결"로 나누고 실행 순서 -1000으로 다른 Start보다 먼저 돌게 했다.
- **이동속도는 캐릭터로 옮겼다(구현 중 결정).** `PlayerStats.RunSpeed`는 저장되지 않는 캐릭터 속성이라 "저장되는가" 기준에 따라 `PlayerController.RunSpeed`로 옮겼다.

**무엇을 옮기고 무엇을 남기나 — 기준은 "저장되는가":**

| 종류 | 예 | 주인 |
|---|---|---|
| 진행도 (저장됨) | 레벨, 경험치, 골드, 업그레이드 단계, 배운 스킬, 장착 슬롯, 스테이지 번호 | GameSession |
| 전투 상태 (저장 안 됨) | 현재 HP, 스킬 쿨다운, 현재 타겟, FSM 상태 | 캐릭터 |

기존 결정("스테이지 중간 진행도와 전투 중 HP는 저장 안 함")과 맞는다. HUD의 HP 바는 세션이 아니라 캐릭터의 HP를 구독한다.

**비용과 위험:**
- 바뀌는 곳: `PlayerController`, `PlayerSpawner`, `GameManager`, `StageManager` 일부, 밸런스 데이터 참조 위치, CLAUDE.md 저장 시스템 섹션.
- **세이브 파일 형식(DTO)은 그대로 둔다** → 마이그레이션 없음. 위험을 낮추는 핵심 조건이다.
- 밸런스 데이터 참조를 캐릭터 프리팹에서 옮길 때 인스펙터 재연결이 필요하다. 빠지면 조용히 null이 되므로 GameInitializer 시작 시 참조를 검증해 에러 로그를 낸다.
- 범위 밖: `GameDatabaseManager`(읽기 전용 정적 데이터), `PoolManager`(인프라)는 UI가 쓰지 않으므로 싱글턴을 유지한다. 모든 싱글턴 제거가 목표가 아니다.

**진행:** UI 1차 앞의 **0단계**로, 게임 동작이 바뀌지 않는 리팩터를 UI 작업과 별도 커밋 단위로 한다. 섞으면 문제가 생겼을 때 원인을 분리하기 어렵다. 반응형 전환은 기존 패널의 구독 방식까지 바뀌므로 0단계가 아니라 1차에서 패널 이관과 함께 한다. 검증: 플레이 모드에서 기존 세이브 로드 → 업그레이드 → 재시작 후 유지 확인.

### 10-3. 재화 숫자는 `GameNumber`

**현재:** 골드, 업그레이드 비용, 경험치, 숫자 포맷터(`NumberFormatUtility.Format(int)`)가 전부 `int`(최대 약 21억)다. 업그레이드 최대 단계가 29라 지금은 문제없지만(공격력 비용 `80 × 1.18^단계`가 2만 대에서 끝남), 최대 단계를 104로만 올려도 공격력 비용이 `int` 범위를 넘는다.

**결정:** 재화는 상한 없이 지수적으로 커지는 방치형 경제로 설계한다. 그래서 재화와 그에 엮인 수치를 `GameNumber` 구조체로 감싼다.

- **핵심은 표현이 아니라 감싸는 것.** API에는 `GameNumber`라는 이름만 노출된다. 내부는 `double`(약 10^308까지)로 시작하고, 부족해지면 "가수 + 지수" 방식으로 구조체 내부만 바꾼다.
- 1차에서 모델이 `ReactiveProperty<GameNumber>`로 바뀌기 전에 정해야 한다. 그 뒤에 바꾸면 Presenter, 파생 상태, 레드닷, 포맷터, 세이브까지 전부 다시 고쳐야 한다.
- 대상: 골드, 업그레이드 비용, 몬스터 처치 보상(런타임 값). 레벨/업그레이드 단계/슬롯 번호 같은 개수는 `int` 유지.
- **0단계 구현에서 범위를 좁힌 것:** 경험치는 `int`로 남겼다(필요 경험치가 엑셀 데이터 테이블 값이라 데이터 포맷까지 바뀌므로, 경제 규모가 실제로 커질 때 따로 한다). 몬스터 기본 보상(`MonsterData.goldReward`)도 데이터 테이블은 `int` 그대로 두고, 스테이지 배율을 곱한 런타임 보상만 `GameNumber`로 만든다 — 커지는 쪽은 배율이라 기본값은 작고, 데이터 쪽을 안 건드리면 `.asset` 직렬화 위험이 없다.
- 구조체 요건: 불변, 사칙연산과 비교 연산자, `IEquatable`(반응형 값의 같은 값 무시에 필요), 포맷터는 `NumberFormatUtility.Format(GameNumber)`로 이동.
- `double`의 한계: 약 9×10^15을 넘으면 1 단위 정확도가 사라진다. 방치형의 표시("1.2aa")와 구매 가능 비교에는 문제가 없는 수준으로 판단.

**직렬화 경계 — 저장/데이터 쪽은 기본 타입으로 둔다:**
- 세이브 DTO(`PlayerSaveData.gold`)와 데이터 테이블에는 `GameNumber`를 넣지 않는다(세이브는 `double`, 데이터 테이블은 위 이유로 `int` 유지). 구조체를 넣으면 YAML/JSON 모양이 중첩 구조로 바뀌어 기존 값이 조용히 사라진다(CLAUDE.md "직렬화 함정"). 런타임 모델에서만 `GameNumber`로 바꾼다.
- `int` → `double` 타입 변경도 위험 지점이었다. **확인 결과:** `JsonUtility`가 예전 형식(`"gold":12345`)을 `double` 필드로 그대로 읽는다(배치 모드에서 확인). 데이터 테이블은 타입을 안 바꿨으므로 `.asset` 확인은 불필요.

## 11. 결정 변경 이력

설계 중 뒤집힌 결정과 이유. 같은 고민을 반복하지 않기 위해 남긴다.

| 이전 결정 | 변경 | 이유 |
|---|---|---|
| 창 종류를 서브클래스로(`UITabPanel`, `UIPopup` …) | `UIWindow` + `WindowOptions` | 성격이 독립된 여러 축이라 서브클래스 조합이 곱으로 늘어남 |
| 레이어마다 루트 Canvas + 창마다 Canvas | 정렬 구분은 코드 enum, Canvas는 창 단위 | 두 겹 Canvas가 중복. 정렬 구분은 sortingOrder 기준값이면 충분 |
| 최상위 Static/Dynamic Canvas 분리 | 창/HUD 그룹 단위 Canvas, 서브 Canvas는 측정 후 | 최상위 분리는 한 Canvas 전체가 다른 Canvas 전체의 위/아래에 놓여 정렬 구분과 충돌 |
| 정렬 구분 8개 | 5개 | 쓰지 않는 구분을 미리 만들지 않음. 간격을 넓게 둬 추가 비용 없음 |
| 데미지 숫자를 HUD 위 | HUD 아래(Fx 0) | HUD 위면 데미지 숫자가 HUD 바를 덮음 |
| 열기/닫기 요청 큐 | 창 상태 확인 + 전환 중 입력 차단 | 큐가 막으려던 경쟁 상태는 대부분 연타이며 입력 차단으로 해결됨 |
| 반응형 상태 도입 보류(C# 이벤트 + 구독 헬퍼) | 지금 도입 | 보류 근거가 "도메인 코드까지 번진다"는 일정 문제였음. 구조 필요성만 보면 필요하고, 나중에 바꾸면 그 위에 만든 Presenter/RedDot을 전부 다시 써야 함 |
| 사건은 C# 이벤트 유지 | `Subject<T>`로 구독 모양 통일 | 상태/사건을 섞어 쓰면 구독 쪽에서 실수. 공통 `Subscribe → IDisposable`이면 구독 쪽은 고를 것이 없고 해제 누락도 사라짐 |
| 정렬 구분 이름 `UILayer` → 후보 `UISortGroup` | `UISortBand` | `UILayer`는 Unity Layer/Sorting Layer와, `UISortGroup`은 `SortingGroup` 컴포넌트와 충돌 |
| 보스 도전 가능 = 사건 | 상태 | "지금 도전 가능한가?"에 답이 있음. 사건이면 나중에 생성된 버튼이 지나간 알림을 못 받음 |
| 창 옵션 표현 미정(코드 vs 직렬화) | 구조는 코드(가상 프로퍼티 + 프리셋), 연출만 프리팹 | 구조를 프리팹에 두면 동작이 숨고 코드와 어긋날 수 있음 |
| UI 명령도 비동기 모양 검토 | 동기 + 결과 반환, 서버가 필요한 명령만 비동기 | 서버 권한 전환은 UI 모양보다 훨씬 큰 구조 변경이라 미리 비동기로 해도 얻는 것이 적고, 실행되지 않는 경로를 검증 없이 안게 됨 |
| 진행도를 캐릭터(`PlayerController`)가 소유, 로드는 그 `Awake()` | GameSession 소유, GameInitializer가 생성 순서 결정 | 수명이 캐릭터에 묶이고 순서가 암묵 규칙에 흩어지며, UI에 모델을 주입할 수 없음 |
| 재화 `int` | `GameNumber` 구조체 (내부 `double`) | 방치형 경제는 상한 없이 지수적으로 커짐. 모델이 반응형으로 바뀌기 전에 감싸 두면 이후 표현 변경은 구조체 내부만 바뀜 |
| 창 열기는 결과 없음 | 핸들로 닫힘 대기 + 타입 있는 결과 | 확인창 결과와 시작 팝업 순서에 필요. 나중에 넣으면 `Open` 시그니처가 바뀌어 호출부 전부 수정 |
| 정적 이벤트 버스 (예외 규정 없음) | 원칙의 예외로 명시 + 플레이 시작 시 초기화 | 인스턴스화는 비용 대비 효과가 적고, 실제 위험(도메인 리로드 꺼짐 시 잔존 구독자)은 초기화로 막힘 |
| 시작 코드 이름 `Bootstrap` | `GameInitializer` | 웹 CSS 프레임워크 Bootstrap과 헷갈림. 역할(게임 시작 시 객체를 순서대로 만들고 연결)이 이름에 드러나게 함 |
| 정렬 구분 간격 200, 깊이 한도 없음 | 간격 1000, 구분 안 깊이 100까지, 초과 시 에러 | 깊이가 쌓이면 다음 구분을 조용히 침범할 수 있었음. 16비트 범위라 간격을 아낄 이유가 없음 |
| 정적 이벤트 버스에 사건과 상태 혼재 | 버스에는 사건만, 상태는 주인의 `ReactiveProperty` | 정적 버스의 상태는 전역 가변 변수가 됨 |
| 재진입 무한 루프를 깊이 제한으로 감지 | 총 전달 수로 감지 | 구현 중 재현: 구독자 둘이면 깊이 17에서 알림이 30만 번을 넘어 깊이 100에 닿지 못함 |
| 새 팝업은 전부 `UIWindow` | 개발자 도구 패널만 IMGUI(예외) | 릴리스 빌드에서 통째로 빠져야 해서 에셋을 가질 수 없음. 창 프리팹을 `WindowRegistry`에 등록하면 릴리스 빌드에 스크립트가 빠진 프리팹이 들어간다([DEV_TOOLS.md](DEV_TOOLS.md) T3) |
