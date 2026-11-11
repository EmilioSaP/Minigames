
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ParkourMatchResultsUI : MonoBehaviour, IMatchResultsUI
{
    [Serializable]
    private class PlayerCountLayout
    {
        [Header("Player Count Range")]
        [Min(1)] public int minimumPlayers = 1;
        [Min(1)] public int maximumPlayers = 2;

        [Header("Row Layout")]
        [Min(0f)] public float rowHeight = 90f;
        [Min(0f)] public float spacing = 10f;

        [Header("Container Padding")]
        public int paddingLeft = 0;
        public int paddingRight = 0;
        public int paddingTop = 0;
        public int paddingBottom = 0;
    }

    private class RaceResultEntry
    {
        public ulong ClientId;
        public string Username;

        public bool DidNotFinish;
        public bool IsWinner;

        public double CompletionTime;
        public int CompletionOrder;
        public int ParticipantOrder;
    }

    [Header("References")]
    [SerializeField] private FadeCanvasGroupScript faderesultsGroup;

    [SerializeField] private RectTransform resultsListRoot;

    [SerializeField] private VerticalLayoutGroup resultsLayout;

    [SerializeField] private IndividualRaceResult resultPrefab;

    [SerializeField] private MatchResultsManager resultsManager;

    [Header("Player Count Layouts")]
    [SerializeField] private PlayerCountLayout[] layoutPresets;

    [Header("Animation Settings")]
    [SerializeField] private float rowStaggerDelay = 0.2f;
    [SerializeField] private float screenExitAnimationDuration = 0.5f;

    private readonly List<IndividualRaceResult> instantiatedRows = new();

    private Coroutine buildCoroutine;
    private Coroutine hideCoroutine;

    private bool isVisible;
    private bool hasBuiltResults;

    private void Awake()
    {
        //SetCanvasVisible(false);

        if (resultsLayout == null && resultsListRoot != null)
        {
            resultsLayout =
                resultsListRoot.GetComponent<VerticalLayoutGroup>();
        }
    }

    private void OnDisable()
    {
        if (buildCoroutine != null)
        {
            StopCoroutine(buildCoroutine);
            buildCoroutine = null;
        }

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    // ------------------------------------
    // IMatchResultsUI
    // ------------------------------------

    public void SetResultsVisible(bool visible)
    {
        if (visible)
        {
            ShowResults();
        }
        else
        {
            HideResults();
        }
    }

    // ------------------------------------
    // VISIBILITY
    // ------------------------------------

    private void ShowResults()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        isVisible = true;

        SetCanvasVisible(true);

        if (!hasBuiltResults && buildCoroutine == null)
        {
            buildCoroutine = StartCoroutine(BuildResultsWhenReady());
        }
    }

    private void HideResults()
    {
        if (!isVisible)
        {
            SetCanvasVisible(false);
            return;
        }

        isVisible = false;

        // Stop building if the screen is hidden.
        if (buildCoroutine != null)
        {
            StopCoroutine(buildCoroutine);
            buildCoroutine = null;
        }

        // Play each row's exit animation.
        foreach (IndividualRaceResult row in instantiatedRows)
        {
            if (row != null)
                row.PlayExit();
        }

        // Allow the individual animations to finish
        // before hiding the entire results screen.
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterAnimation());
    }

    private IEnumerator HideAfterAnimation()
    {
        yield return new WaitForSecondsRealtime(
            screenExitAnimationDuration
        );

        SetCanvasVisible(false);

        hideCoroutine = null;
    }

    private void SetCanvasVisible(bool visible)
    {
        if (faderesultsGroup == null)
            return;

        if (visible)
        {
            faderesultsGroup.FadeIn();
        }
        else
        {
            faderesultsGroup.FadeOut();
        }
    }

    // ------------------------------------
    // BUILD RESULTS
    // ------------------------------------

    private IEnumerator BuildResultsWhenReady()
    {
        // Find the manager if it wasn't assigned.
        while (resultsManager == null || !resultsManager.IsSpawned)
        {
            if (!isVisible)
            {
                buildCoroutine = null;
                yield break;
            }

            if (resultsManager == null)
            {
                resultsManager =
                    FindFirstObjectByType<MatchResultsManager>();
            }

            yield return null;
        }

        // Wait for the participant snapshot and all result
        // NetworkLists to synchronize.
        while (!resultsManager.AreResultsFinalized)
        {
            if (!isVisible)
            {
                buildCoroutine = null;
                yield break;
            }

            yield return null;
        }

        // Allow synchronized list changes to finish propagating.
        yield return null;

        if (!isVisible)
        {
            buildCoroutine = null;
            yield break;
        }

        yield return BuildResults();

        buildCoroutine = null;
    }

    private IEnumerator BuildResults()
    {
        if (resultPrefab == null || resultsListRoot == null)
        {
            Debug.LogError(
                "ParkourMatchResultsUI is missing its result prefab or list root."
            );

            yield break;
        }

        ClearExistingRows();

        int totalPlayers = resultsManager.ParticipantCount;

        if (totalPlayers == 0)
        {
            Debug.LogWarning("No participants found for match results.");
            hasBuiltResults = true;
            yield break;
        }

        // Select layout based on total number of participants.
        PlayerCountLayout selectedLayout =
            GetLayoutForPlayerCount(totalPlayers);

        ApplyLayout(selectedLayout);

        // Retrieve and prepare the result information.
        List<RaceResultEntry> entries = BuildResultEntries();

        // Sort finishers first, followed by DNF players.
        entries.Sort(CompareResults);

        int placement = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            RaceResultEntry entry = entries[i];

            if (!entry.DidNotFinish)
                placement++;

            IndividualRaceResult row =
                Instantiate(resultPrefab, resultsListRoot);

            instantiatedRows.Add(row);

            row.SetResult(
                placement,
                entry.Username,
                FormatCompletionTime(entry.CompletionTime),
                entry.DidNotFinish,
                entry.IsWinner,
                totalPlayers,
                selectedLayout.rowHeight
            );

            row.PlayEntrance();

            Debug.Log(
                $"Results row {i + 1}: {entry.Username}, " +
                $"{(entry.DidNotFinish ? "DNF" : FormatCompletionTime(entry.CompletionTime))}"
            );

            // Stagger each entrance animation.
            if (i < entries.Count - 1 && rowStaggerDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(rowStaggerDelay);
            }
        }

        hasBuiltResults = true;

        Debug.Log($"Parkour results UI built. Players: {totalPlayers}");
    }

    // ------------------------------------
    // RESULT DATA
    // ------------------------------------

    private List<RaceResultEntry> BuildResultEntries()
    {
        List<RaceResultEntry> entries = new();

        for (int i = 0; i < resultsManager.ParticipantCount; i++)
        {
            MatchParticipantRecord participant =
                resultsManager.GetParticipantAt(i);

            RaceResultEntry entry = new RaceResultEntry
            {
                ClientId = participant.ClientId,
                Username = participant.PlayerName.ToString(),
                IsWinner = resultsManager.IsWinner(participant.ClientId),
                ParticipantOrder = i
            };

            if (resultsManager.TryGetCompletionRecord(
                participant.ClientId,
                out PlayerCompletionRecord completion))
            {
                entry.DidNotFinish = false;
                entry.CompletionTime = completion.CompletionTime;
                entry.CompletionOrder = completion.CompletionOrder;
            }
            else
            {
                entry.DidNotFinish = true;
                entry.CompletionTime = double.MaxValue;
            }

            if (string.IsNullOrWhiteSpace(entry.Username))
            {
                entry.Username = $"Player {entry.ClientId}";
            }

            entries.Add(entry);
        }

        return entries;
    }

    private int CompareResults(
        RaceResultEntry a,
        RaceResultEntry b)
    {
        // Finishers always appear before DNF players.
        if (a.DidNotFinish != b.DidNotFinish)
        {
            return a.DidNotFinish ? 1 : -1;
        }

        // Sort finishers by completion time.
        if (!a.DidNotFinish)
        {
            int timeComparison =
                a.CompletionTime.CompareTo(b.CompletionTime);

            if (timeComparison != 0)
                return timeComparison;

            // Consistent order for exact ties.
            return a.CompletionOrder.CompareTo(b.CompletionOrder);
        }

        // Keep DNF players in their original participant order.
        return a.ParticipantOrder.CompareTo(b.ParticipantOrder);
    }

    private string FormatCompletionTime(double seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(
            Math.Max(0, seconds)
        );

        int minutes = (int)time.TotalMinutes;

        return $"{minutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    // ------------------------------------
    // LAYOUT
    // ------------------------------------

    private PlayerCountLayout GetLayoutForPlayerCount(int count)
    {
        if (layoutPresets != null)
        {
            foreach (PlayerCountLayout preset in layoutPresets)
            {
                if (preset == null)
                    continue;

                if (count >= preset.minimumPlayers &&
                    count <= preset.maximumPlayers)
                {
                    return preset;
                }
            }
        }

        // Default layout if no matching preset is configured.
        return new PlayerCountLayout
        {
            minimumPlayers = 1,
            maximumPlayers = 8,
            rowHeight = 80f,
            spacing = 8f
        };
    }

    private void ApplyLayout(PlayerCountLayout preset)
    {
        if (resultsLayout == null)
            return;

        resultsLayout.spacing = preset.spacing;

        resultsLayout.padding = new RectOffset(
            preset.paddingLeft,
            preset.paddingRight,
            preset.paddingTop,
            preset.paddingBottom
        );
    }

    // ------------------------------------
    // CLEANUP
    // ------------------------------------

    private void ClearExistingRows()
    {
        foreach (IndividualRaceResult row in instantiatedRows)
        {
            if (row != null)
                Destroy(row.gameObject);
        }

        instantiatedRows.Clear();
    }
}
