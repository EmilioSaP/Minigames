using UnityEngine;

public class Interactable : MonoBehaviour
{
    public enum InteractionType
    {
        Grabbable,
        Kickable,
        Vehicle,
        Switch,
        Equipable
    }

    [SerializeField] private InteractionType interactionType;

    public InteractionType Type => interactionType;
}