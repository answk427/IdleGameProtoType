using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// StageData → StageDatabase(SO) 동기화 담당.
// bgTexturePath 기준으로 배경 텍스처를 자동 매칭해서 StageEntry.backgroundTexture를 채운다.
// 경로가 비었거나 텍스처를 찾지 못하면, 그 스테이지에 이미 연결돼 있던 텍스처를 그대로 둔다(동기화로 배경이 사라지지 않게. 경고는 남긴다).
// ExcelToJsonConverter는 이 클래스를 IDataSyncer 구현체로 리플렉션 탐색해 자동으로 실행한다.
public class StageDatabaseSyncer : IDataSyncer
{
    private const string DbPath = "Assets/Data/Stage/StageDatabase.asset";

    public Type DataType => typeof(StageData);

    public void Sync(IList dataList, Action<string> log)
    {
        var db = DatabaseSyncUtility.LoadOrCreateDatabaseAsset<StageDatabase>(DbPath, log);

        // 기존 연결을 스테이지 번호 기준으로 백업 (경로로 찾지 못했을 때 유지용)
        var existingByNumber = new Dictionary<int, Texture2D>();
        foreach (var e in db.entries)
        {
            if (e?.data != null) existingByNumber[e.data.StageNumber] = e.backgroundTexture;
        }

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
            else if (existingByNumber.TryGetValue(data.StageNumber, out Texture2D kept) && kept != null)
            {
                bg = kept;
                log?.Invoke($"[SO 동기화 경고] Stage {data.StageNumber}의 배경 텍스처 '{data.BgTexturePath}'를 찾지 못해 기존 연결({kept.name})을 유지합니다. 엑셀의 bgTexturePath를 채우세요.");
            }
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
