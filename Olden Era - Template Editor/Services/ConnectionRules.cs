using OldenEraTemplateEditor.Models;

namespace Olden_Era___Template_Editor.Services;

public static class ConnectionRules
{
    /// <summary>Proximity controls zone placement; it does not create a passage for a hero.</summary>
    public static bool AllowsTravel(Connection connection) =>
        !string.Equals(connection.ConnectionType, "Proximity", StringComparison.OrdinalIgnoreCase);
}
