namespace NsiTransfer.Contract.Exceptions;

public class ConfigurationValidationException : Exception
{
    public IReadOnlyCollection<string> Failures { get; }

    public ConfigurationValidationException(IEnumerable<string> failures)
        : base("Конфигурация не прошла валидацию")
    {
        Failures = failures.ToArray();
    }
}
