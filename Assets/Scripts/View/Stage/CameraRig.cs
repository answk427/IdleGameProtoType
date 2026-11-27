using UnityEngine;

// 카메라가 대열의 앞장(플레이어)을 따라간다(docs/COMBAT_ARCHITECTURE.md 9-3, D20).
// 화면 구도는 처음 시작할 때의 "카메라 - 플레이어" 거리를 그대로 유지한다(플레이어가 화면의 같은 자리에 보인다).
// 시뮬레이션은 카메라를 모른다. 화면 프레임 마지막(LateUpdate)에, 전투원의 보간 위치가 정해진 뒤에 따라간다.
// 배경도 여기서 카메라에 맞춘다.
public class CameraRig : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private BackgroundScroller background;

    private IReadOnlyCombatWorld world;
    private CombatantViews views;
    private float allyToCameraX;
    private bool hasOffset;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (targetCamera == null) Debug.LogError("[CameraRig] 따라갈 카메라가 없습니다.");
    }

    // GameInitializer가 게임을 만든 뒤 잇는다(이펙트·배경과 같은 길). 앞장의 그림은 뷰 표에서 찾는다.
    // 예전에는 앱의 실행기(GameRunner)를 씬 필드로 쥐고 거기서 월드와 뷰를 꺼냈다(뷰 → 앱 방향, 13-1).
    public void Bind(IReadOnlyCombatWorld combatWorld, CombatantViews combatantViews)
    {
        world = combatWorld;
        views = combatantViews;
    }

    private void LateUpdate()
    {
        IReadOnlyCombatant lead = world?.LeadAlly;
        if (targetCamera == null || lead == null || views == null || !views.TryGet(lead, out CombatantHost host)) return;

        Transform cameraTransform = targetCamera.transform;
        float allyX = host.transform.position.x; // 이번 화면 프레임의 보간 위치(GameRunner.Update가 그림)
        if (!hasOffset)
        {
            allyToCameraX = cameraTransform.position.x - allyX;
            hasOffset = true;
        }

        Vector3 position = cameraTransform.position;
        position.x = allyX + allyToCameraX;
        cameraTransform.position = position;

        // 원점 재설정으로 좌표가 줄어도 "이동한 거리"는 이어진다.
        if (background != null) background.Follow(position.x, world.TotalShift + position.x);
    }
}
