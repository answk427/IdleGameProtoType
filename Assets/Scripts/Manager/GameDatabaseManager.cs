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

    private bool isInitialized;

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
