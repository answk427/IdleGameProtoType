using System;
using UnityEngine;

// UI 소리를 내는 한 곳. 버튼 클릭(UIButtonEvents.Clicked)을 구독해서 종류에 맞는 클립을 낸다.
// 찾기와 재생은 함수로 받는다(UIRoot는 UISoundSet.Get과 AudioSource.PlayOneShot, 테스트는 가짜).
public sealed class UISoundPlayer : IDisposable
{
    private readonly Func<UIButtonSound, AudioClip> findClip;
    private readonly Action<AudioClip> play;
    private readonly IDisposable subscription;

    public UISoundPlayer(Func<UIButtonSound, AudioClip> findClip, Action<AudioClip> play)
    {
        this.findClip = findClip ?? throw new ArgumentNullException(nameof(findClip));
        this.play = play ?? throw new ArgumentNullException(nameof(play));
        subscription = UIButtonEvents.Clicked.Subscribe(Play);
    }

    public void Play(UIButtonSound sound)
    {
        if (sound == UIButtonSound.None) return;

        AudioClip clip = findClip(sound);
        if (clip != null) play(clip);
    }

    public void Dispose() => subscription.Dispose();
}
