using UnityEngine;

public class GameDatabaseManager : MonoBehaviour
{   
    private static GameDatabaseManager instance;

    // Awake 실행 순서에 의존하지 않도록 지연 초기화.
    // 다른 매니저가 GameDatabaseManager.Awake()보다 먼저 Instance에 접근해도,
    // 씬에 이미 존재하는 인스턴스를 찾아 그 자리에서 초기화한다.
    public static GameDatabaseManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<GameDatabaseManager>();

                if (instance != null)
                {
                    instance.EnsureInitialized();
                }
                else
                {
                    Debug.LogError("[GameDatabaseManager] 씬에 GameDatabaseManager가 존재하지 않습니다.");
                }
            }
            return instance;
        }
    }

    [SerializeField] private MonsterDatabase monsterDatabase;
    [SerializeField] private StageDatabase stageDatabase;
    [SerializeField] private SkillDatabase skillDatabase;
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private BalanceConfig balanceConfig;
    [SerializeField] private ContentUnlockDatabase contentUnlockDatabase;

    private bool isInitialized;

    // 콘텐츠 해금 조건 표. 게임은 GameSession.Unlocks로 읽는다(GameInitializer가 넘긴다). 연결이 빠지면 null.
    public ContentUnlockDatabase ContentUnlocks
    {
        get
        {
            EnsureInitialized();
            return contentUnlockDatabase;
        }
    }

    // GameInitializer가 게임을 시작하기 전에 검사한다.
    public bool HasBalanceConfig => balanceConfig != null;

    // 스테이지 성장 곡선. 필수 데이터라 기본값으로 대신하지 않는다.
    // 기본값으로 굴리면 연결이 빠져도 티가 나지 않고, 에셋을 고쳐도 조용히 무시되기 때문이다.
    public BalanceConfig Balance
    {
        get
        {
            if (balanceConfig == null)
            {
                throw new System.InvalidOperationException("[GameDatabaseManager] BalanceConfig가 연결되어 있지 않습니다.");
            }
            return balanceConfig;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureInitialized();
    }

    // 여러 경로(Awake, Instance 최초 접근)에서 호출될 수 있으므로 중복 실행 방지.
    private void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        // 에디터 플레이에서는 튜닝 덮어쓰기를 적용한 사본을 쓴다(빌드에서는 원본 그대로, docs/COMBAT_ARCHITECTURE.md 8-7).
        monsterDatabase = DataTuning.Prepare(monsterDatabase);
        stageDatabase = DataTuning.Prepare(stageDatabase);
        skillDatabase = DataTuning.Prepare(skillDatabase);
        characterDatabase = DataTuning.Prepare(characterDatabase);
        contentUnlockDatabase = DataTuning.Prepare(contentUnlockDatabase);

        if (contentUnlockDatabase != null) contentUnlockDatabase.EnsureLookup();
        if (characterDatabase != null) characterDatabase.EnsureLookup();
        if (monsterDatabase != null) monsterDatabase.EnsureLookup();
        if (stageDatabase != null) stageDatabase.EnsureLookup();
        if (skillDatabase != null) skillDatabase.EnsureLookup();
    }

    public MonsterEntry GetMonster(int id)
    {
        if (monsterDatabase == null)
        {
            Debug.LogError("[GameDatabaseManager] MonsterDatabase가 연결되어 있지 않습니다.");
            return null;
        }
        return monsterDatabase.GetById(id);
    }

    public StageEntry GetStage(int stageNumber)
    {
        if (stageDatabase == null)
        {
            Debug.LogError("[GameDatabaseManager] StageDatabase가 연결되어 있지 않습니다.");
            return null;
        }
        return stageDatabase.GetByNumber(stageNumber);
    }

    public CharacterEntry GetCharacter(int id)
    {
        if (characterDatabase == null)
        {
            Debug.LogError("[GameDatabaseManager] CharacterDatabase가 연결되어 있지 않습니다.");
            return null;
        }
        return characterDatabase.GetById(id);
    }

    public SkillEntry GetSkill(int id)
    {
        if (skillDatabase == null)
        {
            Debug.LogError("[GameDatabaseManager] SkillDatabase가 연결되어 있지 않습니다.");
            return null;
        }
        return skillDatabase.GetById(id);
    }

    public System.Collections.Generic.IReadOnlyList<SkillEntry> GetAllSkills()
    {
        if (skillDatabase == null)
        {
            Debug.LogError("[GameDatabaseManager] SkillDatabase가 연결되어 있지 않습니다.");
            return new System.Collections.Generic.List<SkillEntry>();
        }
        return skillDatabase.GetAll();
    }
}
