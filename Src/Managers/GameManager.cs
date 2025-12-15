using Godot;
using System;

public partial class GameManager : Node
{
    public static GameManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<GameManager>("GameManager");

    public Player Player { get; private set; }

    public void RegisterPlayer(Player player)
    {
        Player = player;
        GD.Print("[GameManager] Player registered.");
        
        // Also register with DataManager as it was doing before
        DataManager.Instance.RegisterPlayer(player);
    }
}
