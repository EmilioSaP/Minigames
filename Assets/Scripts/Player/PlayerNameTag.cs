using TMPro;
using UnityEngine;

public class PlayerNameTag : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform playerCamera;

    public void SetName(string playerName)
    {
        nameText.text = playerName;
    }

    private void LateUpdate()
    {
        if (playerCamera == null)
            return;

        Vector3 direction = playerCamera.position - transform.position;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(-direction);
        }
    }
}