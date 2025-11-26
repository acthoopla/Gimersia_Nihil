using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class AnimationEventData
{
    [Header("Event Settings")]
    public string eventName;

    [Header("Event Actions")]
    public UnityEvent onEventTriggered;
}

public class AnimationEvent : MonoBehaviour
{
    [Header("Animation Events")]
    [SerializeField] private List<AnimationEventData> animationEvents = new List<AnimationEventData>();

    private Dictionary<string, AnimationEventData> eventDictionary;

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            InitializeEventDictionary();
        }
    }

    private void Awake()
    {
        InitializeEventDictionary();
    }

    private void InitializeEventDictionary()
    {
        eventDictionary = new Dictionary<string, AnimationEventData>();

        foreach (var animEvent in animationEvents)
        {
            if (!string.IsNullOrEmpty(animEvent.eventName))
            {
                if (!eventDictionary.ContainsKey(animEvent.eventName))
                {
                    eventDictionary.Add(animEvent.eventName, animEvent);
                }
            }
        }
    }


    public void TriggerEvent(string eventName)
    {
        if (eventDictionary != null && eventDictionary.ContainsKey(eventName))
        {
            var eventData = eventDictionary[eventName];

            eventData.onEventTriggered?.Invoke();
        }
    }

    public void TriggerEventFromScript(string eventName)
    {
        TriggerEvent(eventName);
    }

    public void TriggerMultipleEvents(string[] eventNames)
    {
        foreach (string eventName in eventNames)
        {
            TriggerEvent(eventName);
        }
    }

    public void TriggerMultipleEventsWithDelay(string[] eventNames, float delay)
    {
        StartCoroutine(TriggerEventsCoroutine(eventNames, delay));
    }

    private System.Collections.IEnumerator TriggerEventsCoroutine(string[] eventNames, float delay)
    {
        foreach (string eventName in eventNames)
        {
            TriggerEvent(eventName);
            yield return new WaitForSeconds(delay);
        }
    }

    public bool HasEvent(string eventName)
    {
        return eventDictionary != null && eventDictionary.ContainsKey(eventName);
    }

    public List<string> GetAllEventNames()
    {
        List<string> eventNames = new List<string>();
        foreach (var animEvent in animationEvents)
        {
            if (!string.IsNullOrEmpty(animEvent.eventName))
            {
                eventNames.Add(animEvent.eventName);
            }
        }
        return eventNames;
    }

    public void AddRuntimeEvent(string eventName, UnityAction action)
    {
        var newEvent = new AnimationEventData();
        newEvent.eventName = eventName;
        newEvent.onEventTriggered = new UnityEvent();
        newEvent.onEventTriggered.AddListener(action);

        animationEvents.Add(newEvent);

        if (eventDictionary == null)
            eventDictionary = new Dictionary<string, AnimationEventData>();

        if (!eventDictionary.ContainsKey(eventName))
        {
            eventDictionary.Add(eventName, newEvent);
        }
    }

    public void RemoveRuntimeEvent(string eventName)
    {
        if (eventDictionary != null && eventDictionary.ContainsKey(eventName))
        {
            var eventToRemove = eventDictionary[eventName];
            animationEvents.Remove(eventToRemove);
            eventDictionary.Remove(eventName);
        }
    }
}
