using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;


[System.Serializable]
public class SpendingEntry
{
    public string date;
    public string category;
    public float amount;
}

[System.Serializable]
public class SpendingData
{
    public List<SpendingEntry> entries;
}

[System.Serializable]
public class GameState
{
    public List<List<string>> map_data;
    public bool house_placed;
    public List<int> house_location;
    public bool buildings_placed;
    public List<List<object>> building_locations;

    public GameState()
    {
        map_data = new List<List<string>>();
        house_placed = false;
        house_location = null;
        buildings_placed = false;
        building_locations = new List<List<object>>();
    }
}

public class MapStateManager : MonoBehaviour
{
    // Configuration
    private const int MAP_SIZE = 20;
    private const string STATE_FILE_NAME = "map_state.json";
    private const string USER_DATA_FOLDER = "UserData/";
    private const string EMPTY = "0";
    private const string ROAD = "1";
    private const string HOUSE = "H";
    private const string ONLINE_SHOPPING = "W"; // warehouse
    private const string SHOPPING = "F"; // factory
    private const string EATING_OUT = "M"; // WHACKDONALD'S
    private const string NIGHT = "N"; // nightclub
    private const string GROCERIES = "T"; // A TREE

    // Prefabs
    public GameObject housePrefab;
    public GameObject warehousePrefab;
    public GameObject factoryPrefab;
    public GameObject mcdonaldsPrefab;
    public GameObject nightclubPrefab;
    public GameObject treePrefab;
    public GameObject roadPrefab;

    private string currentUserEmail;

    private string GetFilePath(string fileName)
    {
        return Path.Combine(Application.dataPath, fileName);
    }

    private string GetUserEmail()
    {
        // Retrieve the email saved by LoginManager
        string email = PlayerPrefs.GetString("CurrentUserEmail", "");
        
        if (string.IsNullOrEmpty(email))
        {
            Debug.LogError("No user email found in PlayerPrefs. User must log in first.");
        }
        
        return email;
    }

    private string SanitizeEmailForFilename(string email)
    {
        // Same sanitization logic as LoginManager
        return email.Replace("@", "_at_").Replace(".", "_");
    }

    private string MapCategoryToBuildingType(string category)
    {
        switch (category)
        {
            case "EO": return "M"; // Eating Out
            case "OS": return "W"; // Online Shopping
            case "SH": return "F"; // Shein
            case "NI": return "N"; // Nightlife
            case "GR": return "T"; // Groceries
            default: return category;
        }
    }

    private string GetSpendingFilePath()
    {
        string email = GetUserEmail();
        
        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        string sanitized = SanitizeEmailForFilename(email);
        string fileName = $"{sanitized}_data.json";
        
        // Check in UserData folder first (as created by LoginManager)
        string userDataPath = Path.Combine(Application.dataPath, USER_DATA_FOLDER, fileName);
        if (File.Exists(userDataPath))
        {
            return userDataPath;
        }
        
        // Fallback to Assets root
        string assetsPath = Path.Combine(Application.dataPath, fileName);
        if (File.Exists(assetsPath))
        {
            return assetsPath;
        }

        Debug.LogError($"Spending data file not found at {userDataPath} or {assetsPath}");
        return null;
    }

    public List<SpendingEntry> LoadSpendingData()
    {
        string filePath = GetSpendingFilePath();
        
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("Could not determine spending data file path.");
            return new List<SpendingEntry>();
        }

        string json = File.ReadAllText(filePath);
        
        // Wrap the array in an object for Unity's JsonUtility
        string wrappedJson = "{\"entries\":" + json + "}";
        SpendingData data = JsonUtility.FromJson<SpendingData>(wrappedJson);
        Debug.Log($"File path being checked: {filePath}");
        
        Debug.Log($"Loaded {data.entries.Count} spending entries from {filePath}");
        return data.entries;
    }

    public Dictionary<string, int> CalculateBuildingCounts(List<SpendingEntry> spendingData)
    {
        // Group by category and sum amounts
        Dictionary<string, float> categoryTotals = new Dictionary<string, float>();
        
        foreach (var entry in spendingData)
        {
            if (!categoryTotals.ContainsKey(entry.category))
            {
                categoryTotals[entry.category] = 0;
            }
            categoryTotals[entry.category] += entry.amount;
        }

        // Calculate total spending
        float totalSpending = categoryTotals.Values.Sum();
        
        // Available spots (excluding roads and house)
        int roadRows = MAP_SIZE / 2; // Every other row
        int totalCells = MAP_SIZE * MAP_SIZE;
        int roadCells = roadRows * MAP_SIZE;
        int availableSpots = totalCells - roadCells - 1; // -1 for house

        Debug.Log($"Total spending: {totalSpending}, Available spots: {availableSpots}");

        // Calculate building count per category based on spending proportion
        Dictionary<string, int> buildingCounts = new Dictionary<string, int>();
        
        foreach (var kvp in categoryTotals)
        {
            float proportion = kvp.Value / totalSpending;
            int count = Mathf.RoundToInt(proportion * availableSpots);
            buildingCounts[kvp.Key] = Mathf.Max(1, count); // At least 1 building per category
            Debug.Log($"Category {kvp.Key}: ${kvp.Value:F2} ({proportion:P1}) -> {count} buildings");
        }

        return buildingCounts;
    }

    public GameState LoadGameState()
    {
        string filePath = GetFilePath(STATE_FILE_NAME);

        // Default blank state
        GameState defaultState = new GameState();
        for (int i = 0; i < MAP_SIZE; i++)
        {
            List<string> row = new List<string>();
            for (int j = 0; j < MAP_SIZE; j++)
            {
                row.Add(EMPTY);
            }
            defaultState.map_data.Add(row);
        }

        if (File.Exists(filePath))
        {
            string json = File.ReadAllText(filePath);

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.Log("Existing file is empty, reinitialising new state...");
                return defaultState;
            }

            Debug.Log($"Loading existing state from {filePath}...");
            GameState loadedState = JsonUtility.FromJson<GameState>(json);

            if (loadedState.map_data == null || loadedState.map_data.Count == 0)
            {
                Debug.Log("Loaded file has no map data, regenerating default map...");
                return defaultState;
            }

            return loadedState;
        }
        else
        {
            Debug.Log("No existing state found. Initializing new map...");
            return defaultState;
        }
    }

    public void SaveGameState(GameState state)
    {
        string filePath = GetFilePath(STATE_FILE_NAME);
        string json = JsonUtility.ToJson(state, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"State successfully saved to {filePath}");
    }

    public void RenderMap(GameState state)
    {
        float cellSize = 1.0f;
        for (int row = 0; row < MAP_SIZE; row++)
        {
            for (int col = 0; col < MAP_SIZE; col++)
            {
                string cell = state.map_data[row][col];
                float offsetX = -(MAP_SIZE * cellSize) / 2f;
                float offsetY = (MAP_SIZE * cellSize) / 2f;

                Vector3 position = new Vector3(
                    offsetX + col * cellSize + cellSize / 2f,
                    offsetY - row * cellSize - cellSize / 2f,
                    0
                );
                GameObject prefab = null;

                switch (cell)
                {
                    case "H": prefab = housePrefab; break;
                    case "W": prefab = warehousePrefab; break;
                    case "F": prefab = factoryPrefab; break;
                    case "M": prefab = mcdonaldsPrefab; break;
                    case "N": prefab = nightclubPrefab; break;
                    case "T": prefab = treePrefab; break;
                    case "1": prefab = roadPrefab; break;
                }

                if (prefab != null)
                {
                    GameObject obj = Instantiate(prefab, position, Quaternion.identity);
                    var info = obj.AddComponent<BuildingInfo>();
                    info.buildingType = cell;
                    info.builtDate = DateTime.Now.ToString("yyyy-MM-dd");
                    info.spentOn = GetSpentOnFromType(cell);
                    info.spentAmount = UnityEngine.Random.Range(100f, 1000f);
                }
            }
        }
    }

    string GetSpentOnFromType(string type)
    {
        switch (type)
        {
            case "M": return "Eating Out";
            case "W": return "Online Shopping";
            case "F": return "Shein";
            case "N": return "Nightlife";
            case "T": return "Groceries";
            case "S": return "Retail Shopping";
            case "H": return "Rent";
            case "A": return "Activities";
            default: return "Misc";
        }
    }

    public GameState PlaceHouse(GameState state)
    {
        if (state.house_placed)
        {
            Debug.Log($"House already placed at [{state.house_location[0]}, {state.house_location[1]}]. Skipping placement.");
            return state;
        }

        // Place house in center of map
        int row = MAP_SIZE / 2;
        int col = MAP_SIZE / 2;

        // Update the map and state variables
        if (state.map_data[row][col] == EMPTY)
        {
            state.map_data[row][col] = HOUSE;
            state.house_placed = true;
            state.house_location = new List<int> { row, col };
        }

        Debug.Log($"House placed at center: Row: {row}, Col: {col}");
        return state;
    }

    public (GameState, List<object>) PlaceBuildingRandom(GameState state, string buildingType)
    {
        int maxAttempts = 1000;
        int attempts = 0;

        while (attempts < maxAttempts)
        {
            int row = UnityEngine.Random.Range(0, MAP_SIZE);
            int col = UnityEngine.Random.Range(0, MAP_SIZE);

            if (state.map_data[row][col] == EMPTY)
            {
                state.map_data[row][col] = buildingType;
                List<object> location = new List<object> { row, col, buildingType };
                Debug.Log($"Building {buildingType} placed at Row: {row}, Col: {col}");
                return (state, location);
            }
            attempts++;
        }

        Debug.LogWarning($"Could not find empty spot for {buildingType} after {maxAttempts} attempts");
        return (state, null);
    }

    public void AddBuilding(string buildingType, GameState gameState)
    {
        var (updatedState, newLocation) = PlaceBuildingRandom(gameState, buildingType);
        
        if (newLocation != null)
        {
            gameState.building_locations.Add(newLocation);
            gameState.buildings_placed = true;
        }
    }

    void Start()
    {
        // 1. Load spending data
        List<SpendingEntry> spendingData = LoadSpendingData();
        
        if (spendingData.Count == 0)
        {
            Debug.LogError("No spending data available. Cannot generate map.");
            return;
        }

        // 2. Calculate building counts based on spending
        Dictionary<string, int> buildingCounts = CalculateBuildingCounts(spendingData);

        // 3. Initialize game state
        GameState gameState = new GameState();
        for (int i = 0; i < MAP_SIZE; i++)
        {
            List<string> row = new List<string>();
            for (int j = 0; j < MAP_SIZE; j++)
            {
                row.Add(EMPTY);
            }
            gameState.map_data.Add(row);
        }

        // 4. Place roads (every other row)
        for (int i = 1; i < MAP_SIZE; i += 2)
        {
            for (int j = 0; j < MAP_SIZE; j++)
            {
                gameState.map_data[i][j] = ROAD;
            }
        }

        // 5. Place house
        gameState = PlaceHouse(gameState);

        // 6. Place buildings based on spending data
        foreach (var kvp in buildingCounts)
        {
            string category = kvp.Key;
            int count = kvp.Value;

            for (int i = 0; i < count; i++)
            {
                AddBuilding(MapCategoryToBuildingType(category), gameState);
            }
        }

        // 7. Save the state
        SaveGameState(gameState);

        // 8. Render the map
        RenderMap(gameState);
        Debug.Log($"Map generated based on spending data:\n{JsonUtility.ToJson(gameState, true)}");
    }
}