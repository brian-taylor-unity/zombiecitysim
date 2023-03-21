using UnityEngine;
using UnityEngine.EventSystems;

public class MouseInputUIBlocker : MonoBehaviour
{
    public bool blockedByUI;
    
    private EventTrigger _eventTrigger;

    private void Start()
    {
        _eventTrigger = GetComponent<EventTrigger>();
        if (_eventTrigger == null)
            return;

        // Pointer Enter
        var enterUIEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterUIEntry.callback.AddListener(_ => { EnterUI(); });
        _eventTrigger.triggers.Add(enterUIEntry);

        //Pointer Exit
        var exitUIEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitUIEntry.callback.AddListener(_ => { ExitUI(); });
        _eventTrigger.triggers.Add(exitUIEntry);
    }

    private void EnterUI()
    {
        blockedByUI = true;
    }

    private void ExitUI()
    {
        blockedByUI = false;
    }
}
