using VitalQ.BusinessLogic.Common;

namespace VitalQ.Tests.Algorithms;

public class PriorityScoreCalculatorTests
{
    [Fact]
    public void CalculateScore_InitialTriage_ReturnsBaseWeights()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var redScore = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightRed, now, now);
        var yellowScore = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightYellow, now, now);
        var greenScore = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightGreen, now, now);

        // Assert
        redScore.Should().Be(1000);
        yellowScore.Should().Be(500);
        greenScore.Should().Be(100);
    }

    [Theory]
    [InlineData(5, 110)]   // 100 + (5 * 2) = 110
    [InlineData(10, 120)]  // 100 + (10 * 2) = 120
    [InlineData(30, 160)]  // 100 + (30 * 2) = 160
    public void CalculateScore_GreenUnder60Minutes_AccruesTwoPointsPerMinute(int minutesWaiting, int expectedScore)
    {
        // Arrange
        var triagedAt = DateTime.UtcNow.AddMinutes(-minutesWaiting);
        var now = DateTime.UtcNow;

        // Act
        var score = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightGreen, triagedAt, now);

        // Assert
        score.Should().Be(expectedScore);
    }

    [Theory]
    [InlineData(10, 520)]  // 500 + (10 * 2) = 520
    [InlineData(40, 580)]  // 500 + (40 * 2) = 580
    public void CalculateScore_YellowAcuity_AccruesTwoPointsPerMinute(int minutesWaiting, int expectedScore)
    {
        // Arrange
        var triagedAt = DateTime.UtcNow.AddMinutes(-minutesWaiting);
        var now = DateTime.UtcNow;

        // Act
        var score = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightYellow, triagedAt, now);

        // Assert
        score.Should().Be(expectedScore);
    }

    [Fact]
    public void CalculateScore_GreenAt60Minutes_AutoEscalatesToYellowBaseWeight()
    {
        // Arrange: Green patient waited exactly 60 minutes
        // Expected: BaseWeight escalates to 500 (Yellow), score = 500 + (60 * 2) = 620
        var triagedAt = DateTime.UtcNow.AddMinutes(-60);
        var now = DateTime.UtcNow;

        // Act
        var score = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightGreen, triagedAt, now);

        // Assert
        score.Should().Be(620);
    }

    [Fact]
    public void CalculateScore_GreenOver60Minutes_ContinuesAccruingUnderEscalatedCeiling()
    {
        // Arrange: Green patient waited 75 minutes
        // Expected: BaseWeight escalates to 500, score = 500 + (75 * 2) = 650
        var triagedAt = DateTime.UtcNow.AddMinutes(-75);
        var now = DateTime.UtcNow;

        // Act
        var score = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightGreen, triagedAt, now);

        // Assert
        score.Should().Be(650);
    }

    [Fact]
    public void CalculateScore_YellowExtremeWait_NeverExceedsRedPriorityCeiling()
    {
        // Arrange: Yellow patient waiting 300 minutes (500 + 600 = 1100 without ceiling)
        // Expected: Capped at YellowMaxCeiling (999) so routine yellow never bypasses code red (1000)
        var triagedAt = DateTime.UtcNow.AddMinutes(-300);
        var now = DateTime.UtcNow;

        // Act
        var score = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightYellow, triagedAt, now);

        // Assert
        score.Should().Be(PriorityScoreCalculator.YellowMaxCeiling); // 999
    }

    [Fact]
    public void CalculateScore_FutureTimeOrNegativeWait_ReturnsBaseWeightWithoutUnderflow()
    {
        // Arrange: Clock skew or negative delta
        var triagedAt = DateTime.UtcNow.AddMinutes(10);
        var now = DateTime.UtcNow;

        // Act
        var score = PriorityScoreCalculator.CalculateScore(PriorityScoreCalculator.BaseWeightGreen, triagedAt, now);

        // Assert
        score.Should().Be(100);
    }

    [Fact]
    public void IsEscalated_WhenGreenAtOrAbove60Minutes_ReturnsTrue()
    {
        // Arrange
        var triagedAt = DateTime.UtcNow.AddMinutes(-60);
        var now = DateTime.UtcNow;

        // Act & Assert
        PriorityScoreCalculator.IsEscalated(PriorityScoreCalculator.BaseWeightGreen, triagedAt, now).Should().BeTrue();
    }

    [Fact]
    public void IsEscalated_WhenGreenUnder60Minutes_ReturnsFalse()
    {
        // Arrange
        var triagedAt = DateTime.UtcNow.AddMinutes(-59);
        var now = DateTime.UtcNow;

        // Act & Assert
        PriorityScoreCalculator.IsEscalated(PriorityScoreCalculator.BaseWeightGreen, triagedAt, now).Should().BeFalse();
    }

    [Fact]
    public void IsEscalated_WhenAlreadyYellowOrRed_ReturnsFalse()
    {
        // Arrange
        var triagedAt = DateTime.UtcNow.AddMinutes(-90);
        var now = DateTime.UtcNow;

        // Act & Assert
        PriorityScoreCalculator.IsEscalated(PriorityScoreCalculator.BaseWeightYellow, triagedAt, now).Should().BeFalse();
        PriorityScoreCalculator.IsEscalated(PriorityScoreCalculator.BaseWeightRed, triagedAt, now).Should().BeFalse();
    }
}
