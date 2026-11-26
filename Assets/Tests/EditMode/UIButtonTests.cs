using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// UIButton(docs/UI_DESIGN.md 10-6).
// - 한 프레임에 클릭 하나(모든 UIButton 공통). 꺼진 버튼의 클릭은 프레임을 차지하지 않는다.
// - Available = false는 모양만 꺼진 것이고 클릭은 전달된다(누르면 명령이 실패 이유를 돌려준다).
// - 받아들인 클릭만 소리 사건을 낸다. 재생은 UISoundPlayer 한 곳.
// - 화면 UI(프리팹, 씬)에 그냥 Button이 없다.
// EditMode 테스트 안에서는 Time.frameCount가 바뀌지 않으므로 "같은 프레임"이고, 다른 프레임은 게이트에 직접 숫자를 넣어 본다.
[TestFixture]
public class UIButtonTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<UIButtonSound> sounds = new List<UIButtonSound>();
    private System.IDisposable soundSubscription;

    [SetUp]
    public void SetUp()
    {
        UIClickGate.Reset();
        UIButtonEvents.ResetSubscribers();
        sounds.Clear();
        soundSubscription = UIButtonEvents.Clicked.Subscribe(sounds.Add);
    }

    [TearDown]
    public void TearDown()
    {
        soundSubscription.Dispose();
        foreach (GameObject go in objects) Object.DestroyImmediate(go);
        objects.Clear();
        UIClickGate.Reset();
        UIButtonEvents.ResetSubscribers();
    }

    private UIButton MakeButton(out List<int> clicks, UIButtonSound sound = UIButtonSound.Click)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        objects.Add(go);
        Image image = go.AddComponent<Image>();
        UIButton button = go.AddComponent<UIButton>();
        button.targetGraphic = image;
        button.Sound = sound;

        var log = new List<int>();
        button.onClick.AddListener(() => log.Add(1));
        clicks = log;
        return button;
    }

    private static PointerEventData Left() => new PointerEventData(null) { button = PointerEventData.InputButton.Left };

    [Test]
    public void SameFrame_SecondClick_IsIgnored_EvenOnAnotherButton()
    {
        UIButton a = MakeButton(out List<int> aClicks);
        UIButton b = MakeButton(out List<int> bClicks);

        a.OnPointerClick(Left());
        a.OnPointerClick(Left());
        b.OnPointerClick(Left());

        Assert.AreEqual(1, aClicks.Count);
        Assert.AreEqual(0, bClicks.Count, "첫 탭이 모달을 연 그 프레임에 둘째 손가락이 밑의 버튼을 누르지 못한다");
        CollectionAssert.AreEqual(new[] { UIButtonSound.Click }, sounds, "받아들인 클릭만 소리를 낸다");
    }

    [Test]
    public void Gate_AcceptsOnePerFrame()
    {
        Assert.IsTrue(UIClickGate.TryAccept(10));
        Assert.IsFalse(UIClickGate.TryAccept(10));
        Assert.IsTrue(UIClickGate.TryAccept(11), "다음 프레임에는 다시 받는다");
        Assert.IsFalse(UIClickGate.TryAccept(11));
    }

    // 꺼진 버튼(interactable = false)은 눌리지 않으므로 그 프레임을 차지하지 않는다.
    [Test]
    public void NonInteractableClick_DoesNotTakeTheFrame()
    {
        UIButton off = MakeButton(out List<int> offClicks);
        off.interactable = false;
        UIButton on = MakeButton(out List<int> onClicks);

        off.OnPointerClick(Left());
        on.OnPointerClick(Left());

        Assert.AreEqual(0, offClicks.Count);
        Assert.AreEqual(1, onClicks.Count);
    }

    [Test]
    public void NotAvailable_LooksDisabled_ButStillClicks()
    {
        UIButton button = MakeButton(out List<int> clicks);
        ColorBlock colors = button.colors;

        button.Available = false;
        Color disabled = colors.disabledColor * colors.colorMultiplier;
        Assert.That(Vector4.Distance(button.targetGraphic.canvasRenderer.GetColor(), disabled), Is.LessThan(1e-4f), "꺼진 모양");

        button.OnPointerClick(Left());
        Assert.AreEqual(1, clicks.Count, "모양만 꺼졌고 클릭은 전달된다(Presenter가 실패 이유를 알린다)");

        button.Available = true;
        Assert.That(Vector4.Distance(button.targetGraphic.canvasRenderer.GetColor(), colors.normalColor * colors.colorMultiplier), Is.LessThan(1e-4f));
    }

    [Test]
    public void RightClick_IsIgnored_AndDoesNotTakeTheFrame()
    {
        UIButton button = MakeButton(out List<int> clicks);

        button.OnPointerClick(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
        button.OnPointerClick(Left());

        Assert.AreEqual(1, clicks.Count);
    }

    // ── 소리 ──

    [Test]
    public void SoundPlayer_PlaysClipOfTheKind_IgnoresNoneAndMissingClips()
    {
        AudioClip click = AudioClip.Create("click", 441, 1, 44100, false);
        var played = new List<AudioClip>();
        var clips = new Dictionary<UIButtonSound, AudioClip> { [UIButtonSound.Click] = click };
        using (var player = new UISoundPlayer(kind => clips.TryGetValue(kind, out AudioClip c) ? c : null, played.Add))
        {
            UIButton silent = MakeButton(out _, UIButtonSound.None);
            silent.OnPointerClick(Left());
            Assert.IsEmpty(played, "None은 소리가 없다");

            UIClickGate.Reset();
            MakeButton(out _).OnPointerClick(Left());
            CollectionAssert.AreEqual(new[] { click }, played);

            clips.Clear();
            player.Play(UIButtonSound.Click);
            Assert.AreEqual(1, played.Count, "클립이 비어 있으면 나지 않는다(지금 프로젝트 상태)");
        }

        UIButtonEvents.RaiseClicked(UIButtonSound.Click);
        Assert.AreEqual(1, played.Count, "버린 재생기는 듣지 않는다");
        Object.DestroyImmediate(click);
    }

    // ── 쓰임 ──

    [Test]
    public void UIPrefabs_HaveNoPlainButton()
    {
        var offenders = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/UI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Button button in prefab.GetComponentsInChildren<Button>(true))
            {
                if (button.GetType() == typeof(Button)) offenders.Add($"{path}: {button.name}");
            }
        }

        CollectionAssert.IsEmpty(offenders, "화면 UI의 버튼은 UIButton이어야 한다");
    }

    [Test]
    public void MainScene_HasNoPlainButton()
    {
        var temp = new GameObject("ButtonScript", typeof(RectTransform));
        objects.Add(temp);
        MonoScript buttonScript = MonoScript.FromMonoBehaviour(temp.AddComponent<Button>());
        Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(buttonScript, out string buttonGuid, out long _));

        string scene = File.ReadAllText("Assets/Scenes/MainScene.unity");
        StringAssert.DoesNotContain("guid: " + buttonGuid, scene, "씬에 직접 놓인 그냥 Button이 있다");
    }
}
