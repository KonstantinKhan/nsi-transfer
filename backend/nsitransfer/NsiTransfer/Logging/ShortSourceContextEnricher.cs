using Serilog.Core;
using Serilog.Events;

namespace NsiTransfer.Logging
{
    /// <summary>
    /// Enricher для добавления короткого имени класса в логи (только имя без namespace)
    /// </summary>
    public class ShortSourceContextEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
            {
                var sourceContextValue = sourceContext.ToString().Trim('"');
                var shortName = sourceContextValue.Split('.').Last();
                logEvent.AddPropertyIfAbsent(new LogEventProperty("ShortSourceContext", new ScalarValue(shortName)));
            }
        }
    }
}
