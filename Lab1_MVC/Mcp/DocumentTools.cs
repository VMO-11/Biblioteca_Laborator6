// Agentul din I7 il descopera singur (tools/list), fara modificari de cod.
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public class DocumentTools
{
    [McpServerTool(Name = "search_documents", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description(
        "Cauta semantic in documentele Bibliotecii FSEGA: " +
        "regulament (imprumuturi, penalitati, program), " +
        "cont-online (autentificare, parole, rezervari, notificari) " +
        "si carte (descrierile cartilor din catalog). " +
        "Returneaza fragmente cu titlu, sursa si scor de similaritate.")]
    public static Task<List<DocumentHit>> SearchDocuments(
        DocumentIndex index, 
      
        [Description("Intrebarea sau descrierea, in cuvintele cititorului.")] string query,
        [Description("Sursa: 'regulament', 'cont-online', 'carte' sau null pentru toate sursele.")] string? source = null,
        [Description("Cate fragmente (1–8). Implicit 3.")] int top = 3,
        CancellationToken ct = default)
    {
        var src = string.IsNullOrWhiteSpace(source) ? null : source.Trim().ToLowerInvariant();
        return index.SearchAsync(query, Math.Clamp(top, 1, 8), src, ct);
    }
}
