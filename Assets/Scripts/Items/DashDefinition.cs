using UnityEngine;

// Granted by whichever Boots item is equipped (Risk of Rain shift-style) — see
// PlayerCombat.TryDash(), which currently uses a fixed placeholder instead of
// reading this from the equipped boots (wired in M4). No boots equipped means
// no dash at all.
[CreateAssetMenu(fileName = "NewDash", menuName = "UnityGladiators/Dash")]
public class DashDefinition : ScriptableObject
{
    [SerializeField] private string dashName;
    [SerializeField] private float distance = 4f;
    [SerializeField] private float cooldown = 1.5f;
    [SerializeField] private bool dealsDamage;
    [SerializeField] private int damage;

    public string DashName => dashName;
    public float Distance => distance;
    public float Cooldown => cooldown;
    public bool DealsDamage => dealsDamage;
    public int Damage => damage;
}
