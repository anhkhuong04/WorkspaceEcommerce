using System.Collections.Generic;
using System.Linq;

namespace WorkspaceEcommerce.Domain.Common;

public class LocalizedText : Dictionary<string, string>
{
    public LocalizedText() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public LocalizedText(IDictionary<string, string> dictionary) : base(dictionary, StringComparer.OrdinalIgnoreCase)
    {
    }

    public string Get(string languageCode, string defaultLanguageCode = "en")
    {
        if (TryGetValue(languageCode, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (TryGetValue(defaultLanguageCode, out var defaultValue) && !string.IsNullOrWhiteSpace(defaultValue))
        {
            return defaultValue;
        }

        return Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
    }

    public static LocalizedText Of(string text)
    {
        return new LocalizedText(new Dictionary<string, string>
        {
            ["en"] = text,
            ["vi"] = text
        });
    }
}
