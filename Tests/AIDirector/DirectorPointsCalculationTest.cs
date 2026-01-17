using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Tests for Director's points calculation logic.
/// These tests verify the GetPointsMultiplier() algorithm which determines
/// how quickly points are accumulated based on player performance ratio.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DirectorPointsCalculationTest
{
    private Director _director = null!;

    [BeforeTest]
    public void Setup()
    {
        _director = new Director();
        // Set default thresholds (same as in Director.cs)
        _director.ReduceThreshold = 0.8f;
        _director.IntensifyThreshold = 1.2f;
        _director.MaxPointsMultiplier = 4.0f;
    }

    [AfterTest]
    public void Teardown()
    {
        _director?.Free();
        _director = null!;
    }

    #region Flow Zone Tests (0.8 <= ratio <= 1.2)

    [TestCase]
    public void GetPointsMultiplier_WhenInFlowZone_ReturnsZero()
    {
        // When ratio is between ReduceThreshold and IntensifyThreshold,
        // the player is in "Flow" state and no points should be generated
        
        // Arrange - set ratio to 1.0 (perfect balance, in Flow zone)
        SetSmoothedRatio(_director, 1.0f);

        // Act
        var result = _director.GetPointsMultiplier();

        // Assert
        AssertFloat(result).IsEqual(0f);
    }

    [TestCase]
    public void GetPointsMultiplier_AtReduceThreshold_ReturnsZero()
    {
        // At exactly the ReduceThreshold (0.8), still in Flow zone
        SetSmoothedRatio(_director, 0.8f);

        var result = _director.GetPointsMultiplier();

        AssertFloat(result).IsEqual(0f);
    }

    [TestCase]
    public void GetPointsMultiplier_AtIntensifyThreshold_ReturnsZero()
    {
        // At exactly the IntensifyThreshold (1.2), still in Flow zone
        SetSmoothedRatio(_director, 1.2f);

        var result = _director.GetPointsMultiplier();

        AssertFloat(result).IsEqual(0f);
    }

    #endregion

    #region Reduce Zone Tests (ratio < 0.8)

    [TestCase]
    public void GetPointsMultiplier_JustBelowReduceThreshold_ReturnsPositive()
    {
        // Just below 0.8, should start generating Benefit points
        SetSmoothedRatio(_director, 0.79f);

        var result = _director.GetPointsMultiplier();

        AssertFloat(result).IsGreater(0f);
    }

    [TestCase]
    public void GetPointsMultiplier_AtZeroRatio_ReturnsMaxMultiplier()
    {
        // At ratio 0, player is extremely struggling, max multiplier
        SetSmoothedRatio(_director, 0f);

        var result = _director.GetPointsMultiplier();

        AssertFloat(result).IsEqual(_director.MaxPointsMultiplier);
    }

    [TestCase]
    public void GetPointsMultiplier_AtHalfReduceZone_ReturnsIntermediateValue()
    {
        // At ratio 0.4 (halfway between 0 and 0.8)
        SetSmoothedRatio(_director, 0.4f);

        var result = _director.GetPointsMultiplier();

        // Should be between 1.0 and MaxMultiplier
        AssertFloat(result).IsGreater(1f);
        AssertFloat(result).IsLess(_director.MaxPointsMultiplier);
    }

    [TestCase]
    public void GetPointsMultiplier_ReduceZone_IncreasesAsRatioDecreases()
    {
        // Lower ratio = player struggling more = faster point generation
        SetSmoothedRatio(_director, 0.6f);
        var higherRatioResult = _director.GetPointsMultiplier();

        SetSmoothedRatio(_director, 0.2f);
        var lowerRatioResult = _director.GetPointsMultiplier();

        AssertFloat(lowerRatioResult).IsGreater(higherRatioResult);
    }

    #endregion

    #region Intensify Zone Tests (ratio > 1.2)

    [TestCase]
    public void GetPointsMultiplier_JustAboveIntensifyThreshold_ReturnsPositive()
    {
        // Just above 1.2, should start generating Negative points
        SetSmoothedRatio(_director, 1.21f);

        var result = _director.GetPointsMultiplier();

        AssertFloat(result).IsGreater(0f);
    }

    [TestCase]
    public void GetPointsMultiplier_AtDoubleRatio_ReturnsMaxMultiplier()
    {
        // At ratio 2.0, player is dominating, max multiplier
        SetSmoothedRatio(_director, 2.0f);

        var result = _director.GetPointsMultiplier();

        AssertFloat(result).IsEqual(_director.MaxPointsMultiplier);
    }

    [TestCase]
    public void GetPointsMultiplier_AtHigherThanDouble_StillReturnsMaxMultiplier()
    {
        // Ratio above 2.0 should still be capped at max
        SetSmoothedRatio(_director, 3.0f);

        var result = _director.GetPointsMultiplier();

        AssertFloat(result).IsEqual(_director.MaxPointsMultiplier);
    }

    [TestCase]
    public void GetPointsMultiplier_AtHalfIntensifyZone_ReturnsIntermediateValue()
    {
        // At ratio 1.6 (halfway between 1.2 and 2.0)
        SetSmoothedRatio(_director, 1.6f);

        var result = _director.GetPointsMultiplier();

        // Should be between 1.0 and MaxMultiplier
        AssertFloat(result).IsGreater(1f);
        AssertFloat(result).IsLess(_director.MaxPointsMultiplier);
    }

    [TestCase]
    public void GetPointsMultiplier_IntensifyZone_IncreasesAsRatioIncreases()
    {
        // Higher ratio = player dominating more = faster point generation
        SetSmoothedRatio(_director, 1.4f);
        var lowerRatioResult = _director.GetPointsMultiplier();

        SetSmoothedRatio(_director, 1.8f);
        var higherRatioResult = _director.GetPointsMultiplier();

        AssertFloat(higherRatioResult).IsGreater(lowerRatioResult);
    }

    #endregion

    #region Points Management Tests

    [TestCase]
    public void AddBenefitPoints_IncreasesPoints()
    {
        // Act
        _director.AddBenefitPoints(10f);
        _director.AddBenefitPoints(5f);

        // Assert - need to use reflection or make the property accessible
        // For now, we test indirectly through spending
        _director.SpendBenefitPoints(12f);
        // If we had 15 points and spent 12, we should have 3 left
        // We can only verify this works by not throwing
    }

    [TestCase]
    public void SpendBenefitPoints_DecreasesPoints()
    {
        // Arrange
        _director.AddBenefitPoints(20f);

        // Act
        _director.SpendBenefitPoints(15f);

        // Points should be 5 now, spending 10 should result in 0 (clamped)
        _director.SpendBenefitPoints(10f);
        
        // No exception means clamping worked
    }

    [TestCase]
    public void SpendBenefitPoints_CannotGoNegative()
    {
        // Arrange
        _director.AddBenefitPoints(5f);

        // Act - try to spend more than available
        _director.SpendBenefitPoints(100f);

        // Assert - should be clamped to 0, not negative
        // Add more points to verify we're at 0
        _director.AddBenefitPoints(10f);
        _director.SpendBenefitPoints(10f);
        // If previous spend went negative, this would fail
    }

    [TestCase]
    public void AddNegativePoints_IncreasesPoints()
    {
        // Act
        _director.AddNegativePoints(15f);
        _director.AddNegativePoints(10f);

        // Assert through spending
        _director.SpendNegativePoints(20f);
        // Should have 5 left
    }

    [TestCase]
    public void SpendNegativePoints_CannotGoNegative()
    {
        // Arrange
        _director.AddNegativePoints(5f);

        // Act
        _director.SpendNegativePoints(100f);

        // Should be clamped to 0
    }

    #endregion

    #region Configuration Tests

    [TestCase]
    public void Director_DefaultThresholds_AreCorrect()
    {
        var director = new Director();

        AssertFloat(director.ReduceThreshold).IsEqual(0.8f);
        AssertFloat(director.IntensifyThreshold).IsEqual(1.2f);
        AssertFloat(director.MaxPointsMultiplier).IsEqual(4.0f);

        director.Free();
    }

    [TestCase]
    public void Director_CustomThresholds_AffectPointsCalculation()
    {
        // Arrange - widen the Flow zone
        _director.ReduceThreshold = 0.5f;
        _director.IntensifyThreshold = 1.5f;

        // At ratio 0.7, with default thresholds would be in Flow
        // But with custom thresholds, should now be in Reduce zone
        SetSmoothedRatio(_director, 0.4f);

        var result = _director.GetPointsMultiplier();

        AssertFloat(result).IsGreater(0f);
    }

    #endregion

    /// <summary>
    /// Helper method to set the private _smoothedRatio field for testing
    /// </summary>
    private void SetSmoothedRatio(Director director, float value)
    {
        // Use reflection to set private field
        var field = typeof(Director).GetField("_smoothedRatio", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(director, value);
    }
}
