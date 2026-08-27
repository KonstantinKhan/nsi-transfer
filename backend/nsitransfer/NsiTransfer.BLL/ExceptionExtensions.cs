namespace NsiTransfer.BLL;

public static class ExceptionExtensions
{
    public static string GetAllMessages(this Exception ex, string separator = "\n---> ")
    {
        var messages = new List<string>();
        var current = ex;

        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                messages.Add($"{current.GetType().Name}: {current.Message}");
            }
            current = current.InnerException;
        }

        return string.Join(separator, messages);
    }
}
