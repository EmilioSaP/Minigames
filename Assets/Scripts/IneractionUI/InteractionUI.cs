using TMPro;
using UnityEngine;

public class InteractionUI : MonoBehaviour
{

    [SerializeField] private TMP_Text pickupPrompt;

    public static InteractionUI Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    public void ShowPickupPrompt(GrabbableObject grabbable)
    {
        if (grabbable == null)
        {
            HidePrompt();
            return;
        }

        if (grabbable.PickupTime > 0f)
        {
            pickupPrompt.text =
                $"Hold Click {grabbable.gameObject.name} to pick it up";
        }
        else
        {
            pickupPrompt.text =
                $"Click on {grabbable.gameObject.name} to pick it up";
        }

        pickupPrompt.gameObject.SetActive(true);
    }

    public void HidePrompt()
    {
        pickupPrompt.gameObject.SetActive(false);
    }
}