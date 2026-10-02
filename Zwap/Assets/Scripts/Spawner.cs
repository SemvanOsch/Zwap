using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum SpawnZone
{
    Anywhere,    // full width, any X
    EdgesOnly,   // left band OR right band, never the middle
    LeftOnly,    // left band only
    RightOnly,   // right band only
    CenterOnly,  // middle band only
    Perch        // on the side rocks (left OR right bank), at the perch X positions below
}

[Serializable]
public class SpawnableItem
{
    public GameObject prefab;
    public SpawnZone spawnZone = SpawnZone.Anywhere;

    [Tooltip("Zet dit AAN voor bomen: de hele prefab wordt gespiegeld (X-scale omgedraaid) als hij aan de rechterkant spawnt. Laat UIT voor stenen en al het andere dat niet gespiegeld hoeft te worden.")]
    public bool mirrorOnRightSide = false;

    [Range(0f, 100f)]
    [Tooltip("Relatief spawn-gewicht. Hoeft niet op te tellen tot 100 over alle items - " +
             "wordt automatisch genormaliseerd. De custom inspector hieronder laat het " +
             "resulterende live percentage per item zien.")]
    public float weight = 10f;

    [Header("Multiples")]
    [Min(1)]
    [Tooltip("Minimum number of objects spawned each time this item is picked.")]
    public int minCount = 1;

    [Min(1)]
    [Tooltip("Maximum number of objects spawned each time this item is picked. The actual count is random between Min and Max. Set both to 1 for a single object.")]
    public int maxCount = 1;

    [Min(0f)]
    [Tooltip("Minimum X distance between objects of the same batch, so they don't overlap. 0 = no check. If no free spot is found after a few tries, that object is skipped.")]
    public float minSeparationX = 1f;

    [Min(0f)]
    [Tooltip("0 = all objects spawn in the same row (side by side). Above 0 = each extra object spawns this much further up. With a delay below you usually want this at 0.")]
    public float stackOffsetY = 0f;

    [Min(0f)]
    [Tooltip("Shortest delay (seconds) before the 2nd, 3rd, ... object of a batch spawns. The first object always spawns immediately. Each delay is re-rolled randomly between Min and Max.")]
    public float minDelay = 0f;

    [Min(0f)]
    [Tooltip("Longest delay (seconds) between objects of the same batch. Set both Min and Max to 0 to spawn the whole batch at once.")]
    public float maxDelay = 0f;
}

public class Spawner : MonoBehaviour
{
    [Header("Spawnables")]
    public SpawnableItem[] items;      // each item now carries its own spawn rule + weight
    public Transform spawnPoint;

    [Header("Timing")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnSpeedPerScore = 0.002f;

    [Header("Separate Can Spawn")]
    [Tooltip("The can is NOT part of the items array above - it spawns on its own timer, so it can appear at the same time as a normal object. Leave the prefab empty to disable can spawning.")]
    public SpawnableItem canItem;

    [Tooltip("Shortest time (seconds) between can spawns. Each interval is re-rolled randomly between min and max. Does NOT scale with score.")]
    [SerializeField] private float canSpawnIntervalMin = 3f;

    [Tooltip("Longest time (seconds) between can spawns.")]
    [SerializeField] private float canSpawnIntervalMax = 6f;

    [Header("Spawn Area (X range)")]
    [SerializeField] private float minX = -3f;
    [SerializeField] private float maxX = 3f;

    [Header("Center Band (used by CenterOnly / EdgesOnly)")]
    [SerializeField] private float centerMinX = -1f;
    [SerializeField] private float centerMaxX = 1f;

    [Header("Perch (used by SpawnZone.Perch, e.g. the heron)")]
    [Tooltip("X position of the left bank rocks where perching enemies sit. Select the Spawner to see it as a line in the Scene view.")]
    [SerializeField] private float leftPerchX = -3.2f;

    [Tooltip("X position of the right bank rocks where perching enemies sit.")]
    [SerializeField] private float rightPerchX = 3.2f;

    [Tooltip("Random +/- offset on the perch X, so herons don't all sit on the exact same spot.")]
    [SerializeField] private float perchJitterX = 0.1f;

    [Tooltip("Shifts perching enemies up/down relative to the spawn point (fine-tune so they sit ON the rock).")]
    [SerializeField] private float perchYOffset = 0f;

    private const int MaxPlacementAttempts = 10;

    private float timer;
    private float canTimer;
    private float canNextInterval;

    void Start()
    {
        RollNextCanInterval();
    }

    void Update()
    {
        timer += Time.deltaTime;

        float multiplier = 1f;

        if (ScoreManager.Instance != null)
            multiplier = 1f + spawnSpeedPerScore * ScoreManager.Instance.GetScore();

        float effectiveInterval = spawnInterval / multiplier;

        if (timer >= effectiveInterval)
        {
            SpawnEntity();
            timer = 0f;
        }

        // The can runs on its own timer, independent of the main pool above,
        // so it can land in the same frame as a normal object. It uses a
        // random interval in [min, max] that is re-rolled after every spawn
        // and deliberately does NOT scale with score.
        if (canItem != null && canItem.prefab != null)
        {
            canTimer += Time.deltaTime;
            if (canTimer >= canNextInterval)
            {
                SpawnItem(canItem);
                canTimer = 0f;
                RollNextCanInterval();
            }
        }
    }

    private void RollNextCanInterval()
    {
        float min = Mathf.Min(canSpawnIntervalMin, canSpawnIntervalMax);
        float max = Mathf.Max(canSpawnIntervalMin, canSpawnIntervalMax);
        canNextInterval = Random.Range(min, max);
    }

    void SpawnEntity()
    {
        if (items == null || items.Length == 0) return;

        SpawnableItem chosen = GetWeightedRandomItem();
        SpawnItem(chosen);
    }

    /// <summary>
    /// Starts a batch of one or more instances of the chosen item (count is
    /// random between minCount and maxCount). Shared by the main weighted
    /// pool and the separate can timer.
    /// </summary>
    private void SpawnItem(SpawnableItem chosen)
    {
        if (chosen == null || chosen.prefab == null) return;

        StartCoroutine(SpawnBatch(chosen));
    }

    /// <summary>
    /// Picks all X positions for the batch up front (so the separation check
    /// works), then spawns them one by one. The first spawns immediately; each
    /// following one waits a random time between minDelay and maxDelay.
    /// If no delay is set, the whole batch spawns in the same frame.
    /// </summary>
    private IEnumerator SpawnBatch(SpawnableItem chosen)
    {
        int minCount = Mathf.Max(1, chosen.minCount);
        int maxCount = Mathf.Max(minCount, chosen.maxCount);
        int count = Random.Range(minCount, maxCount + 1); // int overload: max is exclusive

        float baseY = spawnPoint.position.y + (chosen.spawnZone == SpawnZone.Perch ? perchYOffset : 0f);

        // Pick the positions first so objects in the same batch keep their distance.
        List<float> positions = new List<float>(count);
        for (int i = 0; i < count; i++)
        {
            if (TryPickX(chosen, positions, out float x))
                positions.Add(x);
            // else: no free spot left in this zone - skip this one
        }

        float minDelay = Mathf.Min(chosen.minDelay, chosen.maxDelay);
        float maxDelay = Mathf.Max(chosen.minDelay, chosen.maxDelay);

        for (int i = 0; i < positions.Count; i++)
        {
            if (i > 0 && maxDelay > 0f)
                yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));

            float y = baseY + i * chosen.stackOffsetY;
            SpawnSingle(chosen, positions[i], y);
        }
    }

    /// <summary>
    /// Picks a random X in the item's zone that is far enough from every X
    /// already used in this batch. Returns false if no valid spot was found.
    /// </summary>
    private bool TryPickX(SpawnableItem item, List<float> usedX, out float x)
    {
        x = 0f;

        for (int attempt = 0; attempt < MaxPlacementAttempts; attempt++)
        {
            float candidate = GetRandomX(item.spawnZone);

            bool tooClose = false;
            if (item.minSeparationX > 0f)
            {
                foreach (float other in usedX)
                {
                    if (Mathf.Abs(candidate - other) < item.minSeparationX)
                    {
                        tooClose = true;
                        break;
                    }
                }
            }

            if (!tooClose)
            {
                x = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Instantiates one object at the given position, applying right-side mirroring.
    /// </summary>
    private void SpawnSingle(SpawnableItem item, float x, float y)
    {
        Vector3 spawnPos = new Vector3(x, y, spawnPoint.position.z);
        GameObject spawned = Instantiate(item.prefab, spawnPos, Quaternion.identity);

        // Alleen items met mirrorOnRightSide aan (bv. bomen) worden gespiegeld, en
        // alleen als ze daadwerkelijk rechts van het midden van de spawn-band landen.
        if (item.mirrorOnRightSide)
        {
            float midX = (centerMinX + centerMaxX) * 0.5f;
            if (x > midX)
            {
                Vector3 scale = spawned.transform.localScale;
                scale.x *= -1f;
                spawned.transform.localScale = scale;
            }
        }
    }

    /// <summary>
    /// Roulette-wheel weighted pick: every item's weight is a slice of a
    /// number line from 0 to the sum of all weights. We roll a random point
    /// on that line and walk the items, adding up their slices, until the
    /// running total passes the roll - that item is the winner. Bigger
    /// weight = bigger slice = more likely to be picked. Weights don't need
    /// to sum to 100; they're normalized against each other automatically.
    /// </summary>
    private SpawnableItem GetWeightedRandomItem()
    {
        float totalWeight = 0f;
        foreach (SpawnableItem item in items)
            totalWeight += Mathf.Max(0f, item.weight);

        if (totalWeight <= 0f)
            return items[Random.Range(0, items.Length)]; // all weights 0 - fall back to a flat pick

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (SpawnableItem item in items)
        {
            cumulative += Mathf.Max(0f, item.weight);
            if (roll <= cumulative)
                return item;
        }

        // Floating point rounding fallback - practically never hit.
        return items[items.Length - 1];
    }

    private float GetRandomX(SpawnZone zone)
    {
        switch (zone)
        {
            case SpawnZone.LeftOnly:
                return Random.Range(minX, centerMinX);

            case SpawnZone.RightOnly:
                return Random.Range(centerMaxX, maxX);

            case SpawnZone.CenterOnly:
                return Random.Range(centerMinX, centerMaxX);

            case SpawnZone.EdgesOnly:
                // pick left band or right band, never the middle
                return Random.value < 0.5f
                    ? Random.Range(minX, centerMinX)
                    : Random.Range(centerMaxX, maxX);

            case SpawnZone.Perch:
                {
                    // sit on the left OR right bank rocks
                    float baseX = Random.value < 0.5f ? leftPerchX : rightPerchX;
                    return baseX + Random.Range(-perchJitterX, perchJitterX);
                }

            case SpawnZone.Anywhere:
            default:
                return Random.Range(minX, maxX);
        }
    }

#if UNITY_EDITOR
    // Shows the perch positions (yellow) and the spawn range (white) in the Scene view
    // while the Spawner is selected, so you can line the perch up with the side rocks.
    private void OnDrawGizmosSelected()
    {
        float y = spawnPoint != null ? spawnPoint.position.y : transform.position.y;
        float half = 6f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(leftPerchX, y - half + perchYOffset, 0f), new Vector3(leftPerchX, y + half + perchYOffset, 0f));
        Gizmos.DrawLine(new Vector3(rightPerchX, y - half + perchYOffset, 0f), new Vector3(rightPerchX, y + half + perchYOffset, 0f));

        Gizmos.color = Color.white;
        Gizmos.DrawLine(new Vector3(minX, y - half, 0f), new Vector3(minX, y + half, 0f));
        Gizmos.DrawLine(new Vector3(maxX, y - half, 0f), new Vector3(maxX, y + half, 0f));
    }

    /// <summary>
    /// Editor-only inspector. Compiled out entirely in real builds (UNITY_EDITOR
    /// isn't defined there), so it's safe to keep in the same file - no "Editor"
    /// folder needed. Draws the normal items array, then a live "Spawn Chances"
    /// panel showing each item's actual % based on its weight.
    /// </summary>
    [CustomEditor(typeof(Spawner))]
    private class SpawnerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty itemsProp = serializedObject.FindProperty("items");

            // Draw every field on Spawner exactly as Unity normally would.
            SerializedProperty prop = serializedObject.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(prop, true);
            }

            serializedObject.ApplyModifiedProperties();

            if (itemsProp == null || itemsProp.arraySize == 0)
                return;

            float total = 0f;
            for (int i = 0; i < itemsProp.arraySize; i++)
            {
                SerializedProperty entry = itemsProp.GetArrayElementAtIndex(i);
                total += Mathf.Max(0f, entry.FindPropertyRelative("weight").floatValue);
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Spawn Chances", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < itemsProp.arraySize; i++)
                {
                    SerializedProperty entry = itemsProp.GetArrayElementAtIndex(i);
                    SerializedProperty prefabProp = entry.FindPropertyRelative("prefab");
                    float weight = Mathf.Max(0f, entry.FindPropertyRelative("weight").floatValue);

                    float percent = total > 0f ? weight / total * 100f : 0f;

                    string displayName = prefabProp.objectReferenceValue != null
                        ? prefabProp.objectReferenceValue.name
                        : $"Item {i}";

                    EditorGUILayout.TextField(displayName, $"{percent:F1}%");
                }
            }

            if (total <= 0f)
                EditorGUILayout.HelpBox("All weights are 0 - items will be picked with equal odds instead.", MessageType.Warning);
        }
    }
#endif
}