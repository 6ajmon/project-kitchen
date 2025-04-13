using Godot;
using System;

public partial class Camera : Camera2D
{
    [Export] public float speed = 1000f; // Camera movement speed
    [Export] public float zoomSpeed = 0.1f; // How much to zoom per scroll
    [Export] public float minZoom = 0.2f; // Further zoom OUT (more of the world visible)
    [Export] public float maxZoom = 6.0f; // Closer zoom IN (objects appear bigger)

    public override void _Process(double delta)
    {
        // Camera movement
        Vector2 movement = Vector2.Zero;
        if (Input.IsActionPressed("ui_right")) movement.X += 1;
        if (Input.IsActionPressed("ui_left")) movement.X -= 1;
        if (Input.IsActionPressed("ui_down")) movement.Y += 1;
        if (Input.IsActionPressed("ui_up")) movement.Y -= 1;

        if (movement != Vector2.Zero)
        {
            movement = movement.Normalized() * speed * (float)delta;
            Position += movement;
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.WheelUp && mouseEvent.Pressed)
            {
                // Zoom OUT (see more)
                Zoom += new Vector2(zoomSpeed, zoomSpeed);
            }
            else if (mouseEvent.ButtonIndex == MouseButton.WheelDown && mouseEvent.Pressed)
            {
                // Zoom IN (see less, objects appear larger)
                Zoom -= new Vector2(zoomSpeed, zoomSpeed);
            }

            // Clamp Zoom to prevent extreme values
            Zoom = new Vector2(
                Mathf.Clamp(Zoom.X, minZoom, maxZoom),
                Mathf.Clamp(Zoom.Y, minZoom, maxZoom)
            );
        }
    }
}
