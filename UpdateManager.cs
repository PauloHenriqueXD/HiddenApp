using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace HiddenApp;

/// <summary>Versão no formato SemVer: 1.2.3 ou 1.2.3-beta.1 (pré-lançamento).</summary>
public readonly record struct SemVer(int Major, int Minor, int Patch, string Pre) : IComparable<SemVer>
{
    public bool IsPrerelease => !string.IsNullOrEmpty(Pre);

    public static bool TryParse(string? text, out SemVer version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string s = text.Trim();
        if (s.StartsWith('v') || s.StartsWith('V')) s = s[1..];
        int plus = s.IndexOf('+');                     // "+abc123" (hash do commit) é ignorado
        if (plus >= 0) s = s[..plus];
        string pre = "";
        int dash = s.IndexOf('-');
        if (dash >= 0) { pre = s[(dash + 1)..]; s = s[..dash]; }

        var parts = s.Split('.');
        if (parts.Length is < 1 or > 4) return false;
        var n = new int[3];
        for (int i = 0; i < Math.Min(3, parts.Length); i++)
            if (!int.TryParse(parts[i], out n[i]) || n[i] < 0) return false;

        version = new SemVer(n[0], n[1], n[2], pre);
        return true;
    }

    public int CompareTo(SemVer other)
    {
        int c = Major.CompareTo(other.Major);
        if (c == 0) c = Minor.CompareTo(other.Minor);
        if (c == 0) c = Patch.CompareTo(other.Patch);
        if (c != 0) return c;

        string a = Pre ?? "", b = other.Pre ?? "";
        if (a == b) return 0;
        if (a.Length == 0) return 1;   // 1.0.0 é mais nova que 1.0.0-beta
        if (b.Length == 0) return -1;

        var pa = a.Split('.'); var pb = b.Split('.');
        for (int i = 0; i < Math.Min(pa.Length, pb.Length); i++)
        {
            bool na = int.TryParse(pa[i], out int ia), nb = int.TryParse(pb[i], out int ib);
            int r = (na, nb) switch
            {
                (true, true) => ia.CompareTo(ib),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(pa[i], pb[i]),
            };
            if (r != 0) return r;
        }
        return pa.Length.CompareTo(pb.Length);
    }

    public static bool operator >(SemVer a, SemVer b) => a.CompareTo(b) > 0;
    public static bool operator <(SemVer a, SemVer b) => a.CompareTo(b) < 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}" + (IsPrerelease ? "-" + Pre : "");
}

/// <summary>Uma versão publicada no GitHub Releases que é mais nova que a atual.</summary>
public record UpdateInfo(SemVer Version, string Tag, bool IsPrerelease, string Notes, string PageUrl,
                         string ExeUrl, string? ShaUrl, long Size);

/// <summary>
/// Verifica o GitHub Releases, baixa a versão nova, confere o SHA-256 e troca o executável.
/// O .exe em uso é renomeado para ".old" (o Windows permite renomear, mas não sobrescrever,
/// um programa em execução); a versão nova é aberta com --updated e apaga o .old depois.
/// </summary>
internal static class UpdateManager
{
    const string Owner = "PauloHenriqueXD";
    const string Repo = "HiddenApp";
    const string ExeAssetName = "HiddenApp.exe";
    public const string UpdatedArgument = "--updated";

    public static string ReleasesPage => $"https://github.com/{Owner}/{Repo}/releases";
    static string Api => $"https://api.github.com/repos/{Owner}/{Repo}";

    public static SemVer CurrentVersion { get; } = ReadCurrentVersion();

    static SemVer ReadCurrentVersion()
    {
        string? info = typeof(UpdateManager).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return SemVer.TryParse(info, out var v) ? v : new SemVer(0, 0, 0, "");
    }

    /// <summary>
    /// Só o executável publicado (arquivo único) se atualiza sozinho. Rodando pelo Visual Studio
    /// existe um HiddenApp.dll ao lado do .exe — nesse caso não troca nada.
    /// </summary>
    public static bool CanSelfUpdate =>
        Environment.ProcessPath != null &&
        !File.Exists(Path.Combine(AppContext.BaseDirectory, "HiddenApp.dll"));

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan }; // tempo limite controlado por chamada
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HiddenApp", CurrentVersion.ToString()));
        return http;
    }

    // ---------- Verificação ----------

    /// <summary>Retorna a versão mais nova disponível, ou null se a atual já é a mais recente.</summary>
    public static async Task<UpdateInfo?> CheckAsync(bool includePrereleases)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var releases = new List<JsonElement>();

        if (includePrereleases)
        {
            // Lista as últimas versões (inclui betas)
            string json = await GetApiAsync($"{Api}/releases?per_page=30", cts.Token) ?? "[]";
            using var doc = JsonDocument.Parse(json);
            foreach (var r in doc.RootElement.EnumerateArray()) releases.Add(r.Clone());
        }
        else
        {
            // "latest" do GitHub já ignora pré-lançamentos e rascunhos
            string? json = await GetApiAsync($"{Api}/releases/latest", cts.Token);
            if (json == null) return null; // nenhuma versão publicada ainda
            using var doc = JsonDocument.Parse(json);
            releases.Add(doc.RootElement.Clone());
        }

        UpdateInfo? best = null;
        foreach (var r in releases)
        {
            if (r.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var info = Parse(r);
            if (info == null) continue;
            if (!includePrereleases && info.IsPrerelease) continue;
            if (!(info.Version > CurrentVersion)) continue;
            if (best == null || info.Version > best.Version) best = info;
        }
        return best;
    }

    static async Task<string?> GetApiAsync(string url, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var resp = await Http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        if (resp.StatusCode == HttpStatusCode.Forbidden || (int)resp.StatusCode == 429)
            throw new InvalidOperationException("O GitHub limitou as consultas por enquanto. Tente de novo mais tarde.");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(ct);
    }

    static UpdateInfo? Parse(JsonElement r)
    {
        string tag = r.GetProperty("tag_name").GetString() ?? "";
        if (!SemVer.TryParse(tag, out var version)) return null;

        string? exeUrl = null, shaUrl = null;
        long size = 0;
        foreach (var a in r.GetProperty("assets").EnumerateArray())
        {
            string? name = a.GetProperty("name").GetString();
            string? url = a.GetProperty("browser_download_url").GetString();
            if (string.Equals(name, ExeAssetName, StringComparison.OrdinalIgnoreCase))
            {
                exeUrl = url;
                size = a.GetProperty("size").GetInt64();
            }
            else if (string.Equals(name, ExeAssetName + ".sha256", StringComparison.OrdinalIgnoreCase))
                shaUrl = url;
        }
        if (exeUrl == null) return null; // versão sem executável anexado

        string notes = r.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.String
            ? body.GetString() ?? "" : "";
        bool pre = r.TryGetProperty("prerelease", out var p) && p.GetBoolean();
        string page = r.TryGetProperty("html_url", out var h) ? h.GetString() ?? ReleasesPage : ReleasesPage;

        return new UpdateInfo(version, tag, pre || version.IsPrerelease, notes, page, exeUrl, shaUrl, size);
    }

    // ---------- Instalação ----------

    /// <summary>
    /// Baixa, confere e coloca a versão nova no lugar da atual, e abre a versão nova.
    /// Depois de retornar com sucesso, o app atual deve encerrar.
    /// </summary>
    public static async Task InstallAsync(UpdateInfo update, IProgress<int>? progress, CancellationToken ct = default)
    {
        if (!CanSelfUpdate)
            throw new InvalidOperationException("A atualização automática só funciona no executável publicado (HiddenApp.exe).");
        if (update.ShaUrl == null)
            throw new InvalidOperationException("Esta versão não tem o arquivo de verificação (.sha256). " +
                                                "Por segurança, a atualização automática foi cancelada.");

        // 1) Hash esperado
        string expected;
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            string text = await Http.GetStringAsync(update.ShaUrl, cts.Token);
            expected = text.Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                           .FirstOrDefault()?.ToLowerInvariant() ?? "";
        }
        if (expected.Length != 64)
            throw new InvalidOperationException("O arquivo de verificação da versão é inválido.");

        // 2) Download do executável novo
        string tempDir = Path.Combine(Path.GetTempPath(), "HiddenApp_update");
        Directory.CreateDirectory(tempDir);
        string tempExe = Path.Combine(tempDir, ExeAssetName);

        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            cts.CancelAfter(TimeSpan.FromMinutes(15));
            using var req = new HttpRequestMessage(HttpMethod.Get, update.ExeUrl);
            req.Headers.Accept.ParseAdd("application/octet-stream");
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            resp.EnsureSuccessStatusCode();
            long total = resp.Content.Headers.ContentLength ?? update.Size;

            await using var src = await resp.Content.ReadAsStreamAsync(cts.Token);
            await using var dst = File.Create(tempExe);
            var buffer = new byte[81920];
            long done = 0;
            int read, lastPercent = -1;
            while ((read = await src.ReadAsync(buffer, cts.Token)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), cts.Token);
                done += read;
                if (total > 0)
                {
                    int percent = (int)(done * 100 / total);
                    if (percent != lastPercent) { lastPercent = percent; progress?.Report(percent); }
                }
            }
        }

        // 3) Confere a integridade
        string actual;
        await using (var fs = File.OpenRead(tempExe))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
        if (actual != expected)
        {
            TryDelete(tempExe);
            throw new InvalidOperationException("O arquivo baixado não confere com a verificação (SHA-256). " +
                                                "A atualização foi cancelada e nada foi alterado.");
        }

        // 4) Troca o executável
        string current = Environment.ProcessPath!;
        string old = current + ".old";
        TryDelete(old);
        File.Move(current, old);
        try
        {
            File.Copy(tempExe, current, overwrite: false);
        }
        catch
        {
            File.Move(old, current); // desfaz
            throw;
        }
        TryDelete(tempExe);

        // 5) Abre a versão nova (ela espera esta fechar antes de assumir)
        Process.Start(new ProcessStartInfo(current, UpdatedArgument)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(current)!,
        });
    }

    /// <summary>Apaga o ".old" deixado pela atualização anterior.</summary>
    public static void CleanupOldVersion()
    {
        if (Environment.ProcessPath is not string current) return;
        string old = current + ".old";
        if (!File.Exists(old)) return;
        Task.Run(async () =>
        {
            for (int i = 0; i < 10 && File.Exists(old); i++)
            {
                TryDelete(old);
                await Task.Delay(1500);
            }
        });
    }

    /// <summary>Abre um link no navegador padrão, como usuário comum (não como administrador).</summary>
    public static void OpenUrl(string url)
    {
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (Native.StartUnelevated(Path.Combine(winDir, "explorer.exe"), $"\"{url}\"", winDir) != 0) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
