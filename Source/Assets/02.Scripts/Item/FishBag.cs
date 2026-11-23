// Keep the existing FishBag component and prefab compatible with fish collection.
public class FishBag : Inventory
{
    float _maxWeight;

    public float MaxWeight => _maxWeight;

    public void SetMaxWeight(float maxWeight)
    {
        if (maxWeight < 0f || float.IsNaN(maxWeight) || float.IsInfinity(maxWeight)) return;
        _maxWeight = maxWeight;
    }

    public void GetFishFillet(ItemData itemData, int amount)
    {
        TryAdd(itemData, amount);
    }
}
