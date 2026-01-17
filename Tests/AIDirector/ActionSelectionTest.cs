using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System.Collections.Generic;
using System.Linq;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Testable action for verifying weighted selection
/// </summary>
public partial class WeightedTestAction : DirectorAction
{
    public int ExecutionCount { get; private set; } = 0;
    
    protected override void OnExecute()
    {
        ExecutionCount++;
    }

    public void ResetCount() => ExecutionCount = 0;
}

/// <summary>
/// Tests for Director's weighted action selection algorithm.
/// The Director uses weighted random selection to choose which action to execute.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class ActionSelectionTest
{
    private List<WeightedTestAction> _actions = null!;

    [BeforeTest]
    public void Setup()
    {
        _actions = new List<WeightedTestAction>();
    }

    [AfterTest]
    public void Teardown()
    {
        foreach (var action in _actions)
        {
            action?.Free();
        }
        _actions.Clear();
        _actions = null!;
    }

    private WeightedTestAction CreateAction(ActionType type, float cost, float weight, float cooldown = 0f)
    {
        var action = new WeightedTestAction();
        action.ActionType = type;
        action.Cost = cost;
        action.Weight = weight;
        action.Cooldown = cooldown;
        _actions.Add(action);
        return action;
    }

    #region Candidate Filtering Tests

    [TestCase]
    public void ActionSelection_FiltersOutWrongType()
    {
        // Arrange
        var benefitAction = CreateAction(ActionType.Benefit, 10f, 1f);
        var negativeAction = CreateAction(ActionType.Negative, 10f, 1f);

        float availablePoints = 50f;
        double currentTime = 0.0;

        // Act - filter for Benefit type
        var candidates = _actions
            .Where(a => a.ActionType == ActionType.Benefit && a.CanExecute(availablePoints, currentTime))
            .ToList();

        // Assert
        AssertInt(candidates.Count).IsEqual(1);
        AssertObject(candidates[0]).IsSame(benefitAction);
    }

    [TestCase]
    public void ActionSelection_FiltersOutExpensiveActions()
    {
        // Arrange
        var cheapAction = CreateAction(ActionType.Negative, 5f, 1f);
        var expensiveAction = CreateAction(ActionType.Negative, 50f, 1f);

        float availablePoints = 20f;
        double currentTime = 0.0;

        // Act
        var candidates = _actions
            .Where(a => a.ActionType == ActionType.Negative && a.CanExecute(availablePoints, currentTime))
            .ToList();

        // Assert
        AssertInt(candidates.Count).IsEqual(1);
        AssertObject(candidates[0]).IsSame(cheapAction);
    }

    [TestCase]
    public void ActionSelection_FiltersOutActionsOnCooldown()
    {
        // Arrange
        var readyAction = CreateAction(ActionType.Negative, 10f, 1f, cooldown: 5f);
        var cooldownAction = CreateAction(ActionType.Negative, 10f, 1f, cooldown: 10f);
        
        // Put cooldownAction on cooldown
        cooldownAction.Execute(0.0);

        float availablePoints = 50f;
        double currentTime = 5.0; // 5 seconds later

        // Act
        var candidates = _actions
            .Where(a => a.ActionType == ActionType.Negative && a.CanExecute(availablePoints, currentTime))
            .ToList();

        // Assert - cooldownAction still has 5 more seconds of cooldown
        AssertInt(candidates.Count).IsEqual(1);
        AssertObject(candidates[0]).IsSame(readyAction);
    }

    [TestCase]
    public void ActionSelection_ReturnsEmptyWhenNoCandidates()
    {
        // Arrange - all actions too expensive
        CreateAction(ActionType.Negative, 100f, 1f);
        CreateAction(ActionType.Negative, 200f, 1f);

        float availablePoints = 50f;
        double currentTime = 0.0;

        // Act
        var candidates = _actions
            .Where(a => a.ActionType == ActionType.Negative && a.CanExecute(availablePoints, currentTime))
            .ToList();

        // Assert
        AssertInt(candidates.Count).IsEqual(0);
    }

    #endregion

    #region Weight Calculation Tests

    [TestCase]
    public void ActionSelection_TotalWeightCalculatedCorrectly()
    {
        // Arrange
        CreateAction(ActionType.Negative, 10f, 1.0f);
        CreateAction(ActionType.Negative, 10f, 2.0f);
        CreateAction(ActionType.Negative, 10f, 3.0f);

        float availablePoints = 50f;
        double currentTime = 0.0;

        // Act
        var candidates = _actions
            .Where(a => a.ActionType == ActionType.Negative && a.CanExecute(availablePoints, currentTime))
            .ToList();
        float totalWeight = candidates.Sum(a => a.Weight);

        // Assert
        AssertFloat(totalWeight).IsEqual(6.0f);
    }

    [TestCase]
    public void ActionSelection_ZeroWeightActionNeverSelected()
    {
        // Arrange
        var zeroWeight = CreateAction(ActionType.Negative, 10f, 0f);
        var normalWeight = CreateAction(ActionType.Negative, 10f, 1f);

        // Act - simulate many selections
        int zeroWeightCount = 0;
        int normalWeightCount = 0;

        for (int i = 0; i < 100; i++)
        {
            float totalWeight = _actions.Sum(a => a.Weight);
            float randomValue = GD.Randf() * totalWeight;
            float currentSum = 0;

            foreach (var action in _actions)
            {
                currentSum += action.Weight;
                if (randomValue <= currentSum)
                {
                    if (action == zeroWeight) zeroWeightCount++;
                    else if (action == normalWeight) normalWeightCount++;
                    break;
                }
            }
        }

        // Assert - zero weight should never be selected
        AssertInt(zeroWeightCount).IsEqual(0);
        AssertInt(normalWeightCount).IsEqual(100);
    }

    #endregion

    #region Statistical Distribution Tests

    [TestCase]
    public void ActionSelection_HigherWeightSelectedMoreOften()
    {
        // Arrange - one action has 3x the weight
        var lowWeight = CreateAction(ActionType.Negative, 10f, 1.0f);
        var highWeight = CreateAction(ActionType.Negative, 10f, 3.0f);

        // Act - simulate many selections
        int lowWeightCount = 0;
        int highWeightCount = 0;
        int iterations = 1000;

        for (int i = 0; i < iterations; i++)
        {
            float totalWeight = _actions.Sum(a => a.Weight);
            float randomValue = GD.Randf() * totalWeight;
            float currentSum = 0;

            foreach (var action in _actions)
            {
                currentSum += action.Weight;
                if (randomValue <= currentSum)
                {
                    if (action == lowWeight) lowWeightCount++;
                    else if (action == highWeight) highWeightCount++;
                    break;
                }
            }
        }

        // Assert - high weight should be selected roughly 3x as often
        // Allow for statistical variance (between 2x and 4x)
        float ratio = (float)highWeightCount / lowWeightCount;
        AssertFloat(ratio).IsBetween(2.0f, 4.0f);
    }

    [TestCase]
    public void ActionSelection_EqualWeights_RoughlyEqualSelection()
    {
        // Arrange - three actions with equal weight
        var action1 = CreateAction(ActionType.Negative, 10f, 1.0f);
        var action2 = CreateAction(ActionType.Negative, 10f, 1.0f);
        var action3 = CreateAction(ActionType.Negative, 10f, 1.0f);

        // Act
        int count1 = 0, count2 = 0, count3 = 0;
        int iterations = 900;

        for (int i = 0; i < iterations; i++)
        {
            float totalWeight = _actions.Sum(a => a.Weight);
            float randomValue = GD.Randf() * totalWeight;
            float currentSum = 0;

            foreach (var action in _actions)
            {
                currentSum += action.Weight;
                if (randomValue <= currentSum)
                {
                    if (action == action1) count1++;
                    else if (action == action2) count2++;
                    else if (action == action3) count3++;
                    break;
                }
            }
        }

        // Assert - each should be selected roughly 1/3 of the time
        // Allow for variance (between 200 and 400 out of 900)
        int expectedPer = iterations / 3; // 300
        int variance = 100;

        AssertInt(count1).IsBetween(expectedPer - variance, expectedPer + variance);
        AssertInt(count2).IsBetween(expectedPer - variance, expectedPer + variance);
        AssertInt(count3).IsBetween(expectedPer - variance, expectedPer + variance);
    }

    #endregion
}
