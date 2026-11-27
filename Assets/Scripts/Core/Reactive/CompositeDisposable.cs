using System;
using System.Collections.Generic;

// 구독 손잡이 묶음. 창 수명에 묶어 두고 닫힐 때 한 번에 해제한다 (docs/UI_DESIGN.md 5-7).
public sealed class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> items = new List<IDisposable>();
    private bool isDisposed;

    public void Add(IDisposable item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        // 이미 닫힌 창에 늦게 추가된 구독은 남기지 않고 바로 해제한다.
        if (isDisposed) item.Dispose();
        else items.Add(item);
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            try
            {
                items[i].Dispose();
            }
            catch (Exception e)
            {
                GameLog.Exception(e);
            }
        }
        items.Clear();
    }
}
