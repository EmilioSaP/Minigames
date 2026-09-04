using System;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;

using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class ConnectionManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Relay")]
    [SerializeField] private int maxConnections = 4;

    private bool servicesInitialized = false;

    private async Task InitializeServices()
    {
        if (servicesInitialized)
            return;

        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            servicesInitialized = true;

            Debug.Log("Unity Services initialized.");
            Debug.Log($"Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Unity Services initialization failed: {e}");

            if (statusText != null)
                statusText.text = "Services initialization failed.";
        }
    }

    public async void HostGame()
    {
        await InitializeServices();

        try
        {
            statusText.text = "Creating Relay allocation...";

            Allocation allocation =
                await RelayService.Instance.CreateAllocationAsync(maxConnections);

            string joinCode =
                await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport =
                NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "wss")
            );

            transport.UseWebSockets = true;

            bool started = NetworkManager.Singleton.StartHost();

            if (started)
            {
                joinCodeText.text = $"Join Code: {joinCode}";
                statusText.text = "Hosting game!";

                Debug.Log($"HOST STARTED");
                Debug.Log($"Join Code: {joinCode}");
            }
            else
            {
                statusText.text = "Failed to start host.";
                Debug.LogError("NetworkManager failed to start Host.");
            }
        }
        catch (Exception e)
        {
            statusText.text = "Failed to create Relay.";

            Debug.LogError($"Host failed: {e}");
        }
    }

    public async void JoinGame()
    {
        await InitializeServices();

        string joinCode = joinCodeInput.text.Trim();

        if (string.IsNullOrEmpty(joinCode))
        {
            statusText.text = "Enter a join code.";
            return;
        }

        try
        {
            statusText.text = "Joining Relay...";

            JoinAllocation allocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport =
                NetworkManager.Singleton.GetComponent<UnityTransport>();

            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(allocation, "wss")
            );

            transport.UseWebSockets = true;

            bool started = NetworkManager.Singleton.StartClient();

            if (started)
            {
                statusText.text = "Joining game...";
                Debug.Log($"CLIENT STARTED");
                Debug.Log($"Join Code: {joinCode}");
            }
            else
            {
                statusText.text = "Failed to start client.";
                Debug.LogError("NetworkManager failed to start Client.");
            }
        }
        catch (Exception e)
        {
            statusText.text = "Failed to join game.";

            Debug.LogError($"Join failed: {e}");
        }
    }
}