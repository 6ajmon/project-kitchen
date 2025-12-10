using Godot;
using System;

public partial class PlayerData : Node
{
    public float CurrentHealth { get; set; }
    public float MaxHealth { get; set; }
    public float DPS { get; set; }
    public float MovementSpeed { get; set; }
}