using UnityEngine;

public class RandomHat : MonoBehaviour
{
    [Tooltip("The parent object containing all the hat children.")]
    public GameObject randomHatParent;

    void Start()
    {
        if (randomHatParent == null)
        {
            randomHatParent = this.gameObject;
        }

        int childCount = randomHatParent.transform.childCount;

        if (childCount == 0)
        {
            Debug.LogWarning("The randomHatParent has no children to enable!");
            return;
        }

        // Pick a random child index
        int randomHatIndex = Random.Range(0, childCount);

        // Loop through all children, turn off the ones that weren't selected, and turn on the selected one
        for (int i = 0; i < childCount; i++)
        {
            GameObject childHat = randomHatParent.transform.GetChild(i).gameObject;
            childHat.SetActive(i == randomHatIndex);
        }
    }
}