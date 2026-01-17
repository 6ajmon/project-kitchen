using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Tests for the TimeBuffer functionality used in PerformanceData.
/// Since TimeBuffer is a private class, we test it indirectly through PerformanceData.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class TimeBufferTest
{
    private PerformanceData _performanceData = null!;

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

    #region Buffer Sum Tests

    [TestCase]
    public void TimeBuffer_EmptyBuffer_ReturnsZero()
    {
        // No data registered, should return 0
        var result = _performanceData.KillRate;
        
        AssertFloat(result).IsEqual(0f);
    }

    [TestCase]
    public void TimeBuffer_SingleEntry_ReturnsValue()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterEnemyKill(1.0f);

        // Act
        var result = _performanceData.KillRate;

        // Assert
        AssertFloat(result).IsEqual(1f);
    }

    [TestCase]
    public void TimeBuffer_MultipleEntries_ReturnsSumWithinWindow()
    {
        // Arrange - add entries within window
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterEnemyKill(1.0f);
        
        _performanceData.TotalSessionTime = 5.0;
        _performanceData.RegisterEnemyKill(1.0f);
        
        _performanceData.TotalSessionTime = 10.0;
        _performanceData.RegisterEnemyKill(1.0f);

        // Act - check sum while all entries are still in window (15s)
        _performanceData.TotalSessionTime = 12.0;
        var result = _performanceData.KillRate;

        // Assert
        AssertFloat(result).IsEqual(3f);
    }

    #endregion

    #region Buffer Expiration Tests

    [TestCase]
    public void TimeBuffer_OldEntriesExpire_ReturnsOnlyRecent()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterEnemyKill(1.0f); // Will expire

        _performanceData.TotalSessionTime = 20.0;
        _performanceData.RegisterEnemyKill(1.0f); // Still valid

        // Act - first entry should have expired (>15s ago)
        _performanceData.TotalSessionTime = 25.0;
        var result = _performanceData.KillRate;

        // Assert - only the recent kill counts
        AssertFloat(result).IsEqual(1f);
    }

    [TestCase]
    public void TimeBuffer_AllEntriesExpired_ReturnsZero()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterEnemyKill(1.0f);
        _performanceData.RegisterEnemyKill(1.0f);
        _performanceData.RegisterEnemyKill(1.0f);

        // Act - move far into the future
        _performanceData.TotalSessionTime = 100.0;
        var result = _performanceData.KillRate;

        // Assert
        AssertFloat(result).IsEqual(0f);
    }

    [TestCase]
    public void TimeBuffer_PartialExpiration_ReturnsCorrectSum()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterEnemyKill(1.0f); // Expires at t=15

        _performanceData.TotalSessionTime = 10.0;
        _performanceData.RegisterEnemyKill(1.0f); // Expires at t=25

        _performanceData.TotalSessionTime = 20.0;
        _performanceData.RegisterEnemyKill(1.0f); // Expires at t=35

        // Act - at t=22, first should be expired, other two valid
        _performanceData.TotalSessionTime = 22.0;
        var result = _performanceData.KillRate;

        // Assert
        AssertFloat(result).IsEqual(2f);
    }

    #endregion

    #region Different Window Sizes Tests

    [TestCase]
    public void DamageBuffer_Uses15SecondWindow()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterDamageTaken(10f);

        // Act - check at 14 seconds (still in window)
        _performanceData.TotalSessionTime = 14.0;
        var resultInWindow = _performanceData.DamageTakenRate;

        // Move past window
        _performanceData.TotalSessionTime = 16.0;
        var resultExpired = _performanceData.DamageTakenRate;

        // Assert
        AssertFloat(resultInWindow).IsEqual(10f);
        AssertFloat(resultExpired).IsEqual(0f);
    }

    [TestCase]
    public void HPDropBuffer_Uses5SecondWindow()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f);
        
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(80f); // 20 HP drop

        // Act - check at 4 seconds (still in window)
        _performanceData.TotalSessionTime = 4.0;
        var resultInWindow = _performanceData.RecentHPDrop;

        // Move past 5s window
        _performanceData.TotalSessionTime = 7.0;
        var resultExpired = _performanceData.RecentHPDrop;

        // Assert
        AssertFloat(resultInWindow).IsEqual(20f);
        AssertFloat(resultExpired).IsEqual(0f);
    }

    [TestCase]
    public void RecentDamageBuffer_Uses6SecondWindow()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterDamageTaken(15f);

        // Act - check at 5 seconds (still in window)
        _performanceData.TotalSessionTime = 5.0;
        var resultInWindow = _performanceData.RecentDamageTaken;

        // Move past 6s window
        _performanceData.TotalSessionTime = 7.0;
        var resultExpired = _performanceData.RecentDamageTaken;

        // Assert
        AssertFloat(resultInWindow).IsEqual(15f);
        AssertFloat(resultExpired).IsEqual(0f);
    }

    #endregion

    #region Edge Cases

    [TestCase]
    public void TimeBuffer_NegativeTime_DoesNotCrash()
    {
        // This shouldn't happen in practice but let's be defensive
        _performanceData.TotalSessionTime = -5.0;
        _performanceData.RegisterEnemyKill(1.0f);

        _performanceData.TotalSessionTime = 0.0;
        var result = _performanceData.KillRate;

        // Should handle this gracefully
        AssertFloat(result).IsGreaterEqual(0f);
    }

    [TestCase]
    public void TimeBuffer_ZeroValue_TrackedCorrectly()
    {
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterDamageTaken(0f);
        _performanceData.RegisterDamageTaken(10f);

        var result = _performanceData.DamageTakenRate;

        AssertFloat(result).IsEqual(10f);
    }

    [TestCase]
    public void TimeBuffer_VeryLargeValues_HandledCorrectly()
    {
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterDamageTaken(999999f);

        var result = _performanceData.DamageTakenRate;

        AssertFloat(result).IsEqual(999999f);
    }

    #endregion
}
