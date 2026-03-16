using GitCatalog.Core;

namespace GitCatalog.Generation;

internal static class DiagramHelpers
{
    /// <summary>
    /// Converts a <see cref="CatalogRelationshipType"/> enum value to a snake_case label
    /// suitable for use as a Mermaid edge label.
    /// </summary>
    internal static string ToRelationshipLabel(CatalogRelationshipType type)
    {
        var raw = type.ToString();
        var chars = new List<char>(raw.Length + 8);
        foreach (var c in raw)
        {
            if (char.IsUpper(c) && chars.Count > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(c));
        }

        return new string(chars.ToArray());
    }

    /// <summary>
    /// Escapes double-quote characters so a string can be safely embedded inside
    /// a Mermaid quoted label.
    /// </summary>
    internal static string EscapeLabel(string value)
        => value.Replace("\"", "\\\"", StringComparison.Ordinal);
}
