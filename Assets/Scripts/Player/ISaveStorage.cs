using System;
using System.Collections;

// 저장 데이터를 "어디에" 두는지에 대한 추상화. 로컬 파일/서버 등 백엔드를 갈아끼울 때
// 이 인터페이스 구현체만 새로 만들면 되고, 호출부(PlayerController/GameManager)는 안 바뀐다.
//
// Load()는 동기: PlayerController.Awake()가 그 자리에서 결과를 바로 써야 하기 때문.
// Save()는 코루틴: 호출부가 결과를 기다리지 않는 fire-and-forget이라 네트워크 지연이
// 생겨도 문제없도록 미리 비동기 모양으로 잡아둔 것.
public interface ISaveStorage
{
    PlayerSaveData Load();
    IEnumerator Save(PlayerSaveData data);
    void Delete();
}

// 저장 데이터가 존재하지만(신규 유저가 아님) 읽거나 해석할 수 없을 때 던지는 예외.
public class SaveDataCorruptedException : Exception
{
    public SaveDataCorruptedException(string message) : base(message) { }
    public SaveDataCorruptedException(string message, Exception inner) : base(message, inner) { }
}
