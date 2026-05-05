using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using System.Linq;

public class RandomModelManager : MonoBehaviour
{
    public static RandomModelManager Instance;

    [Header("Master Prefab Lists")]
    public GameObject[] allModelPrefabs;
    public GameObject[] allResourceBrickPrefabs;

    [Header("Trial Settings")]
    [SerializeField] private int totalTrials = 3;

    [Header("Logging Settings")]
    [SerializeField] private bool saveTrialLogs = true;

    // Resolved at runtime to a portable path next to the .exe (project root in the Editor).
    // Previously a [SerializeField] string; the obsolete value may still be present in
    // existing scene/prefab YAML and is harmlessly ignored by Unity until the scene is re-saved.
    private string logPath => DataPaths.ModelOrderData;

    private string trialCsvPath = "";
    private List<GameObject[]> trialModels = new();
    private List<GameObject[]> trialResources = new();

    private Dictionary<string, List<GameObject>> modelGroups = new();
    private HashSet<string> usedModelNames = new();
    private HashSet<string> usedResourceNames = new();

    private int currentTrial = 0;

    // 30.07.2025 begin
    private string participantCode; // Default value, will be set in Awake()
    // 30.07.2025 end

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 30.07.2025 begin
            // Get the participant code from PlayerPrefs first, then fall back to managers.
            participantCode = PlayerPrefs.GetString("ParticipantCode",
                GameObject.Find("TutorialGameManager")?.GetComponent<TutorialGameManager>()?.participantCode
                ?? GameObject.Find("GameManager")?.GetComponent<GameManager>()?.participantCode
                ?? "Unknown");
            // 30.07.2025 end

            // First try to reuse an existing model-order CSV for this participant.
            // If that fails, create a new randomized order and log it.
            bool loadedFromExistingCsv = TryLoadTrialsFromExistingCsv(participantCode);

            if (!loadedFromExistingCsv)
            {
                InitializeModelGroups();
                CreateTrials();

                // Log the complete model order for all conditions once, up front.
                LogAllTrials(participantCode);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeModelGroups()
    {
        // Initialize model groups by complexity and type
        foreach (string key in new[] { "C1F", "C1A", "C2F", "C2A", "C3F", "C3A" })
        {
            modelGroups[key] = new List<GameObject>();
        }

        foreach (GameObject go in allModelPrefabs)
        {
            Match match = Regex.Match(go.name, @"(C\d)M\d+([FA])");
            if (match.Success)
            {
                string key = $"{match.Groups[1].Value}{match.Groups[2].Value}";
                modelGroups[key].Add(go);
            }
        }

        foreach (var list in modelGroups.Values)
            Shuffle(list, 0);
    }

		void CreateTrials()
		{
		    trialModels.Clear();
		    trialResources.Clear();

		    for (int trial = 0; trial < totalTrials; trial++)
		    {
		        List<(GameObject model, GameObject resource)> trialPairs = new();
		
		        // TM + TR always first
		        GameObject tm = allModelPrefabs.FirstOrDefault(go => go.name == "TM");
		        GameObject tr = allResourceBrickPrefabs.FirstOrDefault(go => go.name == "TR");
		
		        trialPairs.Add((tm, tr));
		        usedModelNames.Add(tm.name);
		        usedResourceNames.Add(tr.name);
		
		        // Add 6 items: 1F + 1A per complexity level
		        for (int complexity = 1; complexity <= 3; complexity++)
		        {
		            foreach (string type in new[] { "F", "A" })
		            {
		                string key = $"C{complexity}{type}";
		                GameObject model = PopRandomFrom(modelGroups[key]);
		                if (model == null)
		                {
		                    Debug.LogError($"Not enough models for group {key}");
		                    continue;
		                }
		
		                usedModelNames.Add(model.name);
		
		                GameObject resource = MatchResourceBrick(model.name);
		                if (resource == null)
		                {
		                    Debug.LogError($" Missing resource for {model.name}");
		                    continue;
		                }
		
		                usedResourceNames.Add(resource.name);
		                trialPairs.Add((model, resource));
		            }
		        }
		
		        // Shuffle items after TM/TR
		        Shuffle(trialPairs, 1);
		
		        trialModels.Add(trialPairs.Select(p => p.model).ToArray());
		        trialResources.Add(trialPairs.Select(p => p.resource).ToArray());
		    }
		}
	
    GameObject PopRandomFrom(List<GameObject> list)
    {
        if (list.Count == 0) return null;
        int index = UnityEngine.Random.Range(0, list.Count);
        GameObject selected = list[index];
        list.RemoveAt(index);
        return selected;
    }

    GameObject MatchResourceBrick(string modelName)
    {
        Match match = Regex.Match(modelName, @"(C\d)M(\d+)([FA])");
        if (!match.Success) return null;

        string resourceName = $"{match.Groups[1].Value}R{match.Groups[2].Value}{match.Groups[3].Value}";
        GameObject brick = allResourceBrickPrefabs.FirstOrDefault(r =>
            r.name == resourceName && !usedResourceNames.Contains(r.name));

        return brick;
    }

    void Shuffle<T>(List<T> list, int startIndex)
    {
        for (int i = list.Count - 1; i > startIndex; i--)
        {
            int j = UnityEngine.Random.Range(startIndex, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void AssignPrefabsToGameManager(GameManager gm)
    {
        // Determine the condition number from the active scene name (e.g., "Condition2").
        int conditionNumber = ParseTrialNumberFromScene();
        int idx = conditionNumber - 1;

        if (idx < 0 || idx >= trialModels.Count)
        {
            Debug.LogWarning($"RandomModelManager: No trial data available for condition {conditionNumber} (index {idx}).");
            return;
        }

        gm.modelPrefabs = trialModels[idx];
        gm.resourceBrickPrefabs = trialResources[idx];
        gm.trialNumber = conditionNumber;
    }

    int ParseTrialNumberFromScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        Match match = Regex.Match(sceneName, @"Condition(\d)");
        return match.Success ? int.Parse(match.Groups[1].Value) : currentTrial + 1;
    }

    void LogTrialData(GameObject[] models, GameObject[] resources, int trialNumber, string participantCode)
    {
        if (!saveTrialLogs) return;

        Directory.CreateDirectory(logPath);

        if (string.IsNullOrEmpty(trialCsvPath))
        {
            trialCsvPath = Path.Combine(
                logPath,
                $"{participantCode}_ModelOrder_{DateTime.Now:yyyy-MM-dd}.csv"
            );
        }

        bool fileExists = File.Exists(trialCsvPath);
        using StreamWriter writer = new StreamWriter(trialCsvPath, true);

        if (!fileExists)
            writer.WriteLine("ParticipantCode,ConditionNumber,Order,ModelName,ResourceBrickName,Completed");

        for (int i = 0; i < models.Length; i++)
        {
            writer.WriteLine($"{participantCode},{trialNumber},{i},{models[i].name},{resources[i].name},False");
        }

        Debug.Log($"Trial logged: {participantCode}, Condition {trialNumber} → {trialCsvPath}");
    }

    /// <summary>
    /// Logs all trials (for all conditions) once when they are first created,
    /// so that resume or post-hoc analysis can see the full order from the start.
    /// </summary>
    void LogAllTrials(string participantCode)
    {
        if (!saveTrialLogs) return;

        for (int conditionNumber = 1; conditionNumber <= trialModels.Count; conditionNumber++)
        {
            int idx = conditionNumber - 1;
            if (idx < 0 || idx >= trialModels.Count) continue;

            LogTrialData(trialModels[idx], trialResources[idx], conditionNumber, participantCode);
        }
    }

    /// <summary>
    /// Attempts to load existing trial definitions from the latest
    /// model-order CSV for the given participant. If successful,
    /// populates trialModels/trialResources and sets trialCsvPath so
    /// completion updates reuse the same file.
    /// </summary>
    private bool TryLoadTrialsFromExistingCsv(string participant)
    {
        try
        {
            if (string.IsNullOrEmpty(participant)) return false;
            if (!Directory.Exists(logPath)) return false;

            DirectoryInfo dirInfo = new DirectoryInfo(logPath);
            // Look for any existing model order files for this participant.
            FileInfo latestCsv = dirInfo
                .GetFiles($"{participant}_ModelOrder_*.csv")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latestCsv == null)
            {
                return false;
            }

            string[] lines = File.ReadAllLines(latestCsv.FullName);
            if (lines.Length <= 1)
            {
                return false;
            }

            // Group rows by condition number.
            var byCondition = new Dictionary<int, List<(int order, string modelName, string resourceName)>>();

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                string[] parts = lines[i].Split(',');
                if (parts.Length < 5) continue;

                if (!int.TryParse(parts[1], out int conditionNumber)) continue;
                if (!int.TryParse(parts[2], out int order)) continue;

                string modelName = parts[3].Trim();
                string resourceName = parts[4].Trim();

                if (!byCondition.TryGetValue(conditionNumber, out var list))
                {
                    list = new List<(int, string, string)>();
                    byCondition[conditionNumber] = list;
                }

                list.Add((order, modelName, resourceName));
            }

            if (byCondition.Count == 0)
            {
                return false;
            }

            trialModels.Clear();
            trialResources.Clear();

            // Build trial arrays in ascending condition order.
            foreach (int conditionNumber in byCondition.Keys.OrderBy(c => c))
            {
                var entries = byCondition[conditionNumber]
                    .OrderBy(e => e.order)
                    .ToList();

                var models = new List<GameObject>();
                var resources = new List<GameObject>();

                foreach (var (order, modelName, resourceName) in entries)
                {
                    GameObject modelPrefab = allModelPrefabs.FirstOrDefault(go => go.name == modelName);
                    GameObject resourcePrefab = allResourceBrickPrefabs.FirstOrDefault(go => go.name == resourceName);

                    if (modelPrefab == null || resourcePrefab == null)
                    {
                        Debug.LogWarning($"RandomModelManager: Could not find prefabs for model '{modelName}' or resource '{resourceName}' when loading from CSV.");
                        continue;
                    }

                    models.Add(modelPrefab);
                    resources.Add(resourcePrefab);
                }

                if (models.Count > 0 && resources.Count == models.Count)
                {
                    trialModels.Add(models.ToArray());
                    trialResources.Add(resources.ToArray());
                }
            }

            if (trialModels.Count == 0)
            {
                return false;
            }

            // Reuse this CSV file for completion updates.
            trialCsvPath = latestCsv.FullName;

            Debug.Log($"RandomModelManager: Loaded existing model order from '{trialCsvPath}' for participant '{participant}'.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"RandomModelManager: Failed to load existing trials from CSV. {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Marks a specific model (by order index) as completed in the model-order CSV.
    /// </summary>
    public void MarkModelCompleted(string participantCode, int conditionNumber, int orderIndex)
    {
        if (!saveTrialLogs) return;

        try
        {
            // Ensure we know which CSV file to edit.
            if (string.IsNullOrEmpty(trialCsvPath) || !File.Exists(trialCsvPath))
            {
                Debug.LogWarning("RandomModelManager: No trial CSV to mark completion in.");
                return;
            }

            var lines = File.ReadAllLines(trialCsvPath).ToList();
            if (lines.Count <= 1) return; // header only

            // Header: ParticipantCode,ConditionNumber,Order,ModelName,ResourceBrickName,Completed
            for (int i = 1; i < lines.Count; i++)
            {
                string[] parts = lines[i].Split(',');
                if (parts.Length < 6) continue;

                if (!int.TryParse(parts[1], out int cond)) continue;
                if (!int.TryParse(parts[2], out int order)) continue;

                if (parts[0] == participantCode && cond == conditionNumber && order == orderIndex)
                {
                    parts[5] = "True"; // mark as completed
                    lines[i] = string.Join(",", parts);
                    break;
                }
            }

            File.WriteAllLines(trialCsvPath, lines);
        }
        catch (Exception ex)
        {
            Debug.LogError($"RandomModelManager: Failed to mark model completed. {ex.Message}");
        }
    }
}
