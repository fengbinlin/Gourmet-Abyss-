using UnityEngine;

public class Table : MonoBehaviour
{
    private FacilityUnlockable _facilityUnlock;

    public bool IsFacilityUnlocked => _facilityUnlock == null || _facilityUnlock.IsUnlocked;

    private void Awake()
    {
        _facilityUnlock = GetComponent<FacilityUnlockable>();
    }
}
