using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Test implementation of DirectorAction for testing purposes
/// </summary>
public partial class TestDirectorAction : DirectorAction
{
    public int ExecuteCount { get; private set; } = 0;
    public double LastExecuteTime { get; private set; } = -1;

    protected override void OnExecute()
    {
        ExecuteCount++;
    }
}

[TestSuite]
[RequireGodotRuntime]
public partial class DirectorActionTest
{
    private TestDirectorAction _action = null!;

    [BeforeTest]
    public void Setup()
    {
        _action = new TestDirectorAction();
        _action.Cost = 10f;
        _action.Cooldown = 5f;
        _action.Weight = 1f;
    }

    [AfterTest]
    public void Teardown()
    {
        _action?.Free();
        _action = null!;
    }

    #region CanExecute Tests

    [TestCase]
    public void CanExecute_WhenEnoughPoints_ReturnsTrue()
    {
        // Arrange
        float availablePoints = 20f;
        double currentTime = 10.0;

        // Act
        var result = _action.CanExecute(availablePoints, currentTime);

        // Assert
        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void CanExecute_WhenNotEnoughPoints_ReturnsFalse()
    {
        // Arrange
        float availablePoints = 5f; // Less than Cost (10)
        double currentTime = 10.0;

        // Act
        var result = _action.CanExecute(availablePoints, currentTime);

        // Assert
        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void CanExecute_WhenExactlyEnoughPoints_ReturnsTrue()
    {
        // Arrange
        float availablePoints = 10f; // Exactly Cost
        double currentTime = 10.0;

        // Act
        var result = _action.CanExecute(availablePoints, currentTime);

        // Assert
        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void CanExecute_WhenOnCooldown_ReturnsFalse()
    {
        // Arrange
        _action.Execute(10.0); // Execute at time 10
        float availablePoints = 20f;
        double currentTime = 12.0; // Only 2 seconds later (cooldown is 5)

        // Act
        var result = _action.CanExecute(availablePoints, currentTime);

        // Assert
        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void CanExecute_WhenCooldownExpired_ReturnsTrue()
    {
        // Arrange
        _action.Execute(10.0); // Execute at time 10
        float availablePoints = 20f;
        double currentTime = 20.0; // 10 seconds later (cooldown is 5)

        // Act
        var result = _action.CanExecute(availablePoints, currentTime);

        // Assert
        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void CanExecute_WhenCooldownExactlyExpired_ReturnsTrue()
    {
        // Arrange
        _action.Execute(10.0); // Execute at time 10
        float availablePoints = 20f;
        double currentTime = 15.0; // Exactly 5 seconds later

        // Act
        var result = _action.CanExecute(availablePoints, currentTime);

        // Assert - at exactly cooldown boundary should be able to execute
        AssertBool(result).IsTrue();
    }

    #endregion

    #region Execute Tests

    [TestCase]
    public void Execute_CallsOnExecute()
    {
        // Act
        _action.Execute(10.0);

        // Assert
        AssertInt(_action.ExecuteCount).IsEqual(1);
    }

    [TestCase]
    public void Execute_CalledMultipleTimes_IncrementsExecuteCount()
    {
        // Act
        _action.Execute(0.0);
        _action.Execute(10.0);
        _action.Execute(20.0);

        // Assert
        AssertInt(_action.ExecuteCount).IsEqual(3);
    }

    #endregion

    #region ActionType Tests

    [TestCase]
    public void ActionType_DefaultsToNegative()
    {
        // Arrange
        var action = new TestDirectorAction();

        // Assert
        AssertObject(action.ActionType).IsEqual(ActionType.Negative);

        // Cleanup
        action.Free();
    }

    [TestCase]
    public void ActionType_CanBeSetToBenefit()
    {
        // Arrange
        _action.ActionType = ActionType.Benefit;

        // Assert
        AssertObject(_action.ActionType).IsEqual(ActionType.Benefit);
    }

    #endregion

    #region Cost and Weight Tests

    [TestCase]
    public void Cost_CanBeModified()
    {
        // Act
        _action.Cost = 25f;

        // Assert
        AssertFloat(_action.Cost).IsEqual(25f);
    }

    [TestCase]
    public void Weight_CanBeModified()
    {
        // Act
        _action.Weight = 5f;

        // Assert
        AssertFloat(_action.Weight).IsEqual(5f);
    }

    #endregion
}
