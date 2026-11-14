// Biblioteca.Agent
// 1 = inspector (fara model de limbaj): ce vede clientul MCP
// 2 = agentul: Ollama + tool-urile MCP ale Bibliotecii
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;
using System.ClientModel;


// Portul https al aplicației Biblioteca (launchSettings.json)
const string McpEndpoint = "https://localhost:7105/mcp";
const string OllamaEndpoint = "http://localhost:11434/v1";
const string Model = "qwen3:4b";

var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri(McpEndpoint)
});
await using McpClient mcp = await McpClient.CreateAsync(transport);
IList<McpClientTool> tools = await mcp.ListToolsAsync();

Console.Write("1 Inspector ; 2 Agent ; 4 Evaluare > ");

var optiune = Console.ReadLine();

if (optiune == "1")
    await Inspector();
else if (optiune == "2")
    await Agent();
else if (optiune == "4")
    await Evaluare();
else
    Console.WriteLine("Optiune invalida.");

//  Misiunea 1 (Inspectorul)
async Task Inspector()
{
    foreach (var t in tools)
    {
        Console.WriteLine($"\n== {t.Name}\n{t.Description}");
        Console.WriteLine(JsonSerializer.Serialize(t.JsonSchema,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    Console.Write("\nCautati in catalog (search_books): ");
    var query = Console.ReadLine() ?? "";
    CallToolResult result = await mcp.CallToolAsync("search_books",
        new Dictionary<string, object?> { ["query"] = query });

    Console.WriteLine($"IsError: {result.IsError}");
    foreach (var block in result.Content.OfType<TextContentBlock>())
        Console.WriteLine(block.Text);
}

//  Misiunea 2  Agentul
async Task Agent()
{
    IChatClient chat = new ChatClientBuilder(
            new OpenAIClient(new ApiKeyCredential("ollama"),
                             new OpenAIClientOptions { Endpoint = new Uri(OllamaEndpoint) })
                .GetChatClient(Model)
                .AsIChatClient())
        .UseFunctionInvocation()        // execută automat tool-urile cerute de model
        .Build();

    List<ChatMessage> messages =
    [
        new(ChatRole.System,
            "Ești asistentul Bibliotecii FSEGA. Răspunzi în limba română. " +
            "Pentru orice informație despre cărți, cititori sau recomandări folosește tool-urile. " +
            "Nu inventa titluri, prețuri sau autori: dacă tool-urile nu întorc informația, spune că nu o ai.")
    ];
    var options = new ChatOptions { Tools = [.. tools] };

    Console.WriteLine($"Tool-uri disponibile: {string.Join(", ", tools.Select(t => t.Name))}");
    while (true)
    {
        Console.Write("\nTu: ");
        var question = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(question)) break;

        messages.Add(new(ChatRole.User, question));
        ChatResponse response = await chat.GetResponseAsync(messages, options);
        messages.AddMessages(response);

        // urma agentului: ce tool-uri a apelat si cu ce argumente
        foreach (var call in response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>())
        {
            var args = string.Join(", ", (call.Arguments ?? new Dictionary<string, object?>())
                .Select(a => $"{a.Key}={a.Value}"));
            Console.WriteLine($"  [tool] {call.Name}({args})");
        }
        Console.WriteLine($"Asistent: {response.Text}");
    }
}

async Task Evaluare()
{
    var path = Path.Combine(AppContext.BaseDirectory, "eval.json");

    if (!File.Exists(path))
    {
        Console.WriteLine("Nu gasesc eval.json.");
        return;
    }

    using var fisier = JsonDocument.Parse(
        await File.ReadAllTextAsync(path));

    int total = 0;
    int gasite1 = 0, gasite3 = 0, gasite5 = 0;

    Console.WriteLine("\nEVALUARE RAG - top 5\n");

    foreach (var test in fisier.RootElement.EnumerateArray())
    {
        string question = test.GetProperty("question").GetString()!;
        string source = test.GetProperty("source").GetString()!;
        string expected = test.GetProperty("expected").GetString()!;

        total++;

        var result = await mcp.CallToolAsync(
            "search_documents",
            new Dictionary<string, object?>
            {
                ["query"] = question,
                ["source"] = source,
                ["top"] = 5
            });

        int pozitie = 0;
        double primulScor = 0;

        foreach (var block in result.Content.OfType<TextContentBlock>())
        {
            try
            {
                using var json = JsonDocument.Parse(block.Text);
                var hits = json.RootElement;

                if (hits.ValueKind != JsonValueKind.Array)
                    continue;

                int index = 0;

                foreach (var hit in hits.EnumerateArray())
                {
                    index++;

                    string title = "";
                    double score = 0;

                    foreach (var property in hit.EnumerateObject())
                    {
                        if (property.Name.Equals(
                                "title", StringComparison.OrdinalIgnoreCase))
                            title = property.Value.GetString() ?? "";

                        if (property.Name.Equals(
                                "score", StringComparison.OrdinalIgnoreCase))
                            score = property.Value.GetDouble();
                    }

                    if (index == 1)
                        primulScor = score;
                    

                    if (pozitie == 0 &&
                        Normalizeaza(title).StartsWith(
                            Normalizeaza(expected),
                            StringComparison.OrdinalIgnoreCase))
                        pozitie = index;
                }
            }
            catch (JsonException)
            {
                Console.WriteLine("Raspuns MCP care nu este JSON valid.");
            }
        }

        if (pozitie == 1) gasite1++;
        if (pozitie is >= 1 and <= 3) gasite3++;
        if (pozitie is >= 1 and <= 5) gasite5++;

        Console.WriteLine(
            $"{total,2}. {question}\n" +
            $"    Loc: {(pozitie == 0 ? "negasit" : pozitie.ToString())}" +
            $" | Scor primul: {primulScor:F3}");
    }

    Console.WriteLine("\nREZULTATE:");
    Console.WriteLine($"Recall@1: {100.0 * gasite1 / total:F1}%");
    Console.WriteLine($"Recall@3: {100.0 * gasite3 / total:F1}%");
    Console.WriteLine($"Recall@5: {100.0 * gasite5 / total:F1}%");
}

string Normalizeaza(string text)
{
    string descompus = text
        .Replace("\\.", ".")
        .Normalize(System.Text.NormalizationForm.FormD);

    return new string(descompus
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray())
        .Normalize(System.Text.NormalizationForm.FormC)
        .Trim();
}
