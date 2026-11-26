using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

// 세이브 오류 창(docs/UI_DESIGN.md 6-4, 10-2): 이유별 문구, 화면에 파일 경로를 내지 않음, 종료는 한 번,
// 그리고 창이 닫히지 않는 옵션(System 모달, 바깥 터치·뒤로가기로 닫지 않음).
[TestFixture]
public class SaveErrorPresenterTests
{
    private sealed class FakeView : ISaveErrorView
    {
        public string Title, Body, Reason, QuitLabel;
        public event Action QuitTapped;

        public void SetMessage(string title, string body, string reason)
        {
            Title = title;
            Body = body;
            Reason = reason;
        }

        public void SetQuitLabel(string label) => QuitLabel = label;

        public void TapQuit() => QuitTapped?.Invoke();
    }

    private FakeView view;
    private int quitCalls;

    [SetUp]
    public void SetUp()
    {
        view = new FakeView();
        quitCalls = 0;
    }

    private SaveErrorPresenter Bind(SaveErrorArgs args) => new SaveErrorPresenter(view, args, () => quitCalls++);

    [Test]
    public void Corrupt_ShowsSafetyMessage_AndValidatorReason()
    {
        const string validatorMessage = "세이브 파일이 손상되었습니다: stats.level: 없거나 1 이상의 정수가 아님";
        Bind(SaveErrorArgs.Unreadable(new InvalidDataException(validatorMessage)));

        Assert.AreEqual("진행도를 불러오지 못했습니다", view.Title);
        Assert.AreEqual("세이브 파일을 읽을 수 없어 게임을 시작하지 않았습니다. 세이브 파일은 바꾸지 않았습니다.", view.Body);
        Assert.AreEqual(validatorMessage, view.Reason);
        Assert.AreEqual("게임 종료", view.QuitLabel);
    }

    [TestCase(typeof(IOException))]
    [TestCase(typeof(UnauthorizedAccessException))]
    [TestCase(typeof(FileNotFoundException))]
    public void ReadError_ShowsTypeOnly_NotMessageWithPath(Type exceptionType)
    {
        const string path = @"C:\Users\someone\AppData\LocalLow\DefaultCompany\IdleGameProtoType\player_save.json";
        var error = (Exception)Activator.CreateInstance(exceptionType, $"Could not read '{path}'");

        Bind(SaveErrorArgs.Unreadable(error));

        Assert.AreEqual($"읽기 오류 ({exceptionType.Name})", view.Reason);
        StringAssert.DoesNotContain("someone", view.Reason);
        StringAssert.DoesNotContain(@":\", view.Reason);
    }

    [Test]
    public void StageMissing_SameWindow_ReasonNamesStage()
    {
        Bind(SaveErrorArgs.StageMissing(999));

        Assert.AreEqual(SaveErrorPresenter.Title, view.Title);
        Assert.AreEqual(SaveErrorPresenter.Body, view.Body);
        Assert.AreEqual("저장된 스테이지가 게임 데이터에 없음 (스테이지 999)", view.Reason);
    }

    [Test]
    public void Quit_CallsQuitOnce_EvenWhenTappedAgain()
    {
        Bind(SaveErrorArgs.StageMissing(5));

        view.TapQuit();
        view.TapQuit();

        Assert.AreEqual(1, quitCalls);
    }

    [Test]
    public void Dispose_StopsListening()
    {
        Bind(SaveErrorArgs.StageMissing(5)).Dispose();

        view.TapQuit();

        Assert.AreEqual(0, quitCalls);
    }

    [Test]
    public void UnreadableWithoutError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SaveErrorArgs.Unreadable(null));
    }

    [Test]
    public void Window_IsSystemModal_ClosedNeitherByBackNorOutside()
    {
        var host = new GameObject("SaveErrorWindowForTest");
        try
        {
            WindowOptions options = host.AddComponent<SaveErrorWindow>().Options;

            Assert.AreEqual(UISortBand.System, options.Band);
            Assert.IsTrue(options.Modal, "모달 딤이 밑의 HUD·탭바 터치를 막는다");
            Assert.IsFalse(options.CloseOnOutside);
            Assert.IsFalse(options.CloseOnBack);
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
