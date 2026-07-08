using System;
using System.Collections;


public abstract class SaveStorageBase : ISaveStorage
{
    public PlayerSaveData Load()
    {
        try
        {
            // 세이브 데이터가 없으면 생성
            string raw = ReadRawOrNull();
            if (raw == null) return new PlayerSaveData();

            PlayerSaveData data = Parse(raw);
            if (data == null || data.stats == null)
            {
                throw new InvalidOperationException("파싱 결과가 비어 있습니다.");
            }
            return data;
        }
        catch (Exception e)
        {
            throw new SaveDataCorruptedException("세이브 데이터를 읽거나 해석하는 데 실패했습니다.", e);
        }
    }

    protected abstract string ReadRawOrNull();
    protected abstract PlayerSaveData Parse(string raw);

    public abstract IEnumerator Save(PlayerSaveData data);
    public abstract void Delete();
}
