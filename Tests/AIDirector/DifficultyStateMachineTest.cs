using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System.Collections.Generic;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Tests for DifficultyStateMachine state transitions and behavior
/// </summary>
[TestSuite]
public partial class DifficultyStateMachineTest
{
    // Note: Full integration tests of DifficultyStateMachine require scene setup
    // These tests focus on the logic that can be tested in isolation

    #region State Enum Tests

    [TestCase]
    public void State_HasThreeStates()
    {
        // Assert that all expected states exist
        var states = System.Enum.GetValues<DifficultyStateMachine.State>();
        
        AssertInt(states.Length).IsEqual(3);
    }

    [TestCase]
    public void State_ContainsMaintain()
    {
        var state = DifficultyStateMachine.State.Maintain;
        AssertString(state.ToString()).IsEqual("Maintain");
    }

    [TestCase]
    public void State_ContainsReduce()
    {
        var state = DifficultyStateMachine.State.Reduce;
        AssertString(state.ToString()).IsEqual("Reduce");
    }

    [TestCase]
    public void State_ContainsIntensify()
    {
        var state = DifficultyStateMachine.State.Intensify;
        AssertString(state.ToString()).IsEqual("Intensify");
    }

    #endregion
}
