using UnityEngine;

public class CellRender : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    public void Hide()
    {
        _spriteRenderer.enabled = false;
    }
    public void ShowHighLight()
    {
        _spriteRenderer.enabled = true;
        _spriteRenderer.color = Color.red;
    }
    public void ShowNormal()
    {
        _spriteRenderer.enabled = true;
        Color c = Color.white;
        c.a = 1f;
        _spriteRenderer.color = c;
    }

    public void ShowBlur()
    {
        _spriteRenderer.enabled = true;
        Color c = Color.white;
        c.a = 0.25f;
        _spriteRenderer.color = c;
    }

    private void OnValidate()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }
}
