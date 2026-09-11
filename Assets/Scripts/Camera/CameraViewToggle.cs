using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraViewToggle : NetworkBehaviour
{
    public enum CameraView
    {
        FirstPerson,
        ThirdPerson,
        FarThirdPerson
    }

    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private GameObject playerVisual;
    [SerializeField] private GameObject playerVisualHat;
    [SerializeField] private GameObject playerNameTag;

    [Header("Third Person Offsets")]
    [SerializeField] private List<Vector3> offsets = new List<Vector3>
    {
        new Vector3(0f, 2f, -5f),
        new Vector3(0f, 4f, -10f)
    };

    private CameraView currentView = CameraView.FirstPerson;

    public bool IsFirstPerson => currentView == CameraView.FirstPerson;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            playerCamera.enabled = true;
            ApplyCurrentView();
        }
        else
        {
            playerCamera.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            CycleView();
        }
    }

    private void CycleView()
    {
        currentView++;

        if ((int)currentView > 2)
            currentView = CameraView.FirstPerson;

        ApplyCurrentView();
    }

    private void ApplyCurrentView()
    {
        switch (currentView)
        {
            case CameraView.FirstPerson:
                ApplyFirstPerson();
                break;

            case CameraView.ThirdPerson:
                ApplyThirdPerson(0);
                break;

            case CameraView.FarThirdPerson:
                ApplyThirdPerson(1);
                break;
        }
    }

    private void ApplyFirstPerson()
    {
        playerVisual.SetActive(false);
        playerVisualHat.SetActive(false);

        if (playerNameTag != null)
            playerNameTag.SetActive(false);

        cameraTransform.localPosition = Vector3.zero;
    }

    private void ApplyThirdPerson(int offsetIndex)
    {
        playerVisual.SetActive(true);
        playerVisualHat.SetActive(true);

        if (playerNameTag != null)
            playerNameTag.SetActive(true);

        if (offsets.Count > offsetIndex)
            cameraTransform.localPosition = offsets[offsetIndex];
    }
}