using UnityEngine;

public class Hitstun : MonoBehaviour
{
    private float stunnedUntilTime;

    public bool IsStunned => Time.time < stunnedUntilTime;

    public void ApplyStun(float duration)
    {
        float newStunnedUntilTime = Time.time + duration;

        if (newStunnedUntilTime > stunnedUntilTime)
        {
            stunnedUntilTime = newStunnedUntilTime;
        }
    }
}
