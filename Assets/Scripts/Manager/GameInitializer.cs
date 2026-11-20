using UnityEngine;

// 게임 시작 시 객체를 순서대로 만들고 서로 연결하는 조립 지점(Composition Root). 씬에 하나만 둔다.
//
//   1. 세이브 로드
//   2. GameSession 생성 + 세이브 적용(Apply)
//   3. 플레이어 캐릭터 스폰 + 진행도 주입
//   4. GameManager에 세션과 캐릭터를 넘겨 게임 루프 시작
//
// 예전에는 이 순서가 여러 Awake/Start에 흩어진 암묵 규칙이었다(캐릭터 Awake가 세이브를 읽고,
// GameManager.Start가 그 캐릭터에서 골드/스테이지를 꺼내 썼다). 이제 순서를 이 한 곳에서 읽을 수 있다.
//
// Awake가 아니라 Start에서 하는 이유: 각 객체의 Awake(자기 자신 초기화, 이벤트 구독, 싱글턴 등록,
// PlayAreaBounds 계산 등)가 모두 끝난 뒤에 연결해야 하기 때문이다. Unity 규약대로
// "Awake = 자기 자신, Start = 다른 객체와 연결"로 나누고, DefaultExecutionOrder로 다른 Start보다 먼저 돈다.
[DefaultExecutionOrder(-1000)]
public class GameInitializer : MonoBehaviour
{
    [Header("밸런스 데이터 (예전에는 플레이어 프리팹에 있던 참조)")]
    [SerializeField] private PlayerStatDatabase statData;
    [SerializeField] private PlayerUpgradeConfig upgradeConfig;

    [Header("씬 참조")]
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private GameManager gameManager;

    public GameSession Session { get; private set; }

    private void Start()
    {
        // 참조가 빠지면(인스펙터 연결 누락) 에러 없이 null로 진행되다가 엉뚱한 곳에서 터진다.
        // 시작 지점에서 먼저 검사해 원인을 바로 보이게 한다.
        if (!ValidateReferences()) return;

        PlayerSaveData saveData = SaveStorageProvider.Current.Load();

        Session = new GameSession(statData, upgradeConfig);
        Session.Apply(saveData);

        PlayerController player = playerSpawner.EnsurePlayer();
        if (player == null)
        {
            Debug.LogError("[GameInitializer] 플레이어를 생성하지 못했습니다.");
            return;
        }

        player.Initialize(Session.Stats);
        gameManager.Initialize(Session, player);
    }

    private bool ValidateReferences()
    {
        bool ok = true;
        if (statData == null) { Debug.LogError("[GameInitializer] statData(PlayerStatDatabase)가 연결되지 않았습니다."); ok = false; }
        if (upgradeConfig == null) { Debug.LogError("[GameInitializer] upgradeConfig(PlayerUpgradeConfig)가 연결되지 않았습니다."); ok = false; }
        if (playerSpawner == null) { Debug.LogError("[GameInitializer] playerSpawner가 연결되지 않았습니다."); ok = false; }
        if (gameManager == null) { Debug.LogError("[GameInitializer] gameManager가 연결되지 않았습니다."); ok = false; }
        return ok;
    }
}
