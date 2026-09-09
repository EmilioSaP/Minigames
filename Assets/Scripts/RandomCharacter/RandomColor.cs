using System.Collections.Generic;
using UnityEngine;

public class RandomColor : MonoBehaviour
{
    [Tooltip("The objects to color. If left empty, it will color the object this script is attached to.")]
    public List<GameObject> baseGameObjects = new List<GameObject>();

    void Start()
    {
        if (baseGameObjects.Count == 0)
        {
            baseGameObjects.Add(this.gameObject);
        }

        Color randomColor = Random.ColorHSV(0f, 1f, 0.4f, 1f, 0.3f, 0.8f);
        bool rendererFound = false;

        foreach (GameObject baseGameObject in baseGameObjects)
        {
            if (baseGameObject == null)
                continue;

            Renderer objRenderer = baseGameObject.GetComponent<Renderer>();

            if (objRenderer == null)
                continue;

            // Note: If using URP/HDRP, you may need to use "_BaseColor" instead of "_Color"
            objRenderer.material.color = randomColor;
            rendererFound = true;
        }

        if (!rendererFound)
        {
            Debug.LogWarning("No Renderer found on the baseGameObjects!");
        }
    }
}