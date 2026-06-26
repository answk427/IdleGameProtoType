using UnityEngine;

public class PlayerSpawner : MonoBehaviour
{
    public static PlayerSpawner Instance { get; private set; }

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform defaultSpawnPoint;

    public PlayerController CurrentPlayer { get; private set; }

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

        CurrentPlayer = FindAnyObjectByType<PlayerController>();
        if (CurrentPlayer != null)
        {
            return CurrentPlayer;
        }

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
