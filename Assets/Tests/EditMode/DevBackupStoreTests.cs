using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

// 개발 도구 백업(docs/DEV_TOOLS.md T7): 검사를 통과한 것만 쓰고 읽으며, 기존 백업을 덮어쓰지 않는다.
[TestFixture]
public class DevBackupStoreTests
{
    private string root;
    private DevBackupStore store;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "IdleGameDevBackupTests_" + System.Guid.NewGuid().ToString("N"));
        store = new DevBackupStore(root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private static PlayerSaveData Sample(int level) => new PlayerSaveData
    {
        gold = 1.5e9,
        currentStageNumber = 2,
        stats = new PlayerStatsSaveData { level = level, currentExp = 7, learnedSkillIds = new List<int> { 1, 3 } },
    };

    [Test]
    public void WriteThenRead_RoundTripsExactly()
    {
        PlayerSaveData original = Sample(12);

        Assert.IsTrue(store.TryWrite(original, "manual", out string path, out string error), error);
        Assert.IsTrue(store.TryRead(path, out PlayerSaveData read, out error), error);

        Assert.AreEqual(JsonUtility.ToJson(original), JsonUtility.ToJson(read));
    }

    [Test]
    public void ManyWritesInARow_NeverOverwrite_AndListNewestFirst()
    {
        var written = new List<string>();
        for (int i = 1; i <= 5; i++)
        {
            Assert.IsTrue(store.TryWrite(Sample(i), "same-reason", out string path, out string error), error);
            written.Add(path);
        }

        IReadOnlyList<string> listed = store.List();
        Assert.AreEqual(5, listed.Count, "같은 밀리초에 써도 서로 덮어쓰지 않는다");
        CollectionAssert.AreEquivalent(written, listed);

        // 최근 것부터: 이름이 시각으로 시작하므로 이름 역순
        var sorted = new List<string>(listed);
        sorted.Sort(System.StringComparer.Ordinal);
        sorted.Reverse();
        CollectionAssert.AreEqual(sorted, listed);
    }

    [Test]
    public void InvalidData_IsNotWritten()
    {
        PlayerSaveData bad = Sample(3);
        bad.gold = double.NaN;

        Assert.IsFalse(store.TryWrite(bad, "manual", out string path, out _));
        Assert.IsNull(path);
        Assert.AreEqual(0, store.List().Count);
    }

    [TestCase("")]
    [TestCase("{\"stats\":{\"level\":1}")]                     // 잘린 JSON
    [TestCase("{\"stats\":{},\"gold\":0,\"currentStageNumber\":1}")] // 필수 키 없음
    public void CorruptBackup_IsRejected(string content)
    {
        Directory.CreateDirectory(store.Directory);
        string path = Path.Combine(store.Directory, "20260101-000000-000_corrupt.json");
        File.WriteAllText(path, content);

        Assert.IsFalse(store.TryRead(path, out PlayerSaveData data, out string error));
        Assert.IsNull(data);
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }

    [Test]
    public void ReasonWithPathCharacters_StaysInsideBackupFolder()
    {
        Assert.IsTrue(store.TryWrite(Sample(1), "../../evil/name", out string path, out string error), error);

        Assert.AreEqual(Path.GetFullPath(store.Directory), Path.GetFullPath(Path.GetDirectoryName(path)));
    }
}
