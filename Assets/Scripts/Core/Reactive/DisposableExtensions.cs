using System;

public static class DisposableExtensions
{
    public static T AddTo<T>(this T disposable, CompositeDisposable composite) where T : IDisposable
    {
        composite.Add(disposable);
        return disposable;
    }
}
