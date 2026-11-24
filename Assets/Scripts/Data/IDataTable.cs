using System;

// 엑셀 시트 하나에 대응하는 데이터베이스(ScriptableObject)의 공통 접점. 튜닝 덮어쓰기(DataTuning)가 어느 표든 같은 방식으로 다룬다.
public interface IDataTable
{
    Type DataType { get; }
    int RowCount { get; }
    IData GetRow(int index); // 행이 비어 있으면 null
}
