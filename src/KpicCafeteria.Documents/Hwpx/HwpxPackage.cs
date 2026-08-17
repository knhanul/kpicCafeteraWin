using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KpicCafeteria.Documents.Hwpx;

/// <summary>
/// HWPX ZIP 패키지 (파일 목록 + XML 읽기/수정 + 저장 + 검증).
/// 기존 hwpx_engine.py HwpxPackage에 대응.
/// </summary>
public sealed class HwpxPackage
{
    public const int MinHwpxSize = 1024;

    private static readonly Regex SectionPattern = new(@"^Contents/section(\d+)\.xml$", RegexOptions.Compiled);

    private static readonly string[] RequiredPackageFiles =
    [
        "mimetype", "Contents/content.hpf", "Contents/header.xml", "version.xml", "META-INF/container.xml",
    ];

    public Dictionary<string, byte[]> Files { get; } = [];

    public List<string> Order { get; } = [];

    public string? SourcePath { get; }

    public int? SourceSize { get; }

    private HwpxPackage(string? sourcePath, int? sourceSize)
    {
        SourcePath = sourcePath;
        SourceSize = sourceSize;
    }

    public static HwpxPackage Load(string templatePath)
    {
        var path = Path.GetFullPath(templatePath);
        if (!File.Exists(path))
        {
            throw new HwpxTemplateError($"템플릿 파일을 찾을 수 없습니다: {path}");
        }

        var fileSize = new FileInfo(path).Length;
        if (fileSize < MinHwpxSize)
        {
            throw new HwpxTemplateError($"파일이 너무 작습니다 ({fileSize}바이트). 정상적인 HWPX 파일이 아닙니다.");
        }

        var package = new HwpxPackage(path, (int)fileSize);
        try
        {
            using var archive = ZipFile.OpenRead(path);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName;
                if (name.StartsWith('/') || name.Contains(".."))
                {
                    throw new HwpxTemplateError($"안전하지 않은 경로가 포함되어 있습니다: {name}");
                }

                using var stream = entry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                package.Files[name] = memory.ToArray();
                package.Order.Add(name);
            }
        }
        catch (InvalidDataException ex)
        {
            throw new HwpxTemplateError($"정상적인 HWPX ZIP 파일이 아닙니다: {path}", ex);
        }

        return package;
    }

    /// <summary>메모리 바이트로 패키지 로드 (테스트/임베디드 자원용).</summary>
    public static HwpxPackage LoadBytes(byte[] content, string? sourceName = null)
    {
        if (content.Length < MinHwpxSize)
        {
            throw new HwpxTemplateError($"파일이 너무 작습니다 ({content.Length}바이트). 정상적인 HWPX 파일이 아닙니다.");
        }

        var package = new HwpxPackage(sourceName, content.Length);
        try
        {
            using var memory = new MemoryStream(content);
            using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName;
                if (name.StartsWith('/') || name.Contains(".."))
                {
                    throw new HwpxTemplateError($"안전하지 않은 경로가 포함되어 있습니다: {name}");
                }

                using var stream = entry.Open();
                using var target = new MemoryStream();
                stream.CopyTo(target);
                package.Files[name] = target.ToArray();
                package.Order.Add(name);
            }
        }
        catch (InvalidDataException ex)
        {
            throw new HwpxTemplateError($"정상적인 HWPX ZIP 파일이 아닙니다: {sourceName ?? "memory"}", ex);
        }

        return package;
    }

    public HwpxPackage Clone()
    {
        var clone = new HwpxPackage(SourcePath, SourceSize);
        foreach (var (name, content) in Files)
        {
            clone.Files[name] = content;
        }

        clone.Order.AddRange(Order);
        return clone;
    }

    public List<string> SectionNames()
        => Files.Keys
            .Where(name => SectionPattern.IsMatch(name))
            .OrderBy(name => SectionSortKey(name))
            .ToList();

    private static (int Index, string Name) SectionSortKey(string name)
    {
        var match = SectionPattern.Match(name);
        return match.Success ? (int.Parse(match.Groups[1].Value), name) : (int.MaxValue, name);
    }

    private static bool IsXmlLike(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.EndsWith(".xml", StringComparison.Ordinal)
            || lower.EndsWith(".hpf", StringComparison.Ordinal)
            || lower.EndsWith(".rdf", StringComparison.Ordinal);
    }

    public List<string> XmlNames()
    {
        var names = Order.Where(IsXmlLike).ToList();
        foreach (var name in Files.Keys)
        {
            if (IsXmlLike(name) && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    public XDocument ReadXml(string name)
    {
        if (!Files.TryGetValue(name, out var content))
        {
            throw new HwpxTemplateError($"패키지에 파일이 없습니다: {name}");
        }

        try
        {
            return XDocument.Parse(DecodeUtf8(content), LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new HwpxTemplateError($"XML 파싱에 실패했습니다 ({name}): {ex.Message}", ex);
        }
    }

    public void WriteXml(string name, XDocument root)
    {
        Files[name] = EncodeUtf8(root);
        if (!Order.Contains(name))
        {
            Order.Add(name);
        }
    }

    private static string DecodeUtf8(byte[] content)
    {
        // BOM이 있으면 제거하고 UTF-8로 해석한다.
        var offset = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF ? 3 : 0;
        return System.Text.Encoding.UTF8.GetString(content, offset, content.Length - offset);
    }

    private static byte[] EncodeUtf8(XDocument root)
    {
        using var stream = new MemoryStream();
        root.Save(stream, SaveOptions.DisableFormatting);
        return stream.ToArray();
    }

    /// <summary>섹션 수를 맞춘다 (부족하면 첫 섹션 복제, 초과하면 제거). content.hpf/header.xml 갱신.</summary>
    public List<string> EnsureSectionCount(int sectionCount)
    {
        if (sectionCount < 1)
        {
            throw new HwpxTemplateError("섹션 수는 1개 이상이어야 합니다.");
        }

        var sections = SectionNames();
        if (sections.Count == 0)
        {
            throw new HwpxTemplateError("HWPX 패키지에 section XML이 없습니다.");
        }

        var baseSection = sections[0];
        var baseXml = Files[baseSection];

        if (sections.Count > sectionCount)
        {
            foreach (var name in sections.Skip(sectionCount))
            {
                Files.Remove(name);
                Order.Remove(name);
            }
        }
        else if (sections.Count < sectionCount)
        {
            for (var index = sections.Count; index < sectionCount; index++)
            {
                var name = $"Contents/section{index}.xml";
                Files[name] = baseXml;
                if (!Order.Contains(name))
                {
                    Order.Add(name);
                }
            }
        }

        Files["Contents/content.hpf"] = UpdateContentHpf(Files["Contents/content.hpf"], sectionCount);
        UpdateHeaderSecCount(sectionCount);
        Order.RemoveAll(name => !Files.ContainsKey(name));
        foreach (var name in SectionNames())
        {
            if (!Order.Contains(name))
            {
                Order.Add(name);
            }
        }

        return SectionNames();
    }

    private void UpdateHeaderSecCount(int sectionCount)
    {
        const string headerName = "Contents/header.xml";
        if (!Files.TryGetValue(headerName, out var content))
        {
            return;
        }

        var root = XDocument.Parse(DecodeUtf8(content), LoadOptions.PreserveWhitespace);
        root.Root?.SetAttributeValue("secCnt", sectionCount.ToString());
        Files[headerName] = EncodeUtf8(root);
    }

    private static byte[] UpdateContentHpf(byte[] content, int sectionCount)
    {
        var root = XDocument.Parse(DecodeUtf8(content), LoadOptions.PreserveWhitespace);
        var manifest = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "manifest")
            ?? throw new HwpxTemplateError("content.hpf의 manifest 요소가 없습니다.");
        var spine = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "spine")
            ?? throw new HwpxTemplateError("content.hpf의 spine 요소가 없습니다.");

        var sectionItems = manifest.Elements().Where(e => (e.Attribute("href")?.Value ?? "").StartsWith("Contents/section", StringComparison.Ordinal)).ToList();
        if (sectionItems.Count == 0)
        {
            throw new HwpxTemplateError("content.hpf에 section 항목이 없습니다.");
        }

        var prototype = sectionItems[0];
        foreach (var element in sectionItems)
        {
            element.Remove();
        }

        foreach (var element in spine.Elements().Where(e => (e.Attribute("idref")?.Value ?? "").StartsWith("section", StringComparison.Ordinal)).ToList())
        {
            element.Remove();
        }

        for (var index = 0; index < sectionCount; index++)
        {
            var item = new XElement(prototype);
            item.SetAttributeValue("id", $"section{index}");
            item.SetAttributeValue("href", $"Contents/section{index}.xml");
            manifest.Add(item);

            var itemref = new XElement(HwpxNamespaces.OpfNs + "itemref",
                new XAttribute("idref", $"section{index}"),
                new XAttribute("linear", "yes"));
            spine.Add(itemref);
        }

        return EncodeUtf8(root);
    }

    /// <summary>
    /// 패키지 검증.
    /// 기존 hwpx_engine.py HwpxPackage.validate에 대응.
    /// </summary>
    public HwpxValidationResult Validate(string? documentType = null, IReadOnlySet<string>? requiredPlaceholders = null, bool allowRemainingPlaceholders = true)
    {
        var fileSize = SourceSize ?? Files.Values.Sum(content => content.Length);
        if (fileSize < MinHwpxSize)
        {
            throw new HwpxTemplateError($"파일이 너무 작습니다 ({fileSize}바이트). 정상적인 HWPX 파일이 아닙니다.");
        }

        var missing = RequiredPackageFiles.Where(name => !Files.ContainsKey(name)).ToList();
        var sections = SectionNames();
        if (sections.Count == 0)
        {
            missing.Add("Contents/section*.xml");
        }

        if (missing.Count > 0)
        {
            throw new HwpxTemplateError($"필수 HWPX 파일이 없습니다: {string.Join(", ", missing)}");
        }

        foreach (var name in XmlNames())
        {
            try
            {
                XDocument.Parse(DecodeUtf8(Files[name]), LoadOptions.PreserveWhitespace);
            }
            catch (System.Xml.XmlException ex)
            {
                throw new HwpxTemplateError($"XML 파싱에 실패했습니다 ({name}): {ex.Message}", ex);
            }
        }

        var contentRoot = XDocument.Parse(DecodeUtf8(Files["Contents/content.hpf"]), LoadOptions.PreserveWhitespace);
        var manifest = contentRoot.Descendants().FirstOrDefault(e => e.Name.LocalName == "manifest")
            ?? throw new HwpxTemplateError("content.hpf에 manifest 요소가 없습니다.");
        var spine = contentRoot.Descendants().FirstOrDefault(e => e.Name.LocalName == "spine")
            ?? throw new HwpxTemplateError("content.hpf에 spine 요소가 없습니다.");

        var manifestIds = manifest.Elements().Select(e => e.Attribute("id")?.Value).Where(id => id is not null).ToHashSet();
        foreach (var itemref in spine.Elements())
        {
            var idref = itemref.Attribute("idref")?.Value;
            if (idref is not null && !manifestIds.Contains(idref))
            {
                throw new HwpxTemplateError($"spine이 manifest에 없는 항목을 참조합니다: {idref}");
            }
        }

        var text = string.Concat(sections.Select(section =>
            string.Concat(ReadXml(section).Descendants().Where(n => n.Name.LocalName == "t").Select(n => n.Value))));
        var placeholders = HwpxPlaceholder.Find(text);

        if (documentType is not null && requiredPlaceholders is null)
        {
            requiredPlaceholders = HwpxPlaceholder.RequiredByType.GetValueOrDefault(documentType);
        }

        if (requiredPlaceholders is not null)
        {
            var missingPlaceholders = requiredPlaceholders.Where(p => !placeholders.Contains(p)).OrderBy(p => p).ToList();
            if (missingPlaceholders.Count > 0)
            {
                throw new HwpxTemplateError("필수 플레이스홀더가 없습니다: " + string.Join(", ", missingPlaceholders));
            }
        }

        if (!allowRemainingPlaceholders && placeholders.Count > 0)
        {
            throw new HwpxTemplateError("템플릿 플레이스홀더가 남아 있습니다: " + string.Join(", ", placeholders));
        }

        return new HwpxValidationResult(sections.Count, Files.Count, placeholders, fileSize);
    }

    /// <summary>패키지를 ZIP으로 저장하고 구조 검증 후 바이트 반환.</summary>
    public byte[] Save(string? outputPath = null)
    {
        using var output = new MemoryStream();
        var names = new List<string>();
        names.Add("mimetype");
        names.AddRange(Order.Where(name => name != "mimetype"));
        names.AddRange(Files.Keys.Where(name => !names.Contains(name)));

        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in names)
            {
                if (!Files.TryGetValue(name, out var content))
                {
                    continue;
                }

                var entry = archive.CreateEntry(name, name == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        var data = output.ToArray();

        // 생성 결과 구조 검증
        using (var memory = new MemoryStream(data))
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Read))
        {
            foreach (var requiredName in RequiredPackageFiles)
            {
                if (archive.GetEntry(requiredName) is null)
                {
                    throw new HwpxTemplateError($"생성된 HWPX 패키지에 필수 파일이 없습니다: {requiredName}");
                }
            }

            foreach (var name in XmlNames())
            {
                var entry = archive.GetEntry(name);
                if (entry is null)
                {
                    continue;
                }

                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                XDocument.Parse(reader.ReadToEnd(), LoadOptions.PreserveWhitespace);
            }
        }

        if (outputPath is not null)
        {
            File.WriteAllBytes(outputPath, data);
        }

        return data;
    }
}

/// <summary>패키지 검증 결과.</summary>
public sealed record HwpxValidationResult(int Sections, int Files, IReadOnlyList<string> Placeholders, int FileSize);

/// <summary>플레이스홀더 규칙 ({{TOKEN}}).</summary>
public static class HwpxPlaceholder
{
    private static readonly Regex Pattern = new(@"\{\{([A-Z0-9_]+)\}\}", RegexOptions.Compiled);

    public const string RepeatPageStart = "CAFETERIA_REPEAT_PAGE_START";
    public const string RepeatPageEnd = "CAFETERIA_REPEAT_PAGE_END";

    public static IReadOnlyList<string> Find(string text)
        => Pattern.Matches(text).Select(m => m.Groups[1].Value).Distinct().OrderBy(v => v).ToList();

    public static string Token(string fieldName) => "{{" + fieldName + "}}";

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> RequiredByType { get; } =
        new Dictionary<string, IReadOnlySet<string>>
        {
            ["PRESERVATION_RECORD"] = new HashSet<string>
            {
                "B1_DATE_LABEL", "B1_SAMPLE_HOUR", "B1_SAMPLE_MINUTE", "B1_MANAGER", "B1_MENU_LIST", "B1_FREEZER_TEMP", "B1_DISCARD_DATETIME", "B1_COLLECTOR", "B1_COLLECTION_TIME",
                "B2_DATE_LABEL", "B2_SAMPLE_HOUR", "B2_SAMPLE_MINUTE", "B2_MANAGER", "B2_MENU_LIST", "B2_FREEZER_TEMP", "B2_DISCARD_DATETIME", "B2_COLLECTOR", "B2_COLLECTION_TIME",
                "B3_DATE_LABEL", "B3_SAMPLE_HOUR", "B3_SAMPLE_MINUTE", "B3_MANAGER", "B3_MENU_LIST", "B3_FREEZER_TEMP", "B3_DISCARD_DATETIME", "B3_COLLECTOR", "B3_COLLECTION_TIME",
            },
            ["COOKING_INSTRUCTION"] = new HashSet<string>
            {
                "DATE_LABEL",
                "LUNCH_MENU_1", "LUNCH_MENU_2", "LUNCH_MENU_3", "LUNCH_MENU_4", "LUNCH_MENU_5", "LUNCH_MENU_6", "LUNCH_MENU_7",
                "LUNCH_INGREDIENTS_1", "LUNCH_INGREDIENTS_2", "LUNCH_INGREDIENTS_3", "LUNCH_INGREDIENTS_4", "LUNCH_INGREDIENTS_5", "LUNCH_INGREDIENTS_6", "LUNCH_INGREDIENTS_7",
                "DINNER_MENU_1", "DINNER_MENU_2", "DINNER_MENU_3", "DINNER_MENU_4", "DINNER_MENU_5", "DINNER_MENU_6", "DINNER_MENU_7",
                "DINNER_INGREDIENTS_1", "DINNER_INGREDIENTS_2", "DINNER_INGREDIENTS_3", "DINNER_INGREDIENTS_4", "DINNER_INGREDIENTS_5", "DINNER_INGREDIENTS_6", "DINNER_INGREDIENTS_7",
            },
            ["MEAL_PLAN"] = new HashSet<string>
            {
                "PERIOD_TITLE",
                "ORIGIN_INFO", "NOTICE", "W1_LUNCH_TIME_INFO", "W2_LUNCH_TIME_INFO", "DINNER_TIME_INFO",
                "W1_D1_DATE", "W1_D1_LUNCH_MENU", "W1_D1_DINNER_MENU",
                "W1_D2_DATE", "W1_D2_LUNCH_MENU", "W1_D2_DINNER_MENU",
                "W1_D3_DATE", "W1_D3_LUNCH_MENU", "W1_D3_DINNER_MENU",
                "W1_D4_DATE", "W1_D4_LUNCH_MENU", "W1_D4_DINNER_MENU",
                "W1_D5_DATE", "W1_D5_LUNCH_MENU", "W1_D5_DINNER_MENU",
                "W2_D1_DATE", "W2_D1_LUNCH_MENU", "W2_D1_DINNER_MENU",
                "W2_D2_DATE", "W2_D2_LUNCH_MENU", "W2_D2_DINNER_MENU",
                "W2_D3_DATE", "W2_D3_LUNCH_MENU", "W2_D3_DINNER_MENU",
                "W2_D4_DATE", "W2_D4_LUNCH_MENU", "W2_D4_DINNER_MENU",
                "W2_D5_DATE", "W2_D5_LUNCH_MENU", "W2_D5_DINNER_MENU",
            },
        };
}
