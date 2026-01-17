using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

[TestSuite]
[RequireGodotRuntime]
public partial class PerformanceDataTest
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

    #region Accuracy Tests

    [TestCase]
    public void AverageAccuracy_WhenNoShotsFired_ReturnsZero()
    {
        // Arrange - no shots fired

        // Act
        var result = _performanceData.AverageAccuracy;

        // Assert
        AssertFloat(result).IsEqual(0f);
    }

    [TestCase]
    public void AverageAccuracy_WhenAllShotsHit_ReturnsOne()
    {
        // Arrange
        _performanceData.ShotsFired = 10;
        _performanceData.ShotsHit = 10;

        // Act
        var result = _performanceData.AverageAccuracy;

        // Assert
        AssertFloat(result).IsEqual(1f);
    }

    [TestCase]
    public void AverageAccuracy_WhenHalfShotsHit_ReturnsPointFive()
    {
        // Arrange
        _performanceData.ShotsFired = 10;
        _performanceData.ShotsHit = 5;

        // Act
        var result = _performanceData.AverageAccuracy;

        // Assert
        AssertFloat(result).IsEqual(0.5f);
    }

    [TestCase]
    public void RegisterShotFired_IncreasesShotsFiredCount()
    {
        // Act
        _performanceData.RegisterShotFired();
        _performanceData.RegisterShotFired();
        _performanceData.RegisterShotFired();

        // Assert
        AssertInt(_performanceData.ShotsFired).IsEqual(3);
    }

    [TestCase]
    public void RegisterShotHit_IncreasesShotsHitCount()
    {
        // Act
        _performanceData.RegisterShotHit();
        _performanceData.RegisterShotHit();

        // Assert
        AssertInt(_performanceData.ShotsHit).IsEqual(2);
    }

    #endregion

    #region Damage Tracking Tests

    [TestCase]
    public void RegisterDamageTaken_IncreasesTotalDamage()
    {
        // Act
        _performanceData.RegisterDamageTaken(25f);
        _performanceData.RegisterDamageTaken(15f);

        // Assert
        AssertFloat(_performanceData.TotalDamageTaken).IsEqual(40f);
    }

    [TestCase]
    public void DamageTakenRate_ReturnsRecentDamage()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterDamageTaken(10f);
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.RegisterDamageTaken(20f);

        // Act - still within 15s window
        _performanceData.TotalSessionTime = 5.0;
        var result = _performanceData.DamageTakenRate;

        // Assert
        AssertFloat(result).IsEqual(30f);
    }

    [TestCase]
    public void DamageTakenRate_ExpiresOldDamage()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterDamageTaken(50f);

        // Act - move time past 15s window
        _performanceData.TotalSessionTime = 20.0;
        var result = _performanceData.DamageTakenRate;

        // Assert - old damage should have expired
        AssertFloat(result).IsEqual(0f);
    }

    #endregion

    #region Kill Tracking Tests

    [TestCase]
    public void RegisterEnemyKill_IncreasesTotalKills()
    {
        // Act
        _performanceData.RegisterEnemyKill(1.0f);
        _performanceData.RegisterEnemyKill(1.0f);
        _performanceData.RegisterEnemyKill(1.0f);

        // Assert
        AssertInt(_performanceData.TotalKills).IsEqual(3);
    }

    [TestCase]
    public void KillRate_ReturnsRecentKills()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterEnemyKill(1.0f);
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.RegisterEnemyKill(1.0f);
        _performanceData.TotalSessionTime = 2.0;
        _performanceData.RegisterEnemyKill(1.0f);

        // Act
        _performanceData.TotalSessionTime = 5.0;
        var result = _performanceData.KillRate;

        // Assert - 3 kills registered
        AssertFloat(result).IsEqual(3f);
    }

    [TestCase]
    public void KillRate_ExpiresOldKills()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.RegisterEnemyKill(1.0f);
        _performanceData.RegisterEnemyKill(1.0f);

        // Act - move time past 15s window
        _performanceData.TotalSessionTime = 20.0;
        var result = _performanceData.KillRate;

        // Assert
        AssertFloat(result).IsEqual(0f);
    }

    #endregion

    #region Health Tracking (Flailing Detection) Tests

    [TestCase]
    public void UpdateHealthTracking_WhenHealthDrops_RecordsHPDrop()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f); // Initial health

        // Act
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(70f); // Dropped 30 HP

        // Assert
        var result = _performanceData.RecentHPDrop;
        AssertFloat(result).IsEqual(30f);
    }

    [TestCase]
    public void UpdateHealthTracking_WhenHealthIncreases_DoesNotRecordDrop()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(50f); // Initial health

        // Act
        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(80f); // Healed

        // Assert
        var result = _performanceData.RecentHPDrop;
        AssertFloat(result).IsEqual(0f);
    }

    [TestCase]
    public void RecentHPDrop_ExpiresOldDrops()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f);
        _performanceData.TotalSessionTime = 0.5;
        _performanceData.UpdateHealthTracking(50f); // 50 HP drop

        // Act - move time past 5s window
        _performanceData.TotalSessionTime = 10.0;
        var result = _performanceData.RecentHPDrop;

        // Assert
        AssertFloat(result).IsEqual(0f);
    }

    [TestCase]
    public void UpdateHealthTracking_MultipleDrops_Accumulates()
    {
        // Arrange & Act
        _performanceData.TotalSessionTime = 0.0;
        _performanceData.UpdateHealthTracking(100f);

        _performanceData.TotalSessionTime = 1.0;
        _performanceData.UpdateHealthTracking(80f); // -20

        _performanceData.TotalSessionTime = 2.0;
        _performanceData.UpdateHealthTracking(60f); // -20

        _performanceData.TotalSessionTime = 3.0;
        _performanceData.UpdateHealthTracking(50f); // -10

        // Assert
        var result = _performanceData.RecentHPDrop;
        AssertFloat(result).IsEqual(50f);
    }

    #endregion

    #region Spawn Tracking Tests

    [TestCase]
    public void RegisterEnemySpawn_TracksSpawnValue()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;

        // Act
        _performanceData.RegisterEnemySpawn(5.0f);
        _performanceData.RegisterEnemySpawn(3.0f);

        // Assert
        var result = _performanceData.RecentSpawnedValue;
        AssertFloat(result).IsEqual(8f);
    }

    [TestCase]
    public void RegisterDirectorSpawn_TracksSeparatelyFromNormalSpawns()
    {
        // Arrange
        _performanceData.TotalSessionTime = 0.0;

        // Act
        _performanceData.RegisterEnemySpawn(10f);        // Normal spawn
        _performanceData.RegisterDirectorSpawn(5f);      // Director spawn

        // Assert
        AssertFloat(_performanceData.RecentSpawnedValue).IsEqual(10f);
        AssertFloat(_performanceData.RecentDirectorSpawnValue).IsEqual(5f);
    }

    #endregion
}
