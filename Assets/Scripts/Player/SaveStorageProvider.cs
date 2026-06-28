// 현재 사용 중인 ISaveStorage 구현체를 들고 있는 단일 스위치 지점.
// 서버 저장으로 바꿀 때는 Current에 새 구현체(예: ServerSaveStorage)를 대입하는
// 한 줄만 바꾸면 되고, 호출부(PlayerController/PlayerStats/GameManager)는 그대로 둔다.
public static class SaveStorageProvider
{
    public static ISaveStorage Current { get; set; } = new LocalFileSaveStorage();
}
