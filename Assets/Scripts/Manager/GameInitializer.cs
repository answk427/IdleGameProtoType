using UnityEngine;

// 게임 시작 시 객체를 순서대로 만들고 서로 연결하는 조립 지점(Composition Root). 씬에 하나만 둔다.
//
//   1. 세이브 로드 (실패하면 여기서 중단)
//   2. GameSession 생성 + 세이브 적용(Apply)
//   3. 플레이어 캐릭터 스폰 + 진행도 주입
//   4. GameManager에 세션과 캐릭터를 넘겨 게임 루프 시작
//   5. UIRoot에 세션과 서비스를 넘겨 UI 조립
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
    [SerializeField] private UIRoot uiRoot;

    public GameSession Session { get; private set; }

    private void Start()
    {
        // 참조가 빠지면(인스펙터 연결 누락) 에러 없이 null로 진행되다가 엉뚱한 곳에서 터진다.
        // 시작 지점에서 먼저 검사해 원인을 바로 보이게 한다.
        if (!ValidateReferences()) return;

        PlayerSaveData saveData;
        try
        {
            saveData = SaveStorageProvider.Current.Load();
        }
        catch (System.Exception e)
        {
            // 새 데이터로 대신 시작하지 않는다. 세션이 없으면 GameManager의 저장 경로(종료 저장 포함)가
            // 아무것도 쓰지 않으므로 원본이 그대로 남는다. 세이브 오류 안내창도 여기서 띄우면 된다.
            Debug.LogError($"[GameInitializer] 세이브를 읽지 못해 게임을 시작하지 않습니다(원본 보존).\n{e}");
            return;
        }

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
        uiRoot.Initialize(Session, GameDatabaseManager.Instance.GetAllSkills(), player);
    }

    private bool ValidateReferences()
    {
        bool ok = true;
        if (statData == null) { Debug.LogError("[GameInitializer] statData(PlayerStatDatabase)가 연결되지 않았습니다."); ok = false; }
        if (upgradeConfig == null) { Debug.LogError("[GameInitializer] upgradeConfig(PlayerUpgradeConfig)가 연결되지 않았습니다."); ok = false; }
        if (playerSpawner == null) { Debug.LogError("[GameInitializer] playerSpawner가 연결되지 않았습니다."); ok = false; }
        if (gameManager == null) { Debug.LogError("[GameInitializer] gameManager가 연결되지 않았습니다."); ok = false; }
        if (uiRoot == null) { Debug.LogError("[GameInitializer] uiRoot가 연결되지 않았습니다."); ok = false; }

        // BalanceConfig는 첫 스폰 때에야 쓰인다. 여기서 막지 않으면 게임이 시작된 뒤 스폰 루프만 예외로 멈춘다.
        GameDatabaseManager database = GameDatabaseManager.Instance;
        if (database == null) { Debug.LogError("[GameInitializer] 씬에 GameDatabaseManager가 없습니다."); ok = false; }
        else if (!database.HasBalanceConfig) { Debug.LogError("[GameInitializer] GameDatabaseManager에 balanceConfig(BalanceConfig)가 연결되지 않았습니다."); ok = false; }

        return ok;
    }
}
