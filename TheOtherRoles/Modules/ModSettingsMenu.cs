using UnityEngine;

namespace TheOtherRoles.Modules;

public class ModSettingsMenu : MonoBehaviour
{
    public static PassiveButton GenericButton;
    public static PassiveButton ImpostorButton;
    public static PassiveButton NeutralButton;
    public static PassiveButton CrewmateButton;
    public static PassiveButton ModifierButton;
    public static PassiveButton MatchTagButton;

    public static Scroller ScrollBar;
    public static UiElement BackButton;
    public static UiElement DefaultButtonSelected = null;

    public static CategoryHeaderMasked CategoryHeader;
    public static CategoryHeaderEditRole CategoryHeaderEditRoleOrigin;
    public static RoleOptionSetting RoleOptionSettingOrigin;
    public static ToggleOption CheckboxOrigin;
    public static StringOption StringOptionOrigin;

    public static void Start()
    {
        GameObject.Find("RolesTabs")?.Destroy();

        #region タブ変更ボタン
        GameObject header = new("HeaderButtons");
        header.transform.localPosition = Vector3.zero;

        GameObject instance = new("Instance");
        instance.transform.SetParent(header.transform);
        instance.transform.localPosition = new(-2.8f, 2.3f, -2f);
        instance.layer = 5;
        instance.gameObject.SetActive(true);
        SpriteRenderer instance_renderer = instance.AddComponent<SpriteRenderer>();
        instance_renderer.drawMode = SpriteDrawMode.Sliced;
        instance_renderer.size = Vector2.one * 0.75f;
        instance_renderer.color = Color.gray;
        BoxCollider2D instance_collider = instance.AddComponent<BoxCollider2D>();
        instance_collider.offset = Vector2.zero;
        instance_collider.size = Vector2.one * 0.75f;
        PassiveButton instance_button = instance.AddComponent<PassiveButton>();
        instance_button.Colliders = new Collider2D[] { instance_collider };
        instance_button.OnMouseOut = new();
        instance_button.OnMouseOver = new();

        GameObject generic = Instantiate(instance, header.transform);
        generic.name = "GenericButton";
        SpriteRenderer generic_renderer = generic.GetComponent<SpriteRenderer>();
        generic_renderer.sprite = Helpers.loadSpriteFromResources("Setting_Custom.png", 100f);
        generic_renderer.size = Vector2.one * 0.75f;
        GenericButton = generic.GetComponent<PassiveButton>();
        generic_renderer.color = Color.white;

        GameObject impostor = Instantiate(instance, header.transform);
        impostor.name = "ImpostorButton";
        impostor.transform.localPosition += new Vector3(0.75f, 0f);
        SpriteRenderer impostor_renderer = impostor.GetComponent<SpriteRenderer>();
        impostor_renderer.sprite = Helpers.loadSpriteFromResources("Setting_Impostor.png", 100f);
        impostor_renderer.size = Vector2.one * 0.75f;
        ImpostorButton = impostor.GetComponent<PassiveButton>();
        //ImpostorButton.OnMouseOver.AddListener(() => impostor_renderer.color = Color.white);
        
        GameObject neutral = Instantiate(instance, header.transform);
        neutral.name = "NeutralButton";
        neutral.transform.localPosition += new Vector3(0.75f, 0f) * 2;
        SpriteRenderer neutral_renderer = neutral.GetComponent<SpriteRenderer>();
        neutral_renderer.sprite = Helpers.loadSpriteFromResources("Setting_Neutral.png", 100f);
        neutral_renderer.size = Vector2.one * 0.75f;
        NeutralButton = neutral.GetComponent<PassiveButton>();
        //NeutralButton.OnMouseOver.AddListener(() => neutral_renderer.color = Color.white);

        GameObject crewmate = Instantiate(instance, header.transform);
        crewmate.name = "CrewmateButton";
        crewmate.transform.localPosition += new Vector3(0.75f, 0f) * 3;
        SpriteRenderer crewmate_renderer = crewmate.GetComponent<SpriteRenderer>();
        crewmate_renderer.sprite = Helpers.loadSpriteFromResources("Setting_Crewmate.png", 100f);
        crewmate_renderer.size = Vector2.one * 0.75f;
        CrewmateButton = crewmate.GetComponent<PassiveButton>();
        //CrewmateButton.OnMouseOver.AddListener(() => crewmate_renderer.color = Color.white);

        GameObject modifier = Instantiate(instance, header.transform);
        modifier.name = "ModifierButton";
        modifier.transform.localPosition += new Vector3(0.75f, 0f) * 4;
        SpriteRenderer modifier_renderer = modifier.GetComponent<SpriteRenderer>();
        modifier_renderer.sprite = Helpers.loadSpriteFromResources("Setting_Modifier.png", 100f);
        modifier_renderer.size = Vector2.one * 0.75f;
        ModifierButton = modifier.GetComponent<PassiveButton>();
        //ModifierButton.OnMouseOver.AddListener(() => modifier_renderer.color = Color.white);

        GameObject match_tag = Instantiate(instance, header.transform);
        match_tag.name = "MatchTagButton";
        match_tag.transform.localPosition += new Vector3(0.75f, 0f) * 5;
        SpriteRenderer match_tag_renderer = match_tag.GetComponent<SpriteRenderer>();
        match_tag_renderer.sprite = Helpers.loadSpriteFromResources("TabIcon.png", 100f);
        match_tag_renderer.size = Vector2.one * 0.75f;
        MatchTagButton = match_tag.GetComponent<PassiveButton>();
        //MatchTagButton.OnMouseOver.AddListener(() => match_tag_renderer.color = Color.white);

        Destroy(instance);
        #endregion

        foreach (PassiveButton button in ScrollBar.Inner.GetComponentsInChildren<PassiveButton>(true))
            button.ClickMask = ScrollBar.Hitbox;
    }
}