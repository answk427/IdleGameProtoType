using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// StageData → StageDatabase(SO) 동기화 담당.
// bgTexturePath 기준으로 배경 텍스처를 자동 매칭해서 StageEntry.backgroundTexture를 채운다.
// ExcelToJsonConverter는 이 클래스를 IDataSyncer 구현체로 리플렉션 탐색해 자동으로 실행한다.
public class StageDatabaseSyncer : IDataSyncer
{
    private const string DbPath = "Assets/Data/Stage/StageDatabase.asset";

    public Type DataType => typeof(StageData);

    public void Sync(IList dataList, Action<string> log)
    {
        var db = DatabaseSyncUtility.LoadOrCreateDatabaseAsset<StageDatabase>(DbPath, log);

        var newEntries = new List<StageEntry>();
        int matchedTexture = 0;

        foreach (StageData data in dataList)
        {
            Texture2D bg = null;

            if (!string.IsNullOrEmpty(data.BgTexturePath))
            {
                string directPath = $"Assets/Resources/{data.BgTexturePath}.png";
                bg = AssetDatabase.LoadAssetAtPath<Texture2D>(directPath);

                if (bg == null)
                {
                    string fileName = Path.GetFileName(data.BgTexturePath);
                    string[] guids = AssetDatabase.FindAssets($"{fileName} t:Texture2D");
                    if (guids.Length > 0)
                    {
                        bg = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }
            }

            if (bg != null) matchedTexture++;
            else log?.Invoke($"[SO 동기화 경고] Stage {data.StageNumber}의 배경 텍스처 '{data.BgTexturePath}'를 찾지 못했습니다.");

            newEntries.Add(new StageEntry
            {
                data = data,
                backgroundTexture = bg
            });
        }

        db.entries = newEntries;
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();

        log?.Invoke($"\u2705 StageDatabase 동기화 완료: {newEntries.Count}개 (텍스처 매칭 {matchedTexture}개)");
    }
}
