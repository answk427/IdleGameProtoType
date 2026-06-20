using System;
using System.Collections;

// 엑셀 → JSON 변환이 끝난 뒤, 특정 IData 타입의 데이터를 SO 데이터베이스에 동기화하는 역할을 정의하는 인터페이스.
// ExcelToJsonConverter는 IDataSyncer 구현 클래스를 리플렉션으로 자동 탐색하므로,
// 새로운 SO(데이터베이스)를 추가할 때는 ExcelToJsonConverter.cs를 건드릴 필요 없이
// 이 인터페이스를 구현한 새 클래스만 Editor 폴더 아래에 추가하면 된다.
public interface IDataSyncer
{
    // 이 Syncer가 처리할 IData 구현 타입 (예: typeof(MonsterData))
    Type DataType { get; }

    // dataList: 엑셀에서 변환된 해당 타입의 데이터 리스트 (실제로는 IList<TData>)
    // log: 컨버터 창에 로그를 남기기 위한 콜백 (ExcelToJsonConverter.AddLog)
    void Sync(IList dataList, Action<string> log);
}
