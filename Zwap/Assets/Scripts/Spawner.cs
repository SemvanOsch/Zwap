using System;
using System.Collections;
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
    CenterOnly   // middle band only
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
}

public class Spawner : MonoBehaviour
{
    [Header("Spawnables")]
    public SpawnableItem[] items;      // each item now carries its own spawn rule + weight
    public Transform spawnPoint;

    [Header("Timing")]
    [SerializeField] private float spawnInterval = 2f;
    [SerializeField] private float spawnSpeedPerScore = 0.002f;

    [Header("Spawn Area (X range)")]
    [SerializeField] private float minX = -3f;
    [SerializeField] private float maxX = 3f;

    [Header("Center Band (used by CenterOnly / EdgesOnly)")]
    [SerializeField] private float centerMinX = -1f;
    [SerializeField] private float centerMaxX = 1f;

    private float timer;

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
    }

    void SpawnEntity()
    {
        if (items == null || items.Length == 0) return;

        SpawnableItem chosen = GetWeightedRandomItem();
        if (chosen == null || chosen.prefab == null) return;

        float x = GetRandomX(chosen.spawnZone);
        Vector3 spawnPos = new Vector3(x, spawnPoint.position.y, spawnPoint.position.z);

        GameObject spawned = Instantiate(chosen.prefab, spawnPos, Quaternion.identity);

        // Alleen items met mirrorOnRightSide aan (bv. bomen) worden gespiegeld, en
        // alleen als ze daadwerkelijk rechts van het midden van de spawn-band landen.
        if (chosen.mirrorOnRightSide)
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

            case SpawnZone.Anywhere:
            default:
                return Random.Range(minX, maxX);
        }
    }

#if UNITY_EDITOR
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