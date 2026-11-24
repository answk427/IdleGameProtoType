using System.Collections;
using UnityEngine;

public class HitEffect : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color hitColor = Color.red;
    [SerializeField] private float duration = 0.1f;

    private Color originalColor;

    private CombatantHost owner;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;

        owner = GetComponentInParent<CombatantHost>();
    }

    private void OnEnable()
    {
        if (owner != null)
        {
            owner.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (owner != null)
        {
            owner.OnDamaged -= HandleDamaged;
        }
    }

    private void HandleDamaged()
    {
        StopAllCoroutines();
        StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(duration);
        spriteRenderer.color = originalColor;
    }
}
