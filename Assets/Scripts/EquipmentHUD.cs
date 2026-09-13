using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A read-only view of the existing direct-swap equipment system.
public class EquipmentHUD : MonoBehaviour
{
    [Serializable]
    public class SlotView
    {
        public ItemSlot slot;
        public Image frame;
        public HUDFlash flash;
        public TextMeshProUGUI itemName;
        public TextMeshProUGUI rarity;
    }

    public PlayerEquipment equipment;
    public SlotView[] slots;
    public GameObject comparisonPanel;
    public TextMeshProUGUI swapPrompt;
    public TextMeshProUGUI currentItem;
    public TextMeshProUGUI nearbyItem;
    public CanvasGroup equipMessageGroup;
    public TextMeshProUGUI equipMessage;
    private float messageUntil;
    private readonly EquippedItem[] displayedItems = new EquippedItem[5];
    private EquippedItem displayedPickup;
    private EquippedItem displayedCurrent;
    private bool initialized;

    private void OnEnable()
    {
        if (equipment != null) equipment.ItemEquipped += OnItemEquipped;
    }

    private void OnDisable()
    {
        if (equipment != null) equipment.ItemEquipped -= OnItemEquipped;
        if (equipMessageGroup != null) equipMessageGroup.alpha = 0f;
        messageUntil = 0f;
    }

    private void OnItemEquipped(ItemSlot slot, EquippedItem item)
    {
        foreach (var view in slots)
            if (view.slot == slot && view.flash != null)
            {
                view.flash.SetBaseColor(RarityColor.Get(item.Rarity));
                view.flash.Trigger();
            }
        if (equipMessage == null || equipMessageGroup == null) return;
        equipMessage.text = "Equipped: " + item.Definition.ItemName;
        equipMessage.color = RarityColor.Get(item.Rarity);
        equipMessageGroup.alpha = 1f;
        messageUntil = Time.unscaledTime + 2.2f;
    }

    private void LateUpdate()
    {
        if (equipMessageGroup != null)
            equipMessageGroup.alpha = Mathf.Clamp01((messageUntil - Time.unscaledTime) / .5f);
        if (equipment == null)
        {
            if (comparisonPanel != null) comparisonPanel.SetActive(false);
            return;
        }
        for (int i = 0; i < slots.Length; i++)
        {
            var view = slots[i];
            var item = equipment.GetEquipped(view.slot);
            if (initialized && displayedItems[i] == item) continue;
            displayedItems[i] = item;
            Color color = item != null ? RarityColor.Get(item.Rarity) : new Color(.38f, .39f, .4f);
            if (view.flash != null) view.flash.SetBaseColor(color);
            else view.frame.color = color;
            view.itemName.text = item?.Definition != null ? item.Definition.ItemName : "Empty";
            view.itemName.color = item != null ? Color.white : Color.gray;
            view.rarity.text = item != null ? RarityName(item.Rarity) : "-";
            view.rarity.color = color;
        }
        initialized = true;
        var pickup = equipment.TargetedPickup;
        var candidate = pickup != null && pickup.isActiveAndEnabled ? pickup.Item : null;
        bool show = candidate?.Definition != null;
        comparisonPanel.SetActive(show);
        if (!show) { displayedPickup = null; return; }
        var current = equipment.GetEquipped(candidate.Definition.Slot);
        if (displayedPickup == candidate && displayedCurrent == current) return;
        displayedPickup = candidate;
        displayedCurrent = current;
        string slotName = candidate.Definition.Slot == ItemSlot.Pants ? "LEGS" : candidate.Definition.Slot.ToString().ToUpperInvariant();
        swapPrompt.text = "[E] " + (current == null ? "EQUIP" : "SWAP") + "  /  " + slotName;
        currentItem.text = Describe(current);
        nearbyItem.text = Describe(candidate);
    }

    private static string RarityName(ItemRarity rarity) => rarity.ToString() == "SuperRare" ? "SUPER RARE" : rarity.ToString().ToUpperInvariant();

    private static string Describe(EquippedItem item)
    {
        if (item?.Definition == null) return "<color=#888888>Empty slot</color>";
        var text = new StringBuilder();
        text.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(RarityColor.Get(item.Rarity))).Append('>')
            .Append(item.Definition.ItemName).Append("</color>\n<size=85%>").Append(RarityName(item.Rarity)).Append("</size>\n");
        if (item.RolledDamage > 0) text.Append("\n+").Append(item.RolledDamage).Append(" Damage");
        foreach (var affix in item.Affixes)
        {
            if (affix.definition == null) continue;
            var stat = affix.definition.StatType;
            bool percent = stat == StatType.AttackSpeed || stat == StatType.CritChance || stat == StatType.AbilityCooldownReduction || stat == StatType.MoveSpeed;
            string name = System.Text.RegularExpressions.Regex.Replace(stat.ToString(), "([a-z])([A-Z])", "$1 $2");
            text.Append("\n+").Append(Mathf.RoundToInt(affix.rolledValue * (percent ? 100 : 1))).Append(percent ? "% " : " ").Append(name);
        }
        if (item.Definition.AbilityDefinition != null) text.Append("\n\nAbility: ").Append(item.Definition.AbilityDefinition.AbilityName);
        if (item.Definition.DashDefinition != null) text.Append("\n\nDash: ").Append(item.Definition.DashDefinition.DashName);
        return text.ToString();
    }
}
