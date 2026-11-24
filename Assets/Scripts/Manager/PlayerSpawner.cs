using UnityEngine;

// 플레이어 캐릭터를 스폰한다. 어떤 캐릭터인지는 엑셀 캐릭터 표(CharacterData, D12)의 id로 정하고,
// 프리팹은 그 행에 엑셀 변환 때 자동 연결된 것(characterName = 프리팹 이름)을 쓴다. 몬스터 스폰과 같은 방식이다.
// 직업 선택이 생기면 characterId를 세이브나 UI가 정하게 바꾸면 된다.
public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    [Tooltip("캐릭터 표(CharacterDatabase)의 id")]
    [SerializeField] private int characterId = 1;
    [SerializeField] private Transform defaultSpawnPoint;

    public PlayerController CurrentPlayer { get; private set; }

    // 스폰한 캐릭터의 데이터(PlayerController.Initialize에 넘긴다)
    public CharacterData Character { get; private set; }

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

        CharacterEntry entry = GameDatabaseManager.Instance != null ? GameDatabaseManager.Instance.GetCharacter(characterId) : null;
        if (entry?.data == null)
        {
            Debug.LogError($"[PlayerSpawner] 캐릭터 표에 id {characterId}가 없습니다.");
            return null;
        }
        Character = entry.data;

        CurrentPlayer = FindAnyObjectByType<PlayerController>();
        if (CurrentPlayer != null)
        {
            return CurrentPlayer;
        }

        GameObject playerPrefab = entry.prefab;
        if (playerPrefab == null)
        {
            Debug.LogError($"[PlayerSpawner] 캐릭터 {characterId}({entry.data.CharacterName})의 프리팹이 연결되지 않았습니다.");
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
        CurrentPlayer = obj.GetComponent<PlayerController>();

        if (CurrentPlayer == null)
        {
            Debug.LogError($"[PlayerSpawner] {playerPrefab.name}에 PlayerController 컴포넌트가 없습니다.");
        }

        return CurrentPlayer;
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
