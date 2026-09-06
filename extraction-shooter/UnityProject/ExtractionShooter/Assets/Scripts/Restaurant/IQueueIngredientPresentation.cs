using System.Collections;
using System.Collections.Generic;

// Optional presentation boundary. Ingredient consumption and queue rules remain in RestaurantPanel.
public interface IQueueIngredientPresentation
{
    bool CanPresentQueueIngredients { get; }
    IEnumerator PlayQueueIngredients(List<InventoryManager.IngredientFlySource> sources, DishQueueSlot target,
        float duration, float spawnInterval, float landingDuration);
}
