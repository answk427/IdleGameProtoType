using UnityEngine;

// 플레이어 인스턴스의 존재 여부를 검사하고, 없으면 생성(스폰)하는 책임을 전담.
//
// - 같은 씬에서 UI 패널만 전환되는 경우: Instance가 이미 존재하므로 그대로 반환.
// - 콘텐츠별로 씬이 전환되는 경우: 씬 전환으로 기존 인스턴스가 파괴되었으므로
//   프리팹에서 새로 생성하고, PlayerController.Awake()가 PlayerSaveData.Load()로
//   저장된 스탯을 복원한다(이미 구현되어 있음).
//
// 두 시나리오 모두 GameManager.Start()는 동일하게 EnsurePlayer()만 호출하면 된다.
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

        // 씬에 이미 배치된 플레이어가 있는지 먼저 확인 (과거 방식과의 호환, 에디터 테스트용)
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

    // 콘텐츠 이동(장비/동료 등) 등으로 명시적으로 플레이어를 정리해야 할 때 사용.
    // 같은 씬에서 UI 패널만 전환되는 구조라면 보통 호출할 필요 없음.
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
