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
    [SerializeField] private string logPath = @"D:\LegoVR\unity-lego-vr\Other_than_in_project_files\Model_Order_Data";

    private string trialCsvPath = "";
    private List<GameObject[]> trialModels = new();
    private List<GameObject[]> trialResources = new();

    private Dictionary<string, List<GameObject>> modelGroups = new();
    private HashSet<string> usedModelNames = new();
    private HashSet<string> usedResourceNames = new();

    private int currentTrial = 0;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeModelGroups();
            CreateTrials();
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
		                    Debug.LogError($"🚨 Missing resource for {model.name}");
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
        if (currentTrial >= trialModels.Count)
        {
            Debug.LogWarning("All trials have been assigned.");
            return;
        }

        gm.modelPrefabs = trialModels[currentTrial];
        gm.resourceBrickPrefabs = trialResources[currentTrial];
        gm.trialNumber = ParseTrialNumberFromScene();

        LogTrialData(gm.modelPrefabs, gm.resourceBrickPrefabs, gm.trialNumber, gm.participantCode);
        currentTrial++;
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
            writer.WriteLine("ParticipantCode,ConditionNumber,Order,ModelName,ResourceBrickName");

        for (int i = 0; i < models.Length; i++)
        {
            writer.WriteLine($"{participantCode},{trialNumber},{i},{models[i].name},{resources[i].name}");
        }

        Debug.Log($"Trial logged: {participantCode}, Condition {trialNumber} → {trialCsvPath}");
    }
}
