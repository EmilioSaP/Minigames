using System.Collections.Generic;
using UnityEngine;

public class InteractionDetector : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private GameObject playerView;
    [SerializeField] private float maxInteractionDistance = 4f;

    private readonly List<Interactable> nearbyInteractables = new();

    public Interactable CurrentTarget { get; private set; }

    private void Update()
    {
        FindCurrentTarget();
    }

    private void FindCurrentTarget()
    {
        CurrentTarget = null;

        if (playerView == null)
            return;

        Ray ray = new Ray(
            playerView.transform.position,
            playerView.transform.forward
        );
        RaycastHit[] hits = Physics.RaycastAll(ray, maxInteractionDistance);
        RaycastHit hit = default;
        bool foundHit = false;

        foreach (RaycastHit candidate in hits)
        {
            Transform hitTransform = candidate.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
                continue;

            if (!foundHit || candidate.distance < hit.distance)
            {
                hit = candidate;
                foundHit = true;
            }
        }

        if (!foundHit)
            return;
        

        Interactable interactable =
            hit.collider.GetComponentInParent<Interactable>();

        if (interactable == null)
            return;
        
        if (!nearbyInteractables.Contains(interactable))
            return;

        CurrentTarget = interactable;
        Debug.Log($"Current Target: {CurrentTarget.gameObject.name}");
    }

    private void OnTriggerEnter(Collider other)
    {
        Interactable interactable =
            other.GetComponentInParent<Interactable>();

        if (interactable == null)
            return;

        if (!nearbyInteractables.Contains(interactable))
            nearbyInteractables.Add(interactable);
    }

    private void OnTriggerExit(Collider other)
    {
        Interactable interactable =
            other.GetComponentInParent<Interactable>();

        if (interactable == null)
            return;

        nearbyInteractables.Remove(interactable);

        if (CurrentTarget == interactable)
            CurrentTarget = null;
    }
}