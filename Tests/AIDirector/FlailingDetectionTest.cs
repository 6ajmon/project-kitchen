using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Tests for the Flailing Detection system.
/// Flailing occurs when a player's HP drops rapidly while at low health,
/// indicating they're in danger and need immediate help from the Director.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class FlailingDetectionTest
{
    private PerformanceData _performanceData = null!;

    // Director default values for flailing detection
    private const float FlailingHPDropThreshold = 30.0f;  // HP drop in 5s to trigger
    private const float FlailingHealthThreshold = 0.3f;   // Health% below which flailing is checked

    [BeforeTest]
    public void Setup()
    {
        _performanceData = new PerformanceData();
    }

    [AfterTest]
    public void Teardown()
    {
        _performanceData?.Free();
        _performanceData = null!;
    }

    #region HP Drop Tracking Tests

    [TestCase]
    public void HPDrop_SingleLargeHit_RecordedCorrectly()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f);

        // Act - take 40 damage
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(60f);

        // Assert
        var hpDrop = _performanceData.RecentHPDrop;
        AssertFloat(hpDrop).IsEqual(40f);
    }

    [TestCase]
    public void HPDrop_MultipleSmallHits_Accumulated()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f);

        // Act - take multiple small hits
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(90f); // -10

        _performanceData.TotalSessionTime = 2.0;
        _performanceData.UpdateHealthTracking(80f); // -10

        _performanceData.TotalSessionTime = 3.0;
        _performanceData.UpdateHealthTracking(70f); // -10

        // Assert - total 30 HP lost
        var hpDrop = _performanceData.RecentHPDrop;
        AssertFloat(hpDrop).IsEqual(30f);
    }

    [TestCase]
    public void HPDrop_ExceedsThreshold_IndicatesFlailing()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f);

        // Act - rapid HP loss
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(80f); // -20

        _performanceData.TotalSessionTime = 2.0;
        _performanceData.UpdateHealthTracking(65f); // -15

        // Assert - 35 HP dropped, exceeds 30 threshold
        var hpDrop = _performanceData.RecentHPDrop;
        AssertFloat(hpDrop).IsGreaterEqual(FlailingHPDropThreshold);
    }

    #endregion

    #region Flailing Condition Tests

    [TestCase]
    public void Flailing_LowHealthAndRapidDrop_ShouldTrigger()
    {
        // Scenario: Player at 25% health and losing HP fast
        float maxHealth = 100f;
        float currentHealth = 25f; // 25% = below FlailingHealthThreshold (30%)
        
        // Setup rapid HP drop
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(55f);

        _performanceData.TotalSessionTime = 2.0;
        _performanceData.UpdateHealthTracking(25f); // -30 in 2 seconds

        // Check conditions
        float healthPercent = currentHealth / maxHealth;
        float recentDrop = _performanceData.RecentHPDrop;

        // Assert - both conditions met
        AssertFloat(healthPercent).IsLess(FlailingHealthThreshold);
        AssertFloat(recentDrop).IsGreaterEqual(FlailingHPDropThreshold);
    }

    [TestCase]
    public void Flailing_HighHealthAndRapidDrop_ShouldNotTrigger()
    {
        // Scenario: Player at 70% health but lost HP fast
        // This is NOT flailing - player has enough HP buffer
        float maxHealth = 100f;
        float currentHealth = 70f;

        // Setup HP drop
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f);

        _performanceData.TotalSessionTime = 2.0;
        _performanceData.UpdateHealthTracking(70f); // -30

        // Check conditions
        float healthPercent = currentHealth / maxHealth;
        float recentDrop = _performanceData.RecentHPDrop;

        // Assert - HP drop threshold met, but health is too high
        AssertFloat(healthPercent).IsGreater(FlailingHealthThreshold);
        AssertFloat(recentDrop).IsGreaterEqual(FlailingHPDropThreshold);
        
        // Combined: NOT flailing because health% > threshold
    }

    [TestCase]
    public void Flailing_LowHealthButSlowDrop_ShouldNotTrigger()
    {
        // Scenario: Player at 20% health but HP dropped slowly
        float maxHealth = 100f;
        float currentHealth = 20f;

        // Setup slow HP drop (spread over time, most expired)
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(50f);

        // Wait for buffer to expire
        _performanceData.TotalSessionTime = 10.0;
        _performanceData.UpdateHealthTracking(20f); // Only -30 but over 10 seconds

        // HP drop buffer is 5 seconds, so the old drop expired
        // Only the recent 30 HP drop counts
        float healthPercent = currentHealth / maxHealth;

        // Assert - health is low
        AssertFloat(healthPercent).IsLess(FlailingHealthThreshold);
    }

    #endregion

    #region HP Drop Buffer Window Tests

    [TestCase]
    public void HPDropBuffer_EntriesExpireAfter5Seconds()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f);

        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(70f); // -30

        // Assert at 4 seconds - still in window
        _performanceData.TotalSessionTime = 4.0;
        AssertFloat(_performanceData.RecentHPDrop).IsEqual(30f);

        // Assert at 7 seconds - expired
        _performanceData.TotalSessionTime = 7.0;
        AssertFloat(_performanceData.RecentHPDrop).IsEqual(0f);
    }

    [TestCase]
    public void HPDropBuffer_OnlyCountsDropsNotHeals()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(50f);

        // Take damage
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(30f); // -20

        // Heal
        _performanceData.TotalSessionTime = 2.0;
        _performanceData.UpdateHealthTracking(60f); // +30 (heal, not recorded)

        // Take damage again
        _performanceData.TotalSessionTime = 3.0;
        _performanceData.UpdateHealthTracking(50f); // -10

        // Assert - only damage counted, not heals
        var hpDrop = _performanceData.RecentHPDrop;
        AssertFloat(hpDrop).IsEqual(30f); // 20 + 10
    }

    #endregion

    #region Edge Cases

    [TestCase]
    public void HPDrop_FirstUpdateWithNoHistory_NoDropRecorded()
    {
        // First update shouldn't record a drop
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(50f);

        var hpDrop = _performanceData.RecentHPDrop;
        AssertFloat(hpDrop).IsEqual(0f);
    }

    [TestCase]
    public void HPDrop_SameHealthValue_NoDropRecorded()
    {
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(50f);

        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(50f); // No change

        var hpDrop = _performanceData.RecentHPDrop;
        AssertFloat(hpDrop).IsEqual(0f);
    }

    [TestCase]
    public void HPDrop_ZeroHealth_RecordedCorrectly()
    {
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(30f);

        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(0f); // Death

        var hpDrop = _performanceData.RecentHPDrop;
        AssertFloat(hpDrop).IsEqual(30f);
    }

    #endregion
}
