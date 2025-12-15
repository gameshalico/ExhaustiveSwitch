using System;
using UnityEngine;

public class GameStateProcessor
{
    public void ProcessGameState(GameState state)
    {
        switch (state)
        {
            case GameState.MainMenu:
                Debug.Log("Main Menu");
                break;
            case GameState.Playing:
                Debug.Log("Playing");
                break;
            case GameState.Paused:
                Debug.Log("Paused");
                break;
            case GameState.GameOver:
                Debug.Log("Game Over");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state));
        }
    }

    public string GetStateMessage(GameState state)
    {
        return state switch
        {
            GameState.MainMenu => "Welcome to the game!",
            GameState.Playing => "Game in progress",
            GameState.Paused => "Game paused",
            GameState.GameOver => "Game over. Try again?",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
    }
}
