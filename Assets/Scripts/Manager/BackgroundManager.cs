using UnityEngine;

public class BackgroundManager : MonoBehaviour
{
    public static BackgroundManager Instance;

    // 스크롤 되는 배경
    [SerializeField] private SpriteRenderer backgroundRenderer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ChangeBackground(Sprite newSprite)
    {
        if (newSprite == null) return;

        backgroundRenderer.sprite = newSprite;
    }
}