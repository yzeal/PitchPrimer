using UnityEngine;

public enum SoundVisualType
{
    simple3D,
    simple2D,
    floating3D
}

public class SoundVisual : MonoBehaviour
{
    public SoundVisualType type = SoundVisualType.simple3D;

    public GameObject visualObject;
    public GameObject colorObject2D; // Für 2D-Objekte, die eine separate Farbkomponente haben

    private Color currentColor;

    public Color GetColor()
    {
        return currentColor;
    }

    public void SetColor(Color c)
    {
        switch (type)
        {
            case SoundVisualType.simple3D:
                // Für 3D-Objekte Renderer-Material setzen
                if (visualObject != null)
                {
                    var renderer = visualObject.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = c;
                        currentColor = c;
                    }
                }
                break;
                
            case SoundVisualType.simple2D:
                // Für 2D-Objekte SpriteRenderer oder Image-Komponente setzen
                if (visualObject != null)
                {
                    var spriteRenderer = visualObject.GetComponent<SpriteRenderer>();
                    if (colorObject2D != null)
                        spriteRenderer = colorObject2D.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = c;
                        currentColor = c;
                    }
                    else
                    {
                        // Fallback für UI-Elemente
                        var image = visualObject.GetComponent<UnityEngine.UI.Image>();
                        if (colorObject2D != null)
                            image = colorObject2D.GetComponent<UnityEngine.UI.Image>();
                        if (image != null)
                        {
                            image.color = c;
                            currentColor = c;
                        }
                    }
                }
                break;

            case SoundVisualType.floating3D:
                // Für 3D-Objekte Renderer-Material setzen
                if (visualObject != null)
                {
                    var renderer = visualObject.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = c;
                        currentColor = c;
                    }
                }
                break;
        }
    }

    public void SetScale(Vector3 s)
    {
        switch (type)
        {
            case SoundVisualType.simple3D:
                // Für 3D-Objekte direkt transform.localScale setzen
                if (visualObject != null)
                {
                    visualObject.transform.localScale = s;
                }
                break;
                
            case SoundVisualType.simple2D:
                // Für 2D-Objekte unterschiedliche Ansätze je nach Komponente
                if (visualObject != null)
                {
                    var rectTransform = visualObject.GetComponent<RectTransform>();
                    var colorTransform = colorObject2D != null ? colorObject2D.GetComponent<RectTransform>() : null;
                    if (rectTransform != null)
                    {
                        // UI-Element: sizeDelta für Width/Height, localScale für Z
                        rectTransform.sizeDelta = new Vector2(s.x * 100f, s.y * 100f);
                        rectTransform.localScale = new Vector3(1f, 1f, s.z);
                        if(colorTransform != null) 
                        {
                            colorTransform.sizeDelta = colorTransform.parent.transform.GetComponent<RectTransform>().sizeDelta;
                            colorTransform.localScale = new Vector3(1f, 1f, 1f);
                        }
                    }
                    else
                    {
                        // 2D-Sprite: localScale verwenden
                        visualObject.transform.localScale = s;
                        if (colorTransform != null)
                        {
                            colorTransform.transform.localScale = s;
                        }
                    }
                }
                break;

            case SoundVisualType.floating3D:
                // Für 3D-Objekte direkt transform.localScale setzen
                if (visualObject != null)
                {
                    visualObject.transform.localPosition = new Vector3(0f, s.y/2f, 0f);
                }
                break;
        }
    }

    public void SetScaleY(float y)
    {
        switch (type)
        {
            case SoundVisualType.simple3D:
                // Für 3D-Objekte nur Y-Komponente der localScale ändern
                if (visualObject != null)
                {
                    Vector3 currentScale = visualObject.transform.localScale;
                    currentScale.y = y;
                    visualObject.transform.localScale = currentScale;
                }
                break;
                
            case SoundVisualType.simple2D:
                // Für 2D-Objekte unterschiedliche Ansätze je nach Komponente
                if (visualObject != null)
                {
                    var rectTransform = visualObject.GetComponent<RectTransform>();
                    if (rectTransform != null)
                    {
                        // UI-Element: Height über sizeDelta setzen
                        Vector2 currentSize = rectTransform.sizeDelta;
                        currentSize.y = y * 100f;
                        rectTransform.sizeDelta = currentSize;
                    }
                    else
                    {
                        // 2D-Sprite: Y-Komponente der localScale ändern
                        Vector3 currentScale = visualObject.transform.localScale;
                        currentScale.y = y;
                        visualObject.transform.localScale = currentScale;
                    }
                }
                break;

            case SoundVisualType.floating3D:
                // Für 3D-Objekte nur Y-Komponente der localScale ändern
                if (visualObject != null)
                {
                    Vector3 currentScale = visualObject.transform.localScale;
                    currentScale.y = y;
                    visualObject.transform.localScale = currentScale;
                }
                break;
        }
    }
}
