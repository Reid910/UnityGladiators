using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Extends the installed HUD in place. Never replaces the existing GameUI references.
public static class ArenaUIPassBuilder
{
    private static readonly Color Ink = new Color(.045f,.048f,.055f,.96f);
    private static readonly Color Gold = new Color(.72f,.57f,.34f);
    private static readonly Color Cream = new Color(.94f,.9f,.8f);
    private static readonly Color Crimson = new Color(.43f,.08f,.085f);
    private static TMP_FontAsset font;
    private static Sprite border;

    [MenuItem("Gladiators/Install Full Arena UI")]
    public static void Install()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Install the UI in Edit mode.");
        var ui = Object.FindAnyObjectByType<GameUI>();
        if (ui == null) throw new System.InvalidOperationException("Open SampleScene with its existing GameUI Canvas.");
        if (ui.GetComponent<ArenaMenuController>() != null) { InstallShardDisplay(); InstallEnemyHealthDisplay(); InstallControlArtwork(); return; }
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Install full arena UI");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        border = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AssetPacks/kenney_fantasy-ui-borders/PNG/Default/Border/panel-border-009.png");
        var root = ui.transform.Find("Combat HUD");
        if (root == null) throw new System.InvalidOperationException("Install the existing Combat HUD first.");
        var serialized = new SerializedObject(ui);
        var combat = (PlayerCombat)serialized.FindProperty("playerCombat").objectReferenceValue;
        var menu = Undo.AddComponent<ArenaMenuController>(ui.gameObject);
        menu.gameUI = ui;
        menu.combatHUD = root.gameObject;
        menu.orbitCamera = Object.FindAnyObjectByType<ThirdPersonCamera>();
        serialized.FindProperty("menus").objectReferenceValue = menu;
        Undo.AddComponent<HUDSafeArea>(root.gameObject);
        var scaler = ui.GetComponent<CanvasScaler>();
        Undo.RecordObject(scaler,"Responsive arena canvas");
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            Undo.RecordObject(text,"Refresh HUD typography");
            if (text.color == Color.white) text.color = Cream;
            text.raycastTarget = false;
        }
        foreach (var image in root.GetComponentsInChildren<Image>(true))
        {
            Undo.RecordObject(image,"Refresh HUD palette");
            if (image.name == "Fantasy frame" || image.name.EndsWith(" border")) image.color = Gold;
            else if (image.sprite == null && image.color.a > .8f && image.name != "Health fill" && image.name != "Stagger fill") image.color = Ink;
        }
        root.Find("Vitals/Title").GetComponent<TextMeshProUGUI>().text = "VITALS  /  GLADIATOR";
        foreach (var name in new[] { "Arena status/Wave", "Arena status/Enemies" })
            root.Find(name).GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        var healthFill = (Image)serialized.FindProperty("healthFill").objectReferenceValue;
        healthFill.color = new Color(.68f,.12f,.14f);
        Accent(root.Find("Vitals"),420);
        Accent(root.Find("Arena status"),280);
        var skills = (RectTransform)root.Find("Skills");
        Undo.RecordObject(skills,"Expand controls"); skills.sizeDelta = new Vector2(700,184);
        var skillFrame = (RectTransform)skills.Find("Fantasy frame"); Undo.RecordObject(skillFrame,"Expand controls frame"); skillFrame.sizeDelta = skills.sizeDelta;
        var combo = (RectTransform)skills.Find("Combo"); Undo.RecordObject(combo,"Center combo"); combo.sizeDelta = new Vector2(670,24);
        var heavyOverlay = skills.Find("Heavy attack cooldown").GetComponent<Image>();
        Undo.RecordObject(heavyOverlay,"Keep heavy icon static"); heavyOverlay.fillAmount = 0f;
        var defend = Panel("Deflect",skills,new Vector2(0,1),new Vector2(548,-34),new Vector2(124,100));
        Text("Key",defend,"SPACE",18,new Vector2(8,-10),new Vector2(108,28),Gold);
        Text("Action",defend,"DEFLECT\n/ BLOCK",17,new Vector2(8,-40),new Vector2(108,50),Cream);
        Text("Deflect hint",skills,"TAP / HOLD",13,new Vector2(538,-137),new Vector2(144,22),Gold);
        Text("Pause hint",root,"ESC  /  PAUSE",17,new Vector2(-240,26),new Vector2(210,30),Gold,new Vector2(1,0));

        var ultimate = Panel("Ultimate",root,new Vector2(.5f,0),new Vector2(0,225),new Vector2(700,62));
        var track = Box("Track",ultimate,new Vector2(14,-14),new Vector2(672,34),new Color(.16f,.12f,.075f));
        var fill = Box("Fill",track.transform,Vector2.zero,new Vector2(672,34),Gold);
        fill.rectTransform.localScale = new Vector3(0,1,1);
        var ultimateLabel = Text("Charge",ultimate,"ULTIMATE  /  0%",18,new Vector2(14,-14),new Vector2(672,34),Cream);
        serialized.FindProperty("ultimateFill").objectReferenceValue = fill;
        serialized.FindProperty("ultimateText").objectReferenceValue = ultimateLabel;
        serialized.FindProperty("ultimateReadyFlash").objectReferenceValue = Undo.AddComponent<HUDFlash>(ultimate.Find("Frame").gameObject);
        var equipMessage = (RectTransform)root.Find("Equip message"); Undo.RecordObject(equipMessage,"Place equip feedback"); equipMessage.anchoredPosition = new Vector2(0,306);

        var enemyRoot = Rect("Enemy stagger bars",ui.transform,Vector2.zero,Vector2.zero,Vector2.zero);
        Stretch(enemyRoot); enemyRoot.pivot = new Vector2(.5f,.5f); enemyRoot.SetAsFirstSibling();
        var enemyHUD = Undo.AddComponent<EnemyStaggerHUD>(enemyRoot.gameObject);
        enemyHUD.worldCamera = Camera.main; enemyHUD.player = combat.transform; enemyHUD.menus = menu;
        var template = Rect("Template",enemyRoot,new Vector2(.5f,.5f),Vector2.zero,new Vector2(180,44));
        Box("Backing",template,Vector2.zero,new Vector2(180,44),new Color(.025f,.025f,.03f,.88f));
        Text("Status",template,"STAGGER",13,new Vector2(0,-1),new Vector2(180,24),Cream);
        var enemyTrack = Box("Track",template,new Vector2(6,-28),new Vector2(168,8),new Color(.19f,.16f,.12f));
        Box("Fill",enemyTrack.transform,Vector2.zero,new Vector2(168,8),Gold);
        template.gameObject.SetActive(false);
        enemyHUD.plateTemplate = template;

        BuildMenus(ui.transform,menu);
        RestyleResult((GameObject)serialized.FindProperty("victoryPanel").objectReferenceValue,"VICTORY","THE ARENA IS YOURS",menu);
        RestyleResult((GameObject)serialized.FindProperty("gameOverPanel").objectReferenceValue,"FALLEN","EVERY DEFEAT FORGES A FIGHTER",menu);
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(menu); EditorUtility.SetDirty(enemyHUD);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        Selection.activeGameObject = menu.mainMenu;
        InstallShardDisplay();
        InstallEnemyHealthDisplay();
        InstallControlArtwork();
    }

    [MenuItem("Gladiators/Install Control Artwork")]
    public static void InstallControlArtwork()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Install control artwork in Edit mode.");
        var ui = Object.FindAnyObjectByType<GameUI>();
        if (ui == null) return;
        var root = ui.transform.Find("Combat HUD");
        if (root == null) return;
        border = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AssetPacks/kenney_fantasy-ui-borders/PNG/Default/Border/panel-border-009.png");
        var defend = root.Find("Skills/Deflect");
        if (defend != null && defend.Find("Block icon") == null)
        {
            const string iconPath = "Assets/AssetPacks/Skill Icon Pack Wenrexa 4.0/256x256px/Normal/38 Icon.png";
            var importer = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            var icon = Box("Block icon",defend,new Vector2(12,0),new Vector2(100,100),Color.white);
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            icon.preserveAspect = true;
            icon.transform.SetAsFirstSibling();
            var action = defend.Find("Action");
            Undo.RecordObject(action.gameObject,"Hide legacy block text"); action.gameObject.SetActive(false);
            var key = defend.Find("Key").GetComponent<TextMeshProUGUI>();
            Undo.RecordObject(key,"Label block artwork"); Undo.RecordObject(key.rectTransform,"Align block binding");
            key.text = "SPACE / BLOCK"; key.fontSize = 14;
            key.rectTransform.anchoredPosition = new Vector2(-10,26);
            key.rectTransform.sizeDelta = new Vector2(144,24);
        }
        var pause = root.Find("Pause hint");
        if (pause != null && root.Find("Pause hint backing") == null)
        {
            var backing = Panel("Pause hint backing",root,new Vector2(1,0),new Vector2(-226,18),new Vector2(238,46));
            backing.SetSiblingIndex(pause.GetSiblingIndex());
        }
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
    }

    [MenuItem("Gladiators/Install Enemy Health Display")]
    public static void InstallEnemyHealthDisplay()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Install enemy health UI in Edit mode.");
        var hud = Object.FindAnyObjectByType<EnemyStaggerHUD>();
        if (hud == null || hud.plateTemplate.Find("Health value") != null) return;
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        var template = hud.plateTemplate;
        Undo.RecordObject(template,"Expand enemy vitals");
        template.sizeDelta = new Vector2(180,76);
        template.pivot = new Vector2(.5f,0f);
        var backing = (RectTransform)template.Find("Backing"); Undo.RecordObject(backing,"Expand nameplate backing"); backing.sizeDelta = template.sizeDelta;
        var status = (RectTransform)template.Find("Status"); Undo.RecordObject(status,"Move stagger text"); status.anchoredPosition = new Vector2(0,-40);
        var track = (RectTransform)template.Find("Track"); Undo.RecordObject(track,"Move stagger bar"); track.anchoredPosition = new Vector2(6,-64);
        Text("Health value",template,"100 / 100",16,new Vector2(0,-1),new Vector2(180,24),Cream);
        var healthTrack = Box("Health track",template,new Vector2(6,-27),new Vector2(168,10),new Color(.19f,.065f,.065f));
        Box("Fill",healthTrack.transform,Vector2.zero,new Vector2(168,10),new Color(.8f,.16f,.17f));
        EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
    }

    [MenuItem("Gladiators/Install Shard Display")]
    public static void InstallShardDisplay()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Install the shard display in Edit mode.");
        var ui = Object.FindAnyObjectByType<GameUI>();
        if (ui == null) return;
        var root = ui.transform.Find("Combat HUD");
        if (root == null) return;
        if (root.Find("Stat shard") != null) { InstallGlovesDisplay(); return; }
        var gear = root.Find("Gear slots").GetComponent<EquipmentHUD>();
        Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Add stat shard HUD");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        border = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AssetPacks/kenney_fantasy-ui-borders/PNG/Default/Border/panel-border-009.png");
        var card = Panel("Stat shard",root,new Vector2(1,1),new Vector2(-28,-162),new Vector2(580,170));
        Text("Slot",card,"STAT SHARD",15,new Vector2(20,-10),new Vector2(540,25),Gold).alignment = TextAlignmentOptions.Left;
        var gem = Box("Shard emblem",card,new Vector2(38,-68),new Vector2(28,42),Gold);
        gem.rectTransform.localRotation = Quaternion.Euler(0,0,35);
        var name = Text("Item",card,"Empty",21,new Vector2(85,-49),new Vector2(182,58),Cream);
        name.alignment = TextAlignmentOptions.Left; name.enableAutoSizing = true; name.fontSizeMin = 15; name.fontSizeMax = 21;
        var rarity = Text("Rarity",card,"-",13,new Vector2(85,-118),new Vector2(182,25),Gold); rarity.alignment = TextAlignmentOptions.Left;
        Box("Divider",card,new Vector2(281,-44),new Vector2(1,103),Gold);
        var bonuses = Text("Bonuses",card,"Equip a stat shard\nto gain bonuses.",17,new Vector2(300,-46),new Vector2(260,102),Cream);
        bonuses.alignment = TextAlignmentOptions.MidlineLeft; bonuses.enableAutoSizing = true; bonuses.fontSizeMin = 12; bonuses.fontSizeMax = 17;
        var frame = card.Find("Frame").GetComponent<Image>();
        var flash = Undo.AddComponent<HUDFlash>(frame.gameObject);
        Undo.RecordObject(gear,"Bind stat shard HUD");
        var views = new System.Collections.Generic.List<EquipmentHUD.SlotView>(gear.slots);
        views.Add(new EquipmentHUD.SlotView { slot = ItemSlot.StatShard, frame = frame, flash = flash, itemName = name, rarity = rarity });
        gear.slots = views.ToArray(); gear.shardBonuses = bonuses;
        var comparison = (RectTransform)root.Find("Swap comparison");
        Undo.RecordObject(comparison,"Position shard comparison"); comparison.anchoredPosition = new Vector2(-28,-348);
        EditorUtility.SetDirty(gear); EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        InstallGlovesDisplay();
    }

    [MenuItem("Gladiators/Install Gloves Display")]
    public static void InstallGlovesDisplay()
    {
        if (Application.isPlaying) throw new System.InvalidOperationException("Install the gloves display in Edit mode.");
        var ui = Object.FindAnyObjectByType<GameUI>();
        if (ui == null) return;
        var row = ui.transform.Find("Combat HUD/Gear slots");
        if (row == null || row.Find("GLOVES") != null) return;
        var gear = row.GetComponent<EquipmentHUD>();
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        border = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AssetPacks/kenney_fantasy-ui-borders/PNG/Default/Border/panel-border-009.png");
        // Fit six slots in the existing width so the wave panel stays clear.
        int index = 0;
        foreach (var view in gear.slots)
        {
            if (view.slot == ItemSlot.StatShard) continue;
            var slot = (RectTransform)view.frame.transform.parent;
            Undo.RecordObject(slot,"Fit six gear slots");
            slot.anchoredPosition = new Vector2(index++ * 98,0); slot.sizeDelta = new Vector2(90,118);
            Undo.RecordObject(view.frame.rectTransform,"Resize gear frame"); view.frame.rectTransform.sizeDelta = slot.sizeDelta;
            foreach (var label in slot.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Undo.RecordObject(label.rectTransform,"Fit gear labels");
                label.rectTransform.anchoredPosition = new Vector2(5,label.rectTransform.anchoredPosition.y);
                label.rectTransform.sizeDelta = new Vector2(80,label.rectTransform.sizeDelta.y);
            }
        }
        var gloves = Panel("GLOVES",row,new Vector2(0,1),new Vector2(490,0),new Vector2(90,118));
        Text("Slot",gloves,"GLOVES",14,new Vector2(5,-10),new Vector2(80,22),Gold);
        var name = Text("Item",gloves,"Empty",17,new Vector2(5,-35),new Vector2(80,48),Color.gray);
        name.enableAutoSizing = true; name.fontSizeMin = 12; name.fontSizeMax = 17;
        var rarity = Text("Rarity",gloves,"-",11,new Vector2(5,-89),new Vector2(80,20),Color.gray);
        var frame = gloves.Find("Frame").GetComponent<Image>(); frame.color = Color.gray;
        var flash = Undo.AddComponent<HUDFlash>(frame.gameObject);
        Undo.RecordObject(gear,"Bind gloves display");
        var views = new System.Collections.Generic.List<EquipmentHUD.SlotView>(gear.slots);
        views.Add(new EquipmentHUD.SlotView {slot = ItemSlot.Gloves, frame = frame, flash = flash, itemName = name, rarity = rarity});
        gear.slots = views.ToArray();
        EditorUtility.SetDirty(gear); EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
    }

    private static void BuildMenus(Transform canvas, ArenaMenuController menu)
    {
        var main = Fullscreen("Arena main menu",canvas,new Color(.025f,.029f,.036f,.98f));
        menu.mainMenu = main.gameObject;
        var content = Rect("Menu composition",main,new Vector2(.5f,.5f),Vector2.zero,new Vector2(1340,740));
        Box("Crimson banner",content,new Vector2(0,0),new Vector2(8,740),Crimson);
        var crest = Box("Arena crest",content,new Vector2(72,-65),new Vector2(42,42),Gold);
        crest.rectTransform.localRotation = Quaternion.Euler(0,0,45);
        var eyebrow = Text("Eyebrow",content,"THE ARENA AWAITS",18,new Vector2(70,-142),new Vector2(650,35),Gold);
        eyebrow.characterSpacing = 6; eyebrow.alignment = TextAlignmentOptions.Left;
        var title = Text("Title",content,"GLADIATORS",76,new Vector2(65,-187),new Vector2(760,110),Cream); title.alignment = TextAlignmentOptions.Left; title.characterSpacing = 5;
        Text("Subtitle",content,"BREAK THEIR GUARD.\nCLAIM YOUR GLORY.",26,new Vector2(70,-310),new Vector2(670,95),Cream).alignment = TextAlignmentOptions.Left;
        menu.playButton = Button("Play",content,"ENTER THE ARENA",new Vector2(70,-455),new Vector2(530,68),menu.Play);
        Button("Quit",content,"QUIT GAME",new Vector2(70,-540),new Vector2(530,58),menu.Quit);
        Text("Footer",content,"WAVE SURVIVAL  /  UNITY GLADIATORS",14,new Vector2(70,-675),new Vector2(650,35),Gold).alignment = TextAlignmentOptions.Left;
        var guide = Panel("Field manual",content,new Vector2(0,1),new Vector2(870,-155),new Vector2(430,490));
        Text("Manual title",guide,"THE GLADIATOR'S CODE",22,new Vector2(25,-22),new Vector2(380,45),Gold);
        Text("Manual",guide,"WASD   Move     SHIFT   Sprint\n\nLMB   Strike     RMB   Heavy\nQ   Ability     CTRL   Dash\n\nSPACE   Tap to deflect / hold to block\nC   Ultimate when charged\nE   Swap nearby gear\n\nBuild enemy stagger.\nWhen BROKEN, strike to finish.",19,new Vector2(30,-92),new Vector2(370,370),Cream).alignment = TextAlignmentOptions.TopLeft;
        main.gameObject.SetActive(false);

        var pause = Fullscreen("Arena pause menu",canvas,new Color(.015f,.018f,.025f,.88f));
        menu.pauseMenu = pause.gameObject;
        var card = Panel("Pause card",pause,new Vector2(.5f,.5f),Vector2.zero,new Vector2(620,590));
        Text("Eyebrow",card,"A MOMENT OF RESPITE",16,new Vector2(30,-34),new Vector2(560,32),Gold);
        Text("Title",card,"PAUSED",60,new Vector2(30,-87),new Vector2(560,84),Cream);
        Text("Hint",card,"Your fight will wait.",20,new Vector2(30,-181),new Vector2(560,36),Cream);
        menu.resumeButton = Button("Resume",card,"RESUME",new Vector2(70,-267),new Vector2(480,62),menu.Resume);
        Button("Main menu",card,"LEAVE RUN / MAIN MENU",new Vector2(70,-346),new Vector2(480,58),menu.ReturnToMenu);
        Button("Quit",card,"QUIT GAME",new Vector2(70,-421),new Vector2(480,58),menu.Quit);
        Text("Escape",card,"ESC / START TO RESUME",14,new Vector2(50,-522),new Vector2(520,32),Gold);
        pause.gameObject.SetActive(false);
    }

    private static void RestyleResult(GameObject panel, string title, string subtitle, ArenaMenuController menu)
    {
        // Preserve legacy children and their listeners for inspection/recovery.
        foreach (Transform child in panel.transform) { Undo.RecordObject(child.gameObject,"Hide legacy result art"); child.gameObject.SetActive(false); }
        var rect = (RectTransform)panel.transform; Undo.RecordObject(rect,"Result layout"); Stretch(rect);
        var background = panel.GetComponent<Image>();
        if (background == null) background = Undo.AddComponent<Image>(panel);
        Undo.RecordObject(background,"Result backdrop"); background.color = new Color(.025f,.023f,.027f,.94f); background.raycastTarget = true;
        var card = Panel("Arena result",panel.transform,new Vector2(.5f,.5f),Vector2.zero,new Vector2(710,510));
        Text("Title",card,title,64,new Vector2(35,-55),new Vector2(640,92),Cream);
        Text("Subtitle",card,subtitle,18,new Vector2(35,-160),new Vector2(640,40),Gold);
        Button("Restart",card,"FIGHT AGAIN",new Vector2(100,-256),new Vector2(510,62),menu.RestartRun);
        Button("Main menu",card,"RETURN TO MAIN MENU",new Vector2(100,-340),new Vector2(510,58),menu.ReturnToMenu);
        panel.SetActive(false);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name,typeof(RectTransform)); Undo.RegisterCreatedObjectUndo(go,"Create arena UI");
        go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name,parent,new Vector2(0,1),position,size).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }
    private static RectTransform Fullscreen(string name, Transform parent, Color color)
    {
        var image = Box(name,parent,Vector2.zero,Vector2.zero,color); Stretch(image.rectTransform); image.raycastTarget = true; return image.rectTransform;
    }
    private static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = Rect(name,parent,anchor,position,size);
        var bg = rect.gameObject.AddComponent<Image>(); bg.color = Ink; bg.raycastTarget = false;
        var frame = Box("Frame",rect,Vector2.zero,size,Gold); frame.sprite = border; frame.type = Image.Type.Sliced;
        return rect;
    }
    private static void Accent(Transform parent,float width) { Box("Bronze rule",parent,new Vector2(16,-2),new Vector2(width-32,2),Gold); }
    private static TextMeshProUGUI Text(string name,Transform parent,string value,float size,Vector2 position,Vector2 dimensions,Color color,Vector2? anchor = null)
    {
        var text = Rect(name,parent,anchor ?? new Vector2(0,1),position,dimensions).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        return text;
    }
    private static Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size,UnityAction callback)
    {
        var image = Box(name,parent,position,size,Crimson); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.45f,1.3f,1.12f); colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.7f,.7f,.7f); colors.fadeDuration = .12f; button.colors = colors;
        Text("Label",image.transform,text,20,Vector2.zero,size,Cream);
        UnityEventTools.AddPersistentListener(button.onClick,callback);
        return button;
    }
}
