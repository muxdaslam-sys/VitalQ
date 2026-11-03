using System.Text.RegularExpressions;
using VitalQ.BusinessLogic.Common;

namespace VitalQ.Tests.Algorithms;

public class MrnGeneratorTests
{
    [Fact]
    public void Generate_MatchesExpectedFormat()
    {
        // Act
        var mrn = MrnGenerator.Generate();

        // Assert
        mrn.Should().NotBeNullOrWhiteSpace();
        // Pattern: MRN-YYMMDD-XXXXXXXX (where XXXXXXXX is 8 hex chars)
        Regex.IsMatch(mrn, @"^MRN-\d{6}-[0-9A-F]{8}$").Should().BeTrue($"MRN was: {mrn}");
        mrn.Length.Should().Be(19);
    }

    [Fact]
    public void Generate_MultipleCalls_GeneratesUniqueValues()
    {
        // Arrange
        const int count = 200;
        var set = new HashSet<string>();

        // Act
        for (int i = 0; i < count; i++)
        {
            set.Add(MrnGenerator.Generate());
        }

        // Assert: 32-bit entropy guarantees zero collisions across sequential draws
        set.Count.Should().Be(count);
    }
}
