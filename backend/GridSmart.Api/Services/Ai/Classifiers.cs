using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace GridSmart.Api.Services.Ai;

/// <summary>
/// Ban gia lap (mac dinh, da chot): phan loai theo tu khoa trong mo ta, khong can mang.
/// Khop 1 nhom: 0.9. Khop nhieu nhom: nhom khop nhieu tu nhat, 0.7. Khong khop: 0.3 (vao hang cho duyet).
/// </summary>
public sealed class MockImageClassifier(IOptions<AiOptions> options) : IImageClassifier
{
    public Task<ClassificationResult> ClassifyAsync(byte[]? image, string? description, CancellationToken ct = default)
        => Task.FromResult(Classify(description, options.Value.MockKeywords));

    public static ClassificationResult Classify(string? description, IReadOnlyDictionary<string, string[]> keywords)
    {
        // So khop nguyen cum tu tren van ban da bo dau: " nap cong " khop "nap cong" nhung "rac" khong khop "racing"
        var text = $" {Normalize(description)} ";
        var scores = keywords
            .Select(kv => (Code: kv.Key, Hits: kv.Value
                .Select(Normalize)
                .Where(k => k.Length > 0)
                .Count(k => text.Contains($" {k} ", StringComparison.Ordinal))))
            .Where(s => s.Hits > 0)
            .OrderByDescending(s => s.Hits)
            .ToList();

        if (scores.Count == 0) return new ClassificationResult(null, "unknown", 0.3);
        var best = scores[0];
        var confidence = scores.Count == 1 ? 0.9 : 0.7;
        return new ClassificationResult(best.Code.ToUpperInvariant(), best.Code.ToLowerInvariant(), confidence);
    }

    /// <summary>Bo dau tieng Viet, chu thuong, gom khoang trang; dau cau thanh khoang trang.</summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var decomposed = input.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return string.Join(' ', sb.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>Goi OpenAI vision, yeu cau tra JSON {label, confidence}.</summary>
public sealed class OpenAiImageClassifier(HttpClient http, IOptions<AiOptions> options) : IImageClassifier
{
    public async Task<ClassificationResult> ClassifyAsync(byte[]? image, string? description, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrEmpty(o.OpenAiApiKey)) throw new InvalidOperationException("Thiếu Ai:OpenAiApiKey.");

        var labels = string.Join(", ", o.LabelMap.Keys);
        var content = new List<object>
        {
            new { type = "text", text = $"Phân loại sự cố đô thị trong ảnh vào đúng một nhãn: {labels}. Mô tả của người dân: \"{description}\". " +
                                         "Trả về JSON {\"label\": \"<nhãn>\", \"confidence\": <0..1>}." },
        };
        if (image is not null)
            content.Add(new { type = "image_url", image_url = new { url = "data:image/jpeg;base64," + Convert.ToBase64String(image) } });

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.OpenAiApiKey);
        req.Content = JsonContent.Create(new
        {
            model = o.OpenAiModel,
            response_format = new { type = "json_object" },
            messages = new object[] { new { role = "user", content } },
        });
        using var res = await http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
        return LabelParser.Parse(text, o.LabelMap);
    }
}

/// <summary>Goi microservice Python (YOLO/CLIP): POST {endpoint} multipart image + description -> {label, confidence}.</summary>
public sealed class HttpImageClassifier(HttpClient http, IOptions<AiOptions> options) : IImageClassifier
{
    public async Task<ClassificationResult> ClassifyAsync(byte[]? image, string? description, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrEmpty(o.HttpEndpoint)) throw new InvalidOperationException("Thiếu Ai:HttpEndpoint.");
        using var form = new MultipartFormDataContent();
        if (image is not null) form.Add(new ByteArrayContent(image) { Headers = { ContentType = new MediaTypeHeaderValue("image/jpeg") } }, "image", "image.jpg");
        form.Add(new StringContent(description ?? ""), "description");
        using var res = await http.PostAsync(o.HttpEndpoint, form, ct);
        res.EnsureSuccessStatusCode();
        return LabelParser.Parse(await res.Content.ReadAsStringAsync(ct), o.LabelMap);
    }
}

/// <summary>Doc JSON {label, confidence} va anh xa nhan AI sang category_code theo Ai:LabelMap.</summary>
public static class LabelParser
{
    public static ClassificationResult Parse(string json, IReadOnlyDictionary<string, string> labelMap)
    {
        using var doc = JsonDocument.Parse(json);
        var label = doc.RootElement.TryGetProperty("label", out var l) ? l.GetString() : null;
        var confidence = doc.RootElement.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var d) ? Math.Clamp(d, 0, 1) : 0;
        var code = label is not null && labelMap.TryGetValue(label, out var mapped) ? mapped : null;
        return new ClassificationResult(code, label, confidence);
    }
}
