using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class CombatHUDBuilder
{
    private static readonly Color Gold = new Color(.78f, .64f, .38f);
    private static Sprite border;
    private static TMP_FontAsset font;
    private static SerializedObject ui;

    [MenuItem("Gladiators/Build Combat HUD")]
    public static void Build()
    {
        var gameUI = Object.FindAnyObjectByType<GameUI>();
        if (gameUI == null) throw new System.InvalidOperationException("Scene needs a Canvas with GameUI.");
        if (gameUI.transform.Find("Combat HUD") != null) { UpgradeSlots(); return; }
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Combat HUD");
        ui = new SerializedObject(gameUI);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        border = Import("Assets/AssetPacks/kenney_fantasy-ui-borders/PNG/Default/Border/panel-border-009.png", true);
        var ability = Import("Assets/AssetPacks/Skill Icon Pack Wenrexa 4.0/256x256px/Normal/15 Icon.png", false);
        var dash = Import("Assets/AssetPacks/Skill Icon Pack Wenrexa 4.0/256x256px/Normal/30 Icon.png", false);
        var fields = new[] { "playerHealthText", "staggerText", "waveText", "enemiesRemainingText", "equippedItemsText", "abilityCooldownText", "dashCooldownText", "comboText" };
        foreach (var field in fields)
        {
            var old = ui.FindProperty(field).objectReferenceValue as TextMeshProUGUI;
            if (old == null) continue;
            Undo.RecordObject(old.gameObject, "Hide original HUD text");
            old.gameObject.SetActive(false);
        }
        var scaler = gameUI.GetComponent<CanvasScaler>();
        Undo.RecordObject(scaler, "Scale combat HUD");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        var root = Rect("Combat HUD", gameUI.transform, Vector2.zero, Vector2.zero, Vector2.zero);
        root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = Vector2.zero; root.offsetMax = Vector2.zero;
        root.SetAsFirstSibling();
        var vital = Panel("Vitals", root, new Vector2(0,1), new Vector2(28,-28), new Vector2(420,154));
        Label("Title", vital, "GLADIATOR", 20, new Vector2(22,-14), new Vector2(370,28), Gold);
        Bar(vital, "Health", new Vector2(22,-53), new Vector2(376,34), new Color(.7f,.13f,.16f), "healthFill");
        Bind("playerHealthText", Label("Health value", vital, "Health: 100 / 100", 19, new Vector2(30,-55), new Vector2(360,30), Color.white));
        Bar(vital, "Stagger", new Vector2(22,-106), new Vector2(376,15), new Color(.9f,.65f,.23f), "staggerFill");
        Label("Stagger title", vital, "STAGGER", 12, new Vector2(22,-88), new Vector2(110,18), Gold);
        Bind("staggerText", Label("Stagger value", vital, "Stagger: 0 / 200", 12, new Vector2(220,-124), new Vector2(180,18), Gold));
        var wave = Panel("Arena status", root, new Vector2(.5f,1), new Vector2(0,-28), new Vector2(280,100));
        Bind("waveText", Label("Wave", wave, "Wave: 1 / 3", 27, new Vector2(20,-13), new Vector2(240,38), Gold));
        Bind("enemiesRemainingText", Label("Enemies", wave, "Enemies: 0", 18, new Vector2(20,-56), new Vector2(240,28), Color.white));
        var skills = Panel("Skills", root, new Vector2(.5f,0), new Vector2(0,28), new Vector2(330,180));
        Skill(skills, "Ability", "Q", ability, 35, "abilityIcon", "abilityCooldownFill", "abilityCooldownText");
        Skill(skills, "Dash", "L CTRL", dash, 195, "dashIcon", "dashCooldownFill", "dashCooldownText");
        Bind("comboText", Label("Combo", skills, "Combo: 0", 16, new Vector2(105,-150), new Vector2(140,25), Gold));
        var gear = Panel("Equipment", root, new Vector2(1,1), new Vector2(-28,-28), new Vector2(310,490));
        Label("Equipment title", gear, "EQUIPMENT", 20, new Vector2(20,-15), new Vector2(270,30), Gold);
        var gearText = Label("Equipment details", gear, "", 16, new Vector2(20,-58), new Vector2(270,410), Color.white);
        gearText.enableAutoSizing = true; gearText.fontSizeMin = 11; gearText.fontSizeMax = 16;
        Bind("equippedItemsText", gearText);
        var player = Object.FindAnyObjectByType<PlayerCombat>();
        if (player != null)
        {
            Bind("playerCombat", player); Bind("playerHealth", player.GetComponent<Health>());
            Bind("playerStagger", player.GetComponent<Stagger>()); Bind("playerEquipment", player.GetComponent<PlayerEquipment>());
        }
        ui.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(gameUI.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        UpgradeSlots();
    }

    [MenuItem("Gladiators/Upgrade HUD Gear Slots")]
    public static void UpgradeSlots()
    {
        var gameUI = Object.FindAnyObjectByType<GameUI>();
        if (gameUI == null) return;
        var root = gameUI.transform.Find("Combat HUD");
        if (root == null) return;
        if (root.Find("Gear slots") != null) { UpgradeAttackCooldown(); return; }
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Upgrade HUD gear slots");
        ui = new SerializedObject(gameUI);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        border = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AssetPacks/kenney_fantasy-ui-borders/PNG/Default/Border/panel-border-009.png");
        var oldGear = root.Find("Equipment");
        if (oldGear != null) { Undo.RecordObject(oldGear.gameObject, "Hide equipment text panel"); oldGear.gameObject.SetActive(false); }
        var row = Rect("Gear slots", root, new Vector2(1,1), new Vector2(-28,-28), new Vector2(580,118));
        var view = Undo.AddComponent<EquipmentHUD>(row.gameObject);
        view.equipment = ui.FindProperty("playerEquipment").objectReferenceValue as PlayerEquipment;
        view.slots = new EquipmentHUD.SlotView[5];
        var names = new[] { "HEAD", "CHEST", "LEGS", "BOOTS", "WEAPON" };
        for (int i = 0; i < 5; i++)
        {
            var slot = Panel(names[i],row,new Vector2(0,1),new Vector2(i*118,0),new Vector2(108,118));
            CenterLabel("Slot",slot,names[i],14,new Vector2(5,-10),new Vector2(98,22),Gold);
            var itemName = CenterLabel("Item",slot,"Empty",17,new Vector2(7,-35),new Vector2(94,48),Color.gray);
            itemName.enableAutoSizing = true; itemName.fontSizeMin = 12; itemName.fontSizeMax = 17;
            var rarity = CenterLabel("Rarity",slot,"-",11,new Vector2(4,-89),new Vector2(100,20),Color.gray);
            var frame = slot.Find("Fantasy frame").GetComponent<Image>(); frame.color = Color.gray;
            view.slots[i] = new EquipmentHUD.SlotView { slot = (ItemSlot)i, frame = frame, itemName = itemName, rarity = rarity };
        }
        var comparison = Panel("Swap comparison",root,new Vector2(1,1),new Vector2(-28,-162),new Vector2(580,320));
        view.comparisonPanel = comparison.gameObject;
        view.swapPrompt = CenterLabel("Swap prompt",comparison,"[E] SWAP",20,new Vector2(16,-12),new Vector2(548,32),Gold);
        CenterLabel("Current heading",comparison,"EQUIPPED",13,new Vector2(18,-54),new Vector2(262,22),Gold);
        CenterLabel("Pickup heading",comparison,"ON THE GROUND",13,new Vector2(300,-54),new Vector2(262,22),Gold);
        Picture("Divider",comparison,new Vector2(289,-55),new Vector2(1,245),new Color(.4f,.34f,.23f));
        view.currentItem = Label("Current item",comparison,"",18,new Vector2(20,-87),new Vector2(250,212),Color.white);
        view.nearbyItem = Label("Nearby item",comparison,"",18,new Vector2(308,-87),new Vector2(250,212),Color.white);
        foreach (var text in new[] { view.currentItem, view.nearbyItem }) { text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 18; }
        comparison.gameObject.SetActive(false);
        var skills = (RectTransform)root.Find("Skills");
        Undo.RecordObject(skills, "Expand skill strip"); skills.sizeDelta = new Vector2(430,180);
        var outer = (RectTransform)skills.Find("Fantasy frame"); Undo.RecordObject(outer,"Resize skill frame"); outer.sizeDelta = skills.sizeDelta;
        AlignSkill(skills,"Ability",150);
        AlignSkill(skills,"Dash",290);
        var combo = skills.Find("Combo").GetComponent<TextMeshProUGUI>();
        Undo.RecordObject(combo,"Center combo"); Undo.RecordObject(combo.rectTransform,"Center combo");
        combo.alignment = TextAlignmentOptions.Center; combo.rectTransform.anchoredPosition = new Vector2(15,-153); combo.rectTransform.sizeDelta = new Vector2(400,24);
        var icon = ui.FindProperty("abilityIcon").objectReferenceValue as Image;
        Picture("Basic attack icon",skills,new Vector2(30,-55),new Vector2(72,72),new Color(.85f,.85f,.85f),icon.sprite);
        var basicFrame = Picture("Basic attack border",skills,new Vector2(30,-55),new Vector2(72,72),Gold,border); basicFrame.type = Image.Type.Sliced;
        CenterLabel("Basic attack binding",skills,"LMB",14,new Vector2(16,-29),new Vector2(100,24),Gold);
        CenterLabel("Basic attack label",skills,"ATTACK",13,new Vector2(16,-130),new Vector2(100,22),Color.white);
        EditorUtility.SetDirty(view);
        EditorSceneManager.MarkSceneDirty(gameUI.gameObject.scene);
        Selection.activeGameObject = row.gameObject;
        UpgradeAttackCooldown();
    }

    [MenuItem("Gladiators/Upgrade Attack Cooldown")]
    public static void UpgradeAttackCooldown()
    {
        var gameUI = Object.FindAnyObjectByType<GameUI>();
        if (gameUI == null) return;
        var skills = gameUI.transform.Find("Combat HUD/Skills");
        if (skills == null) return;
        if (skills.Find("Basic attack cooldown") != null) { UpgradeFeedback(); return; }
        var icon = skills.Find("Basic attack icon").GetComponent<Image>();
        var overlay = Picture("Basic attack cooldown",skills,icon.rectTransform.anchoredPosition,icon.rectTransform.sizeDelta,new Color(0,0,0,.8f),icon.sprite);
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillAmount = 0f;
        overlay.transform.SetSiblingIndex(icon.transform.GetSiblingIndex()+1);
        var serialized = new SerializedObject(gameUI);
        serialized.FindProperty("attackCooldownFill").objectReferenceValue = overlay;
        serialized.FindProperty("attackCooldownText").objectReferenceValue = skills.Find("Basic attack label").GetComponent<TextMeshProUGUI>();
        serialized.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(gameUI.gameObject.scene);
        UpgradeFeedback();
    }

    [MenuItem("Gladiators/Upgrade HUD Feedback")]
    public static void UpgradeFeedback()
    {
        var gameUI = Object.FindAnyObjectByType<GameUI>();
        if (gameUI == null) return;
        var root = gameUI.transform.Find("Combat HUD");
        if (root == null || root.Find("Equip message") != null) return;
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Add HUD feedback and heavy attack");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        border = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AssetPacks/kenney_fantasy-ui-borders/PNG/Default/Border/panel-border-009.png");
        var skills = (RectTransform)root.Find("Skills");
        Undo.RecordObject(skills,"Expand skill strip"); skills.sizeDelta = new Vector2(540,180);
        var frame = (RectTransform)skills.Find("Fantasy frame"); Undo.RecordObject(frame,"Expand frame"); frame.sizeDelta = skills.sizeDelta;
        AlignSkill(skills,"Ability",260);
        AlignSkill(skills,"Dash",400);
        var combo = (RectTransform)skills.Find("Combo"); Undo.RecordObject(combo,"Center combo"); combo.sizeDelta = new Vector2(510,24);
        var basic = skills.Find("Basic attack icon").GetComponent<Image>();
        Picture("Heavy attack icon",skills,new Vector2(132,-55),new Vector2(72,72),new Color(1f,.65f,.4f),basic.sprite);
        var overlay = Picture("Heavy attack cooldown",skills,new Vector2(132,-55),new Vector2(72,72),new Color(0,0,0,.8f),basic.sprite);
        overlay.type = Image.Type.Filled; overlay.fillMethod = Image.FillMethod.Radial360; overlay.fillAmount = 0;
        var heavyFrame = Picture("Heavy attack border",skills,new Vector2(132,-55),new Vector2(72,72),Gold,border); heavyFrame.type = Image.Type.Sliced;
        CenterLabel("Heavy attack binding",skills,"RMB",14,new Vector2(118,-29),new Vector2(100,24),Gold);
        var label = CenterLabel("Heavy attack label",skills,"HEAVY",13,new Vector2(118,-130),new Vector2(100,22),Color.white);
        var serialized = new SerializedObject(gameUI);
        serialized.FindProperty("heavyCooldownFill").objectReferenceValue = overlay;
        serialized.FindProperty("heavyCooldownText").objectReferenceValue = label;
        serialized.FindProperty("abilityReadyFlash").objectReferenceValue = Undo.AddComponent<HUDFlash>(skills.Find("Ability border").gameObject);
        serialized.FindProperty("dashReadyFlash").objectReferenceValue = Undo.AddComponent<HUDFlash>(skills.Find("Dash border").gameObject);
        serialized.ApplyModifiedProperties();
        var gear = root.Find("Gear slots").GetComponent<EquipmentHUD>();
        Undo.RecordObject(gear,"Wire equipment feedback");
        foreach (var slot in gear.slots) slot.flash = Undo.AddComponent<HUDFlash>(slot.frame.gameObject);
        var message = Panel("Equip message",root,new Vector2(.5f,0),new Vector2(0,222),new Vector2(480,48));
        gear.equipMessageGroup = Undo.AddComponent<CanvasGroup>(message.gameObject);
        gear.equipMessageGroup.alpha = 0; gear.equipMessageGroup.interactable = false; gear.equipMessageGroup.blocksRaycasts = false;
        gear.equipMessage = CenterLabel("Message",message,"",21,new Vector2(15,-5),new Vector2(450,38),Color.white);
        gear.equipMessage.enableAutoSizing = true; gear.equipMessage.fontSizeMin = 14; gear.equipMessage.fontSizeMax = 21;
        EditorUtility.SetDirty(gear);
        EditorSceneManager.MarkSceneDirty(gameUI.gameObject.scene);
    }

    private static TextMeshProUGUI CenterLabel(string name, Transform parent, string value, float size, Vector2 position, Vector2 dimensions, Color color)
    {
        var label = Label(name,parent,value,size,position,dimensions,color); label.alignment = TextAlignmentOptions.Center; return label;
    }

    private static void AlignSkill(Transform parent, string name, float x)
    {
        foreach (var suffix in new[] { " icon", " cooldown", " border" })
        {
            var rect = (RectTransform)parent.Find(name + suffix); Undo.RecordObject(rect,"Align skill"); rect.anchoredPosition = new Vector2(x,-34);
        }
        foreach (var suffix in new[] { " binding", " state" })
        {
            var label = parent.Find(name + suffix).GetComponent<TextMeshProUGUI>();
            Undo.RecordObject(label,"Center skill label"); Undo.RecordObject(label.rectTransform,"Center skill label");
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.anchoredPosition = new Vector2(x-15,suffix == " binding" ? -8 : -133);
            label.rectTransform.sizeDelta = new Vector2(130,22);
        }
    }

    private static Sprite Import(string path, bool sliced)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.InvalidOperationException("Missing HUD art: " + path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
        if (sliced) importer.spriteBorder = new Vector4(12,12,12,12);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go, "Create HUD");
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        go.layer = 5; return rect;
    }
    private static Image Picture(string name, Transform parent, Vector2 position, Vector2 size, Color color, Sprite sprite = null)
    {
        var rect = Rect(name,parent,new Vector2(0,1),position,size);
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.sprite = sprite; image.raycastTarget = false; return image;
    }
    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = Rect(name,parent,anchor,position,size);
        var bg = rect.gameObject.AddComponent<Image>(); bg.color = new Color(.035f,.045f,.055f,.94f); bg.raycastTarget = false;
        var frame = Picture("Fantasy frame",rect,Vector2.zero,size,Gold,border); frame.type = Image.Type.Sliced;
        return rect;
    }
    private static TextMeshProUGUI Label(string name, Transform parent, string value, float size, Vector2 position, Vector2 dimensions, Color color)
    {
        var text = Rect(name,parent,new Vector2(0,1),position,dimensions).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSize = size; text.text = value; text.color = color; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal; return text;
    }
    private static void Bind(string field, Object value) { ui.FindProperty(field).objectReferenceValue = value; }
    private static void Bar(Transform parent, string name, Vector2 pos, Vector2 size, Color color, string field)
    {
        Picture(name + " track",parent,pos,size,new Color(.17f,.13f,.13f));
        var fill = Picture(name + " fill",parent,pos + new Vector2(3,-3),size-new Vector2(6,6),color);
        fill.fillAmount = field == "healthFill" ? 1 : 0;
        fill.rectTransform.localScale = new Vector3(fill.fillAmount, 1, 1);
        Bind(field,fill);
    }
    private static void Skill(Transform parent, string name, string key, Sprite sprite, float x, string iconField, string fillField, string textField)
    {
        var pos = new Vector2(x,-34); var size = new Vector2(100,100);
        Bind(iconField, Picture(name + " icon",parent,pos,size,Color.white,sprite));
        var overlay = Picture(name + " cooldown",parent,pos,size,new Color(0,0,0,.8f),sprite);
        overlay.type = Image.Type.Filled; overlay.fillMethod = Image.FillMethod.Radial360; overlay.fillAmount = 0; Bind(fillField,overlay);
        var frame = Picture(name + " border",parent,pos,size,Gold,border); frame.type = Image.Type.Sliced;
        Label(name + " binding",parent,key + "  /  " + name.ToUpperInvariant(),13,new Vector2(x-7,-10),new Vector2(130,22),Gold);
        Bind(textField,Label(name + " state",parent,"READY",13,new Vector2(x,-133),new Vector2(130,22),Color.white));
    }
}
