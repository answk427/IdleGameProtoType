using System;
using System.Collections.Generic;
using UnityEngine;

// UI 소리 종류 → 클립. 연출이라 에셋이 갖는다. 클립이 비어 있으면 그 소리는 나지 않는다
// (2026-11-26 기준 프로젝트에 오디오 파일이 없어 전부 비어 있다. 클립을 넣으면 코드 수정 없이 난다).
[CreateAssetMenu(fileName = "UISoundSet", menuName = "Game/UI Sound Set")]
public class UISoundSet : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public UIButtonSound sound;
        public AudioClip clip;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    // 없으면 null.
    public AudioClip Get(UIButtonSound sound)
    {
        foreach (Entry entry in entries)
        {
            if (entry.sound == sound) return entry.clip;
        }
        return null;
    }
}
