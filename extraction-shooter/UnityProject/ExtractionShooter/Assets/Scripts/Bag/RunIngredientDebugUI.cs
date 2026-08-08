using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Temporary lightweight display for verifying the run-only ingredient bag.
/// It is created at runtime and requires no scene or prefab configuration.
/// </summary>
public class RunIngredientDebugUI : MonoBehaviour
{
    private InventoryManager inventory;
    private Text ingredientText;

    public static void EnsureExists(InventoryManager targetInventory)
    {
        if (targetInventory == null) return;

        RunIngredientDebugUI existing = FindObjectOfType<RunIngredientDebugUI>(true);
        if (existing != null)
        {
            existing.Bind(targetInventory);
            return;
        }

        GameObject root = new GameObject("RunIngredientDebugUI");
        RunIngredientDebugUI ui = root.AddComponent<RunIngredientDebugUI>();
        DontDestroyOnLoad(root);
        ui.Bind(targetInventory);
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = new GameObject("IngredientDebugPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(transform, false);

        RectTransform panel = panelObject.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 1f);
        panel.anchorMax = new Vector2(0.5f, 1f);
        panel.pivot = new Vector2(0.5f, 1f);
        panel.anchoredPosition = new Vector2(0f, -24f);
        panel.sizeDelta = new Vector2(560f, 200f);
        panelObject.GetComponent<Image>().color = new Color(0.38f, 0.03f, 0.03f, 0.94f);

        GameObject textObject = new GameObject("IngredientDebugText", typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);

        ingredientText = textObject.GetComponent<Text>();
        // Unity 2022.2+ removed the built-in "Arial.ttf"; it now returns null and the
        // Text renders no glyphs (red panel shows, text invisible). Prefer the new
        // "LegacyRuntime.ttf" and fall back to Arial for older editors.
        Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtinFont == null)
            builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        ingredientText.font = builtinFont;
        ingredientText.fontSize = 28;
        ingredientText.color = new Color(1f, 0.92f, 0.2f, 1f);
        ingredientText.alignment = TextAnchor.UpperCenter;
        ingredientText.horizontalOverflow = HorizontalWrapMode.Wrap;
        ingredientText.verticalOverflow = VerticalWrapMode.Truncate;
        textObject.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

        Refresh();
    }

    private void Bind(InventoryManager targetInventory)
    {
        if (inventory == targetInventory) return;

        if (inventory != null)
            inventory.OnRunIngredientChanged -= OnIngredientChanged;

        inventory = targetInventory;
        inventory.OnRunIngredientChanged += OnIngredientChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnRunIngredientChanged -= OnIngredientChanged;
    }

    private void OnIngredientChanged(ResourceType type, int oldCount, int newCount)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (ingredientText == null) return;

        StringBuilder builder = new StringBuilder("RUN INGREDIENTS (NO SLOTS)\n");
        if (inventory == null)
        {
            builder.Append("InventoryManager NOT FOUND");
        }
        else
        {
            var counts = inventory.GetAllRunIngredientCounts()
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key.ToString())
                .ToList();

            if (counts.Count == 0)
            {
                builder.Append("EMPTY");
            }
            else
            {
                foreach (var pair in counts)
                    builder.Append(pair.Key).Append(": ").Append(pair.Value).Append('\n');
            }
        }

        ingredientText.text = builder.ToString();
    }
}
