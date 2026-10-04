namespace VitalQ.BusinessLogic.Interfaces;

/// <summary>
/// Concurrency-safe atomic token sequence generator.
/// Prevents duplicate token numbers under concurrent high load.
/// </summary>
public interface ITokenGenerator
{
    Task<string> GenerateTokenNumberAsync(Guid departmentId, string departmentCode);
}
