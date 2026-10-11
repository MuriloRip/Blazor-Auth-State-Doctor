using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

return await AuthDoctorCli.RunAsync(args);

internal static partial class AuthDoctorCli
{
    private const int MaxTraceBytes = 1_048_576;
    private const int MaxObservations = 10_000;
    private const string TraceSchema = "authdoctor-trace/v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        MaxDepth = 16,
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "inspect" => await InspectAsync(args[1..]),
                "test" => await TestAsync(args[1..]),
                _ => UsageError($"Comando não reconhecido: {SafeToken(args[0])}")
            };
        }
        catch (TraceInputException ex)
        {
            Console.Error.WriteLine($"Entrada inválida: {ex.Message}");
            return 2;
        }
        catch (IOException)
        {
            Console.Error.WriteLine("Não foi possível ler ou gravar o arquivo solicitado.");
            return 2;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine("Acesso negado ao arquivo ou diretório solicitado.");
            return 2;
        }
        catch (Exception)
        {
            // Do not print exception messages or stack traces: they may contain user paths or input.
            Console.Error.WriteLine("Falha interna. Nenhum detalhe do input foi impresso; execute novamente com uma entrada validada.");
            return 2;
        }
    }

    private static async Task<int> InspectAsync(string[] args)
    {
        if (args.Length is < 1 or > 1)
            return UsageError("Uso: authdoctor inspect <projeto.csproj|diretorio>");

        var input = Path.GetFullPath(args[0]);
        var project = Directory.Exists(input)
            ? Directory.EnumerateFiles(input, "*.csproj", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal).FirstOrDefault()
            : input;
        if (project is null || !File.Exists(project) || !project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            throw new TraceInputException("não foi encontrado um arquivo .csproj no local informado.");

        var frameworks = new List<string>();
        var packages = new List<string>();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1_000_000 };
        using (var reader = XmlReader.Create(project, settings))
        {
            var xml = XDocument.Load(reader);
            frameworks.AddRange(xml.Descendants().Where(e => e.Name.LocalName is "TargetFramework" or "TargetFrameworks")
                .SelectMany(e => (e.Value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
            packages.AddRange(xml.Descendants().Where(e => e.Name.LocalName == "PackageReference")
                .Select(e => (string?)e.Attribute("Include"))
                .Where(s => s is not null && (s.Contains("AspNetCore", StringComparison.OrdinalIgnoreCase) || s.Contains("Blazor", StringComparison.OrdinalIgnoreCase)))
                .Select(s => s!));
        }

        var modes = DiscoverRenderModes(Path.GetDirectoryName(project)!);
        var sdk = await GetSdkVersionAsync(Path.GetDirectoryName(project)!);
        var result = new ProjectSummary(Path.GetFileName(project), sdk, frameworks.Distinct().Order().ToArray(), modes, packages.Distinct().Order().ToArray(),
            "Render modes são estimativas por busca textual; confirme a configuração efetiva no app.");
        Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        return 0;
    }

    private static string[] DiscoverRenderModes(string root)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        var filesScanned = 0;
        foreach (var file in EnumerateSourceFiles(root))
        {
            if (filesScanned++ >= 500) break;
            try
            {
                var info = new FileInfo(file);
                if (info.Length > 1_000_000) continue;
                var text = File.ReadAllText(file);
                if (RenderModeRegex().IsMatch(text))
                    foreach (Match match in RenderModeRegex().Matches(text)) found.Add(match.Groups[1].Value);
                if (text.Contains("ServerPrerendered", StringComparison.Ordinal)) found.Add("ServerPrerendered (legacy marker)");
            }
            catch (IOException) { /* Skip unreadable or transient source files. */ }
            catch (UnauthorizedAccessException) { /* Skip inaccessible source files. */ }
        }
        return found.ToArray();
    }

    private static IEnumerable<string> EnumerateSourceFiles(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        var visitedDirectories = 0;
        while (pending.Count > 0)
        {
            if (visitedDirectories++ >= 2_000) yield break;
            var directory = pending.Pop();
            IEnumerable<string> subdirs;
            IEnumerable<string> files;
            try
            {
                var remainingDirectories = Math.Max(0, 2_000 - visitedDirectories - pending.Count);
                subdirs = Directory.EnumerateDirectories(directory).Take(remainingDirectories).ToArray();
                files = Directory.EnumerateFiles(directory, "*.razor").Take(250)
                    .Concat(Directory.EnumerateFiles(directory, "*.cs").Take(250)).ToArray();
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            foreach (var subdir in subdirs)
            {
                var name = Path.GetFileName(subdir);
                if (name is not ("bin" or "obj" or ".git" or "node_modules" or "wwwroot"))
                {
                    try
                    {
                        if ((File.GetAttributes(subdir) & FileAttributes.ReparsePoint) == 0) pending.Push(subdir);
                    }
                    catch (IOException) { /* Skip paths that disappear during inspection. */ }
                    catch (UnauthorizedAccessException) { /* Skip inaccessible paths. */ }
                }
            }
            foreach (var file in files) yield return file;
        }
    }

    private static async Task<string> GetSdkVersionAsync(string workingDirectory)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("dotnet", "--version")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null) return "unavailable";
            var output = await process.StandardOutput.ReadToEndAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await process.WaitForExitAsync(cts.Token);
            var version = output.Trim();
            return process.ExitCode == 0 && Regex.IsMatch(version, @"^\d{1,3}\.\d{1,3}\.\d{1,4}([-.+][A-Za-z0-9.-]+)?$") ? version : "unavailable";
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or OperationCanceledException)
        {
            return "unavailable";
        }
    }

    private static async Task<int> TestAsync(string[] args)
    {
        string? tracePath = null;
        string format = "terminal";
        string? outputPath = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--trace" when i + 1 < args.Length: tracePath = args[++i]; break;
                case "--format" when i + 1 < args.Length: format = args[++i]; break;
                case "--output" when i + 1 < args.Length: outputPath = args[++i]; break;
                default: return UsageError("Opção inválida. Uso: authdoctor test --trace <arquivo.json> [--format terminal|json|sarif] [--output <arquivo>]");
            }
        }
        if (tracePath is null) return UsageError("Informe --trace <arquivo.json>.");
        if (format is not ("terminal" or "json" or "sarif")) return UsageError("Formato permitido: terminal, json ou sarif.");

        var traceInfo = new FileInfo(tracePath);
        if (!traceInfo.Exists) throw new TraceInputException("arquivo de trace não encontrado.");
        if (traceInfo.Length == 0 || traceInfo.Length > MaxTraceBytes)
            throw new TraceInputException("o trace deve ter entre 1 byte e 1 MiB.");
        var bytes = await ReadBoundedAsync(tracePath);
        TraceDocument? document;
        try
        {
            using var parsed = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16, CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false });
            EnsureNoDuplicateProperties(parsed.RootElement);
            document = JsonSerializer.Deserialize<TraceDocument>(parsed.RootElement, JsonOptions);
        }
        catch (JsonException) { throw new TraceInputException("JSON malformado ou com estrutura inválida."); }
        if (document is null || document.Schema != TraceSchema)
            throw new TraceInputException($"schema deve ser '{TraceSchema}'.");
        if (document.Observations is null || document.Observations.Count is 0 or > MaxObservations)
            throw new TraceInputException($"o trace deve conter de 1 a {MaxObservations} observações.");

        var lastByScenario = new Dictionary<string, long>(StringComparer.Ordinal);
        var findings = new List<DiagnosticFinding>();
        foreach (var item in document.Observations)
        {
            ValidateObservation(item);
            var scenarioId = item.ScenarioId!;
            var transition = item.Transition!;
            var expectation = item.Expectation!;
            if (lastByScenario.TryGetValue(scenarioId, out var previous) && item.ElapsedMs < previous)
                throw new TraceInputException("elapsedMs deve ser não decrescente dentro de cada scenarioId.");
            lastByScenario[scenarioId] = item.ElapsedMs;

            var mismatch = item.ServerAuthenticated != item.UiAuthenticated;
            var expectedReload = expectation == "full-reload-required";
            var status = !mismatch ? "conformant" : expectedReload ? "expected-full-reload" : "failure";
            var classification = !mismatch ? "none" : expectedReload ? "documented-full-reload-boundary" : item.ServerAuthenticated
                ? "ui-stale-unauthenticated" : "ui-stale-authenticated";
            findings.Add(new DiagnosticFinding(scenarioId, transition, item.ElapsedMs,
                item.ServerAuthenticated, item.UiAuthenticated, expectation, status, classification,
                !mismatch ? "Estado visual e estado observado no servidor coincidem neste checkpoint." : expectedReload
                    ? "Divergência classificada como limite que requer full reload, conforme declarado no trace."
                    : item.ServerAuthenticated
                        ? "Servidor autenticado e UI não autenticada: possível falha de propagação; verifique render mode, boundary e ciclo de vida."
                        : "Servidor não autenticado e UI autenticada: possível estado visual obsoleto; valide autorização server-side e logout."));
        }

        var report = new DiagnosticReport(TraceSchema, findings.Count, findings.Count(f => f.Status == "failure"), findings.Count(f => f.Status == "expected-full-reload"), findings);
        var rendered = format switch
        {
            "json" => JsonSerializer.Serialize(report, JsonOptions),
            "sarif" => JsonSerializer.Serialize(ToSarif(report), JsonOptions),
            _ => RenderTerminal(report)
        };
        if (outputPath is not null) await File.WriteAllTextAsync(outputPath, rendered + Environment.NewLine, new UTF8Encoding(false));
        else Console.WriteLine(rendered);
        return report.Failures > 0 ? 1 : 0;
    }

    private static void ValidateObservation(TraceObservation item)
    {
        if (item is null) throw new TraceInputException("observação vazia.");
        if (item.ScenarioId is null || !SafeIdRegex().IsMatch(item.ScenarioId)) throw new TraceInputException("scenarioId deve conter somente 1–64 caracteres alfanuméricos, '_' ou '-'.");
        if (item.Transition is null || !AllowedTransitions.Contains(item.Transition)) throw new TraceInputException("transition não pertence ao vocabulário permitido.");
        if (item.ElapsedMs < 0 || item.ElapsedMs > 86_400_000) throw new TraceInputException("elapsedMs deve estar entre 0 e 86400000.");
        if (item.Expectation is not ("converge" or "full-reload-required")) throw new TraceInputException("expectation deve ser 'converge' ou 'full-reload-required'.");
    }

    private static readonly HashSet<string> AllowedTransitions = new(StringComparer.Ordinal)
    { "prerender", "hydrate", "login", "logout", "hard-refresh", "enhanced-navigation", "reconnect", "second-tab" };

    private static async Task<byte[]> ReadBoundedAsync(string path)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16_384, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var output = new MemoryStream();
        var buffer = new byte[16_384];
        while (true)
        {
            var count = await input.ReadAsync(buffer);
            if (count == 0) break;
            if (output.Length + count > MaxTraceBytes) throw new TraceInputException("o trace deve ter no máximo 1 MiB.");
            output.Write(buffer, 0, count);
        }
        if (output.Length == 0) throw new TraceInputException("o trace não pode estar vazio.");
        return output.ToArray();
    }

    private static void EnsureNoDuplicateProperties(JsonElement root)
    {
        var pending = new Stack<JsonElement>();
        pending.Push(root);
        while (pending.TryPop(out var element))
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new TraceInputException("JSON contém propriedades duplicadas.");
                    pending.Push(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in element.EnumerateArray()) pending.Push(child);
            }
        }
    }

    private static string RenderTerminal(DiagnosticReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Auth State Doctor — {report.Schema}");
        sb.AppendLine($"Observações: {report.ObservationCount} | Falhas: {report.Failures} | Full reload esperado: {report.ExpectedReloads}");
        foreach (var finding in report.Findings)
        {
            var tag = finding.Status switch { "failure" => "FAIL", "expected-full-reload" => "EXPECTED-RELOAD", _ => "PASS" };
            sb.AppendLine($"[{tag}] {finding.ScenarioId}/{finding.Transition} @ {finding.ElapsedMs}ms — {finding.Classification}");
            sb.AppendLine($"  server={finding.ServerAuthenticated}; ui={finding.UiAuthenticated}. {finding.Guidance}");
        }
        return sb.ToString().TrimEnd();
    }

    private static object ToSarif(DiagnosticReport report) => new Dictionary<string, object?>
    {
        ["version"] = "2.1.0",
        ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
        ["runs"] = new[] { new
        {
            tool = new { driver = new { name = "Blazor Auth State Doctor", informationUri = "https://github.com/MuriloRip/Blazor-Auth-State-Doctor", rules = new[] {
                new { id = "AUTHSTATE001", shortDescription = new { text = "Server/UI authentication state mismatch" }, helpUri = "https://learn.microsoft.com/aspnet/core/blazor/security/" }
            } } },
            results = report.Findings.Where(f => f.Status == "failure").Select(f => new
            {
                ruleId = "AUTHSTATE001", level = "error",
                message = new { text = $"{f.ScenarioId}/{f.Transition}: {f.Classification}. {f.Guidance}" },
                properties = new { scenarioId = f.ScenarioId, transition = f.Transition, elapsedMs = f.ElapsedMs, serverAuthenticated = f.ServerAuthenticated, uiAuthenticated = f.UiAuthenticated }
            }).ToArray()
        } }
    };

    private static int UsageError(string message) { Console.Error.WriteLine(message); PrintHelp(); return 2; }
    private static string SafeToken(string value)
    {
        var sanitized = Regex.Replace(value, @"[^A-Za-z0-9_-]", "?");
        return sanitized[..Math.Min(sanitized.Length, 40)];
    }
    private static void PrintHelp() => Console.WriteLine("""
        Blazor Auth State Doctor (MVP)

        Comandos:
          authdoctor inspect <projeto.csproj|diretorio>
              Descobre TFM, SDK e render modes por heurística local.
          authdoctor test --trace <arquivo.json> [--format terminal|json|sarif] [--output <arquivo>]
              Valida observations tipadas de um trace sanitizado; exit 1 significa divergência não esperada.

        O input deve seguir samples/trace.example.json. Campos desconhecidos não são copiados para o relatório.
        """);

    [GeneratedRegex(@"@rendermode\s+(InteractiveServer|InteractiveWebAssembly|InteractiveAuto)", RegexOptions.CultureInvariant)]
    private static partial Regex RenderModeRegex();
    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdRegex();
}

internal sealed record TraceDocument(string? Schema, List<TraceObservation>? Observations);
internal sealed record TraceObservation(string? ScenarioId, string? Transition, long ElapsedMs, bool ServerAuthenticated, bool UiAuthenticated, string? Expectation);
internal sealed record DiagnosticFinding(string ScenarioId, string Transition, long ElapsedMs, bool ServerAuthenticated, bool UiAuthenticated, string Expectation, string Status, string Classification, string Guidance);
internal sealed record DiagnosticReport(string Schema, int ObservationCount, int Failures, int ExpectedReloads, IReadOnlyList<DiagnosticFinding> Findings);
internal sealed record ProjectSummary(string Project, string SdkVersion, IReadOnlyList<string> TargetFrameworks, IReadOnlyList<string> RenderModes, IReadOnlyList<string> RelevantPackages, string RenderModeDetectionNote);
internal sealed class TraceInputException(string message) : Exception(message);
