// 테스트용 전투원 뷰(그리기 없음). CombatantViews가 붙이고 떼는 규칙을 실제 MonoBehaviour로 확인할 때 쓴다.
// 애니메이션 뷰가 없어도 되도록 기본 Awake 검사를 건너뛴다.
public sealed class TestCombatantHost : CombatantHost
{
    protected override void Awake() { }
}
