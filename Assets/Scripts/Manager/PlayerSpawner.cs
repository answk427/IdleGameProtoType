using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform defaultSpawnPoint;

    public PlayerController CurrentPlayer { get; private set; }

    // 세이브 데이터가 있는데 읽을 수 없어서(손상) 스폰 자체를 안 한 상태. 이 경우 진행도
    // 유실 위험이 있으므로 GameManager가 게임을 시작하지 않고 안내 메시지만 띄운다.
    public bool SaveDataLoadFailed { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public PlayerController EnsurePlayer()
    {
        if (CurrentPlayer != null)
        {
            return CurrentPlayer;
        }

        if (!TryLoadSaveData(out PlayerSaveData saveData))
        {
            return null;
        }

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            player = SpawnPlayer();
            if (player == null) return null;
        }

        player.Initialize(saveData);
        CurrentPlayer = player;
        return CurrentPlayer;
    }

    private PlayerController SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("[PlayerSpawner] playerPrefab이 설정되어 있지 않습니다.");
            return null;
        }

        Vector3 spawnPos = defaultSpawnPoint != null ? defaultSpawnPoint.position : Vector3.zero;
        if (PlayAreaBounds.Instance != null)
        {
            // 프리팹 자체(인스턴스화 전)에서 캐릭터별 보정값을 읽어온다.
            float characterGroundOffset = playerPrefab.GetComponent<PlayerController>()?.GroundOffset ?? 0f;
            spawnPos.y = PlayAreaBounds.Instance.GroundY + characterGroundOffset;
        }

        GameObject obj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
        PlayerController player = obj.GetComponent<PlayerController>();
        if (player == null)
        {
            Debug.LogError($"[PlayerSpawner] {playerPrefab.name}에 PlayerController 컴포넌트가 없습니다.");
        }
        return player;
    }

    // 저장 데이터가 아예 없으면(신규 유저) 새 PlayerSaveData로 성공 처리하고, 있는데
    // 못 읽으면(손상) 여기서 스폰을 막는다 — 손상된 세이브로 PlayerController가 만들어지는
    // 일 자체가 없도록 Instantiate 이전에 확인한다.
    private bool TryLoadSaveData(out PlayerSaveData saveData)
    {
        try
        {
            saveData = SaveStorageProvider.Current.Load();
            return true;
        }
        catch (SaveDataCorruptedException e)
        {
            Debug.LogError($"[PlayerSpawner] 세이브 데이터 손상: {e}");
            SaveDataLoadFailed = true;
            saveData = null;
            return false;
        }
    }

    public void ReleasePlayer()
    {
        if (CurrentPlayer != null)
        {
            CurrentPlayer.SaveProgress();
            Destroy(CurrentPlayer.gameObject);
            CurrentPlayer = null;
        }
    }
}
