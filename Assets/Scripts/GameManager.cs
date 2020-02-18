﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.SceneManagement;
using TMPro;

public enum GameState
{
    preRace,
    countdown,
    inRace,
    postRace
}

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;

    public static GameManager Instance { get { return _instance; } }

    public int numLaps = 3;

    public CinemachineVirtualCamera introCam;

    public GameObject[] playerCountdowns;

    [HideInInspector] public int numCheckpoints;
    public GameState gameState = GameState.preRace;
    public float preRaceDuration = 5.0f;
    public float raceCountdownDuration = 3.0f;
    public float postRaceDuration = 5.0f;
    [HideInInspector] public bool raceComplete = false;
    [HideInInspector] public int[] playerOrder = new int[2] { 0, 0 };
    [HideInInspector] public int[] playerStrokeCounters = new int[2] { 0, 0 };
    

    private DontDestroyScript dontDestroy;
    private List<Player> players = new List<Player>();
    private const int positionModifier = 5;
    private int playersFinished = 0;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this.gameObject); }
        else { _instance = this; }

        numCheckpoints = FindObjectsOfType<LapCheckpoint>().Length;

        dontDestroy = FindObjectOfType<DontDestroyScript>();
    }

    private void Start()
    {
        Player[] allPlayers = GameObject.FindObjectsOfType<Player>();
        for (int i = 0; i < allPlayers.Length; i++)
        {
            players.Add(allPlayers[i]);
        }

        SetPlayersInputControl(false);

        StartCoroutine(StartPreRace());
    }

    private void SetPlayersInputControl(bool canInput)
    {
        foreach (Player player in players)
        {
            player.SetInputControl(canInput);
        }
    }

    private IEnumerator StartPreRace()
    {
        gameState = GameState.preRace;

        yield return new WaitForSeconds(preRaceDuration);

        introCam.Priority = 9;

        StartCoroutine(StartCountdown());
    }

    private IEnumerator StartCountdown()
    {
        gameState = GameState.countdown;

        for (int i = 0; i < playerCountdowns.Length; i++)
        {
            playerCountdowns[i].SetActive(true);
        }

        yield return new WaitForSeconds(raceCountdownDuration);

        

        StartCoroutine(StartRace());
    }

    private IEnumerator StartRace()
    {
        gameState = GameState.inRace;

        SetPlayersInputControl(true);

        yield return new WaitForSeconds(1.0f);

        for (int i = 0; i < playerCountdowns.Length; i++)
        {
            playerCountdowns[i].SetActive(false);
        }

        while (raceComplete == false)
        {
            yield return null;
        }

        StartCoroutine(EndRace());
    }

    private IEnumerator EndRace()
    {
        // Single player
        if (GameObject.Find("DontDestroyObj").GetComponent<DontDestroyScript>().playerCount == 1)
        {
            GameObject.FindGameObjectWithTag("WinnerText").GetComponent<TextMeshProUGUI>().text = "Race end : " + playerStrokeCounters[0] + " Strokes";
        }
        // Mulitplayer
        else
        {
            // Activate leaderboard
            ActivateLeaderboard();
        }

        playerStrokeCounters = new int[] { 0, 0 };

        gameState = GameState.postRace;

        SetPlayersInputControl(false);

        yield return new WaitForSeconds(postRaceDuration);

        RestartScene();
    }

    private void ActivateLeaderboard()
    {
        GameObject leaderboard = GameObject.FindGameObjectWithTag("Leaderboard");
        DontDestroyScript dontDestroy = GameObject.Find("DontDestroyObj").GetComponent<DontDestroyScript>();

        // Assign scores
        int[] scores = new int[2] { 0, 0 };
        for (int i = 0; i < dontDestroy.playerCount; i++)
        {
            scores[i] = (positionModifier * playerOrder[i]) + playerStrokeCounters[i];
        }

        // Put them in order - jank
        if (scores[0] == scores[1])
        {
            // Put in order of place
            leaderboard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "1) Player " + playerOrder[0] + " " + scores[0] + " pts";
            leaderboard.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = "2) Player " + playerOrder[1] + " " + scores[1] + " pts";
        }
        else if (scores[0] < scores[1])
        {
            // Player 1 first
            leaderboard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "1) Player 1 " + scores[0] + " pts";
            leaderboard.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = "2) Player 2 " + scores[1] + " pts";
        }
        else
        {
            leaderboard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = "1) Player 2 " + scores[1] + " pts";
            leaderboard.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = "2) Player 1 " + scores[1] + " pts";
        }
    }

    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void PlayerFinished(int playerNumber)
    {
        if (dontDestroy.playerCount == 1) raceComplete = true;

        playerOrder[playerNumber - 1] = ++playersFinished;

        if (playersFinished == dontDestroy.maxNumPlayers) raceComplete = true;
    }
}
