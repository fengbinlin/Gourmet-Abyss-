using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
// TMP
using TMPro;
public class AllGameManager : MonoBehaviour
{
    public static AllGameManager Instance { get; private set; }
    
    [Header("Game Phase")]
    public GamePhase currentPhase = GamePhase.Selection;
    
    [Header("Player Scores")]
    public int player1Score = 0;
    public int player2Score = 0;
    
    [Header("Score Rules")]
    public int firstPlaceScore = 10;  // First place score
    public int secondPlaceScore = 5;  // Second place score
    public int targetScore = 25;  // Target score to win
    
    [Header("Round Settings")]
    public int maxRounds = 5;  // Max rounds
    private int currentRound = 0;  // Current round (starts from 1)
    
    [Header("Score Display UI")]
    public TextMeshProUGUI player1ScoreText;  // Player1 score display
    public TextMeshProUGUI player2ScoreText;  // Player2 score display
    public TextMeshProUGUI targetScoreText;   // Target score display
    
    [Header("Victory UI")]
    public GameObject victoryUI;  // Victory screen
    public TextMeshProUGUI victoryText;  // Victory text
    
    [Header("Scene Object Management")]
    public GameObject selectPlayersContainer;  // Selection phase players
    public GameObject buildingUnitsContainer;  // Building units for selection
    public GameObject placementPlayersContainer; // Placement phase players
    public GameObject playingPlayersContainer; // Playing phase players
    public GameObject levelEnvironmentContainer; // Level environment
    
    [Header("Player Controllers")]
    public SelectPlayerController player1SelectController;
    public SelectPlayerController player2SelectController;
    
    [Header("Place Manager")]
    public PlaceManager placeManager;
    
    [Header("Playing Player Objects")]
    public GameObject player1PlayingObject; // Player1 playing object
    public GameObject player2PlayingObject; // Player2 playing object
    
    // Delegates
    public delegate void SelectionPhaseStartedDelegate(int currentRound, int maxRounds);
    public delegate void DeploymentPhaseCompletedDelegate();
    
    // Events
    public static event SelectionPhaseStartedDelegate OnSelectionPhaseStarted;
    public static event DeploymentPhaseCompletedDelegate OnDeploymentPhaseCompleted;
    
    // Level completion records
    private List<string> finishOrder = new List<string>(); // Finish order
    private Dictionary<string, bool> playerDead = new Dictionary<string, bool>(); // Player death status
    
    // Playing player initial positions (for reset)
    private Vector3 player1InitialPosition;
    private Vector3 player2InitialPosition;
    private bool initialPositionsSaved = false;

    // SelectPlayer initial positions (for reset)
    private Vector3 player1SelectInitialPosition;
    private Vector3 player2SelectInitialPosition;
    private bool selectInitialPositionsSaved = false;

    private void SaveSelectPlayersInitialPositions()
    {
        if (selectInitialPositionsSaved) return;
        
        if (player1SelectController != null)
        {
            player1SelectInitialPosition = player1SelectController.transform.position;
            Debug.Log($"Saved Player1SelectController initial position: {player1SelectInitialPosition}");
        }
        
        if (player2SelectController != null)
        {
            player2SelectInitialPosition = player2SelectController.transform.position;
            Debug.Log($"Saved Player2SelectController initial position: {player2SelectInitialPosition}");
        }
        
        selectInitialPositionsSaved = true;
    }
    private void ResetSelectPlayersPositions()
    {
        if (player1SelectController != null)
        {
            player1SelectController.transform.position = player1SelectInitialPosition;
        }
        if (player2SelectController != null)
        {
            player2SelectController.transform.position = player2SelectInitialPosition;
        }
    }

    // Game phase enum
    public enum GamePhase
    {
        Selection,  // Selection phase
        Placement,  // Placement phase
        Playing     // Playing phase
    }
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Initialize player status
        playerDead["Player1"] = false;
        playerDead["Player2"] = false;
        
        // Initialize scores (no cache)
        player1Score = 0;
        player2Score = 0;
    }
    
    private void Start()
    {
        // Save playing players initial positions
        SavePlayingPlayersInitialPositions();

        // Save SelectPlayer initial positions
        SaveSelectPlayersInitialPositions();
        // Initialize victory UI
        if (victoryUI != null)
        {
            victoryUI.SetActive(false);
        }
        
        // Reset round
        currentRound = 0;
        
        // Initialize score display
        UpdateScoreDisplay();
        
        // Start with selection phase
        SwitchToSelectionPhase();
    }
    
    private void Update()
    {
        // Listen for Shift+1 to return to main menu
        if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) && Input.GetKeyDown(KeyCode.Alpha1))
        {
            ReturnToMainMenu();
        }
    }

    private void SetActiveSafe(GameObject target, bool state)
    {
        if (target == null) return;
        if (target.activeSelf != state)
        {
            target.SetActive(state);
        }
    }
    
    /// <summary>Save playing players initial positions</summary>
    private void SavePlayingPlayersInitialPositions()
    {
        if (initialPositionsSaved) return;
        
        if (player1PlayingObject != null)
        {
            player1InitialPosition = player1PlayingObject.transform.position;
            Debug.Log($"Saved Player1 initial position: {player1InitialPosition}");
        }
        
        if (player2PlayingObject != null)
        {
            player2InitialPosition = player2PlayingObject.transform.position;
            Debug.Log($"Saved Player2 initial position: {player2InitialPosition}");
        }
        
        initialPositionsSaved = true;
    }
    
    /// <summary>Reset playing players positions</summary>
    private void ResetPlayingPlayersPositions()
    {
        ReactivatePlayingPlayers();

        if (player1PlayingObject != null)
        {
            player1PlayingObject.transform.position = player1InitialPosition;
            Debug.Log($"Reset Player1 position to: {player1InitialPosition}");
        }
        
        if (player2PlayingObject != null)
        {
            player2PlayingObject.transform.position = player2InitialPosition;
            Debug.Log($"Reset Player2 position to: {player2InitialPosition}");
        }
    }

    private void ReactivatePlayingPlayers()
    {
        if (playingPlayersContainer == null) return;

        foreach (Transform child in playingPlayersContainer.transform)
        {
            if (!child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(true);
                Debug.Log($"Reactivated playing player: {child.name}");
            }
        }
    }
    
    /// <summary>Switch to Selection Phase</summary>
    public void SwitchToSelectionPhase()
    {
        // Reset SelectPlayer positions
        ResetSelectPlayersPositions();

        // Increase round
        currentRound++;
        
        currentPhase = GamePhase.Selection;
        Debug.Log($"=== Selection Phase - Round {currentRound}/{maxRounds} (Target: {targetScore}) ===");
        
        // Activate/hide phase containers
        SetActiveSafe(selectPlayersContainer, true);
        SetActiveSafe(placementPlayersContainer, false);
        SetActiveSafe(playingPlayersContainer, false);
        SetActiveSafe(buildingUnitsContainer, true);
        SetActiveSafe(levelEnvironmentContainer, false);

        // Hide previously placed units
        if (SelectManager.Instance != null)
        {
            SelectManager.Instance.HidePlacedUnits();
        }

        // Enable selection phase player controllers and reset state
        if (player1SelectController != null)
        {
            Debug.Log($"Activating Player1SelectController - Active: {player1SelectController.gameObject.activeSelf}, Enabled: {player1SelectController.enabled}");
            player1SelectController.gameObject.SetActive(true);
            player1SelectController.enabled = true;
            player1SelectController.selectedUnit = null;
            player1SelectController.hasPlacedUnit = false;
            Debug.Log($"Player1SelectController Reset - Active: {player1SelectController.gameObject.activeSelf}, Enabled: {player1SelectController.enabled}");
        }
        else
        {
            Debug.LogError("player1SelectController is null!");
        }

        if (player2SelectController != null)
        {
            Debug.Log($"Activating Player2SelectController - Active: {player2SelectController.gameObject.activeSelf}, Enabled: {player2SelectController.enabled}");
            player2SelectController.gameObject.SetActive(true);
            player2SelectController.enabled = true;
            player2SelectController.selectedUnit = null;
            player2SelectController.hasPlacedUnit = false;
            Debug.Log($"Player2SelectController Reset - Active: {player2SelectController.gameObject.activeSelf}, Enabled: {player2SelectController.enabled}");
        }
        else
        {
            Debug.LogError("player2SelectController is null!");
        }

        // Reset completion records
        finishOrder.Clear();
        playerDead["Player1"] = false;
        playerDead["Player2"] = false;

        // Notify SelectManager to spawn new BuildingUnits
        if (SelectManager.Instance != null)
        {
            SelectManager.Instance.BeginSelectionPhase();
        }
        
        // Trigger selection phase started event
        OnSelectionPhaseStarted?.Invoke(currentRound, maxRounds);
    }

    /// <summary>Switch to Placement Phase</summary>
    public void SwitchToPlacementPhase()
    {
        currentPhase = GamePhase.Placement;
        Debug.Log("=== Placement Phase ===");

        // Show/hide phase containers
        SetActiveSafe(selectPlayersContainer, false);
        SetActiveSafe(buildingUnitsContainer, false);
        SetActiveSafe(placementPlayersContainer, true);
        SetActiveSafe(levelEnvironmentContainer, true);
        SetActiveSafe(playingPlayersContainer, false);

        // Disable selection phase player controllers
        if (player1SelectController != null)
        {
            player1SelectController.enabled = false;
            player1SelectController.gameObject.SetActive(false);
        }
        if (player2SelectController != null)
        {
            player2SelectController.enabled = false;
            player2SelectController.gameObject.SetActive(false);
        }

        // Show all previously placed units
        if (SelectManager.Instance != null)
        {
            SelectManager.Instance.EnsurePlacedUnitsVisible();
        }

        // Start PlaceManager placement phase
        if (placeManager != null)
        {
            placeManager.BeginPlacementPhase();
        }
        else if (PlaceManager.Instance != null)
        {
            PlaceManager.Instance.BeginPlacementPhase();
        }
    }

    /// <summary>Switch to Playing Phase</summary>
    public void SwitchToPlayingPhase()
    {
        currentPhase = GamePhase.Playing;
        Debug.Log("=== Playing Phase ===");

        SetActiveSafe(levelEnvironmentContainer, true);
        SetActiveSafe(selectPlayersContainer, false);
        SetActiveSafe(placementPlayersContainer, false);
        SetActiveSafe(playingPlayersContainer, true);
        SetActiveSafe(buildingUnitsContainer, false);

        // Show all placed units
        if (SelectManager.Instance != null)
        {
            SelectManager.Instance.EnsurePlacedUnitsVisible();
        }

        // Reset playing players positions
        ResetPlayingPlayersPositions();
        
        // Trigger deployment completed event
        OnDeploymentPhaseCompleted?.Invoke();

        Debug.Log("Playing players activated, game started!");
    }
    
    /// <summary>Player Finished Level</summary>
    public void PlayerFinished(string playerID)
    {
        // Check if already recorded
        if (finishOrder.Contains(playerID))
        {
            Debug.Log($"{playerID} already finished");
            return;
        }
        
        finishOrder.Add(playerID);
        Debug.Log($"{playerID} finished level, ranked #{finishOrder.Count}");
        
        // Assign score
        if (finishOrder.Count == 1)
        {
            // First place
            AddScore(playerID, firstPlaceScore);
        }
        else if (finishOrder.Count == 2)
        {
            // Second place
            AddScore(playerID, secondPlaceScore);
        }
        
        // Check if level ended
        if (IsLevelEnded())
        {
            Debug.Log("Level end condition met, preparing next round");
            StartCoroutine(PrepareNextLevel());
        }
    }
    
    /// <summary>Player Died</summary>
    public void PlayerDied(string playerID)
    {
        if (!playerDead[playerID])
        {
            playerDead[playerID] = true;
            Debug.Log($"{playerID} died, no score");
            
            // Check if all players have results
            if (IsLevelEnded())
            {
                Debug.Log("Level end condition met, preparing next round");
                StartCoroutine(PrepareNextLevel());
            }
        }
    }
    
    /// <summary>Check if Level Ended</summary>
    private bool IsLevelEnded()
    {
        int totalFinished = finishOrder.Count;
        int totalDead = 0;
        
        foreach (var kvp in playerDead)
        {
            if (kvp.Value && !finishOrder.Contains(kvp.Key))
            {
                totalDead++;
            }
        }
        
        bool isEnded = (totalFinished + totalDead) >= 2;
        
        Debug.Log($"Level status check: Finished={totalFinished}, Dead={totalDead}, Ended={isEnded}");
        
        // Level ends if finished + dead totals 2
        return isEnded;
    }
    
    /// <summary>Add Score</summary>
    private void AddScore(string playerID, int score)
    {
        if (playerID == "Player1")
        {
            player1Score += score;
            Debug.Log($"Player1 earned {score} points, total: {player1Score}");
        }
        else if (playerID == "Player2")
        {
            player2Score += score;
            Debug.Log($"Player2 earned {score} points, total: {player2Score}");
        }
        
        // Don't save scores to cache
        // SaveScores();
        UpdateScoreDisplay();
    }
    
    /// <summary>Update Score Display UI</summary>
    private void UpdateScoreDisplay()
    {
        if (player1ScoreText != null)
        {
            player1ScoreText.text = $"Player1: {player1Score}";
        }
        
        if (player2ScoreText != null)
        {
            player2ScoreText.text = $"Player2: {player2Score}";
        }
        
        if (targetScoreText != null)
        {
            targetScoreText.text = $"Target: {targetScore}";
        }
    }
    
    /// <summary>Prepare Next Level</summary>
    private IEnumerator PrepareNextLevel()
    {
        yield return new WaitForSeconds(3f);
        
        Debug.Log($"Current round: {currentRound}/{maxRounds}");
        Debug.Log($"Current scores - Player1: {player1Score}, Player2: {player2Score}");
        
        // Check game end conditions (either condition ends game)
        bool reachedTargetScore = player1Score >= targetScore || player2Score >= targetScore;
        bool reachedMaxRounds = currentRound >= maxRounds;
        
        if (reachedTargetScore || reachedMaxRounds)
        {
            // Game ended
            if (reachedTargetScore)
            {
                Debug.Log("Player reached target score, game ended!");
            }
            if (reachedMaxRounds)
            {
                Debug.Log("Max rounds reached, game ended!");
            }
            ShowVictoryScreen(reachedTargetScore, reachedMaxRounds);
        }
        else
        {
            // Continue to next round
            Debug.Log("Preparing next round...");
            
            if (SelectManager.Instance != null)
            {
                SelectManager.Instance.ResetSelections();
            }
            
            SwitchToSelectionPhase();
        }
    }
    
    /// <summary>Show Victory Screen</summary>
    private void ShowVictoryScreen(bool reachedTargetScore, bool reachedMaxRounds)
    {
        // Hide all game containers
        SetActiveSafe(selectPlayersContainer, false);
        SetActiveSafe(placementPlayersContainer, false);
        SetActiveSafe(playingPlayersContainer, false);
        SetActiveSafe(buildingUnitsContainer, false);
        SetActiveSafe(levelEnvironmentContainer, false);
        
        // Show victory UI
        if (victoryUI != null)
        {
            victoryUI.SetActive(true);
        }
        
        // Set victory text
        if (victoryText != null)
        {
            string winnerText = "";
            
            // Determine winner
            if (player1Score > player2Score)
            {
                winnerText = $"Player1 Wins!\n\n";
                
                // Add explanation based on end condition
                if (reachedTargetScore && player1Score >= targetScore)
                {
                    winnerText += $"Player1 reached target score first!\n\n";
                }
                else if (reachedMaxRounds)
                {
                    winnerText += $"Max rounds reached, Player1 has higher score!\n\n";
                }
            }
            else if (player2Score > player1Score)
            {
                winnerText = $"Player2 Wins!\n\n";
                
                // Add explanation based on end condition
                if (reachedTargetScore && player2Score >= targetScore)
                {
                    winnerText += $"Player2 reached target score first!\n\n";
                }
                else if (reachedMaxRounds)
                {
                    winnerText += $"Max rounds reached, Player2 has higher score!\n\n";
                }
            }
            else
            {
                winnerText = $"Draw!\n\n";
                
                if (reachedTargetScore)
                {
                    winnerText += $"Both reached target score!\n\n";
                }
                else if (reachedMaxRounds)
                {
                    winnerText += $"Max rounds reached, both have same score!\n\n";
                }
            }
            
            // Show end reasons
            string endReason = "";
            if (reachedTargetScore)
            {
                endReason += $"Target Score Reached ({targetScore} points)\n";
            }
            if (reachedMaxRounds)
            {
                endReason += $"Max Rounds Reached ({maxRounds} rounds)\n";
            }
            winnerText += endReason + "\n";
            
            winnerText += $"Final Scores:\n";
            winnerText += $"Player1: {player1Score} / {targetScore}\n";
            winnerText += $"Player2: {player2Score} / {targetScore}\n\n";
            winnerText += $"Total Rounds: {currentRound}\n\n";
            winnerText += $"Press Shift+1 to return to Main Menu";
            
            victoryText.text = winnerText;
        }
        
        Debug.Log("Game ended, showing victory screen");
    }
    
    /// <summary>Return to Main Menu</summary>
    public void ReturnToMainMenu()
    {
        Debug.Log("Returning to main menu...");
        
        // Reset scores (optional)
        // ResetScores();
        
        // Load scene 0
        SceneManager.LoadScene(0);
    }
    
    /// <summary>Restart Game</summary>
    public void RestartGame()
    {
        Debug.Log("Restarting game...");
        
        // Reset all states
        currentRound = 0;
        player1Score = 0;
        player2Score = 0;
        finishOrder.Clear();
        playerDead["Player1"] = false;
        playerDead["Player2"] = false;
        
        // Clear placed units
        if (SelectManager.Instance != null)
        {
            SelectManager.Instance.ClearAllPlacedUnits();
            SelectManager.Instance.ResetSelections();
        }
        
        // Update score display
        UpdateScoreDisplay();
        
        // Hide victory UI
        if (victoryUI != null)
        {
            victoryUI.SetActive(false);
        }
        
        // Start first round
        SwitchToSelectionPhase();
    }
    
    /// <summary>Save Scores</summary>
    private void SaveScores()
    {
        PlayerPrefs.SetInt("Player1Score", player1Score);
        PlayerPrefs.SetInt("Player2Score", player2Score);
        PlayerPrefs.Save();
    }
    
    /// <summary>Load Scores</summary>
    private void LoadScores()
    {
        if (PlayerPrefs.HasKey("Player1Score"))
        {
            player1Score = PlayerPrefs.GetInt("Player1Score");
        }
        if (PlayerPrefs.HasKey("Player2Score"))
        {
            player2Score = PlayerPrefs.GetInt("Player2Score");
        }
        
        Debug.Log($"Loaded scores - Player1: {player1Score}, Player2: {player2Score}");
        UpdateScoreDisplay();
    }
    
    /// <summary>Reset All Scores</summary>
    public void ResetScores()
    {
        player1Score = 0;
        player2Score = 0;
        // Don't save scores to cache
        // SaveScores();
        UpdateScoreDisplay();
        Debug.Log("Scores reset");
    }
}