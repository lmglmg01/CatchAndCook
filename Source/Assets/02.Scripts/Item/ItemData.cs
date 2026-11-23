using UnityEngine;

public enum ItemCategory
{
    Ingredient,
    Consumable,
    Quest
}

[CreateAssetMenu(fileName = "ItemData", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("아이템 정보")]
    [SerializeField] string itemId;
    [SerializeField] string itemName;

    [Header("아이템 아이콘")]
    [SerializeField] Sprite itemIcon;

    [Header("아이템 설명")]
    [SerializeField, TextArea(3, 6)] string itemDesc;

    [Header("판매 가격")]
    [SerializeField, Min(0f)] float sellPrice;

    public string ItemId => itemId;
    public string ItemName => itemName;
    public Sprite ItemIcon => itemIcon;
    public string ItemDesc => itemDesc;
    public virtual ItemCategory Category => ItemCategory.Ingredient;
    public float SellPrice => sellPrice;
    public bool IsSellable => Category != ItemCategory.Quest;
}
