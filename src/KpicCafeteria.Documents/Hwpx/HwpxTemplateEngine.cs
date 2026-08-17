using System.Xml.Linq;

namespace KpicCafeteria.Documents.Hwpx;

/// <summary>
/// HWPX 템플릿 엔진.
/// 기존 hwpx_engine.py HwpxTemplateEngine에 대응.
/// </summary>
public sealed class HwpxTemplateEngine
{
    private const int LeftParaPrId = 100;
    private const int BlueCharPrId = 100;

    public HwpxPackage Package { get; }

    public HwpxTemplateEngine(HwpxPackage package)
    {
        Package = package;
    }

    public static HwpxTemplateEngine LoadTemplate(string templatePath)
    {
        var engine = new HwpxTemplateEngine(HwpxPackage.Load(templatePath));
        engine.ValidatePackage(allowRemainingPlaceholders: true);
        return engine;
    }

    public static HwpxTemplateEngine LoadTemplateBytes(byte[] content, string? sourceName = null)
    {
        var engine = new HwpxTemplateEngine(HwpxPackage.LoadBytes(content, sourceName));
        engine.ValidatePackage(allowRemainingPlaceholders: true);
        return engine;
    }

    // ---- 필드 치환 ----

    public HwpxTemplateEngine SetField(string fieldName, object? value, string? sectionName = null)
    {
        var token = HwpxPlaceholder.Token(fieldName);
        var replacement = value is null ? "" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        var targetSections = sectionName is null ? Package.SectionNames() : [sectionName];
        foreach (var name in targetSections)
        {
            if (!Package.Files.ContainsKey(name))
            {
                continue;
            }

            var root = Package.ReadXml(name);
            if (root.Root is not null && ReplacePlaceholderInRoot(root.Root, token, replacement))
            {
                Package.WriteXml(name, root);
            }
        }

        return this;
    }

    /// <summary>
    /// 메뉴 목록 필드 치환. hp:t 내부에 XText + hp:lineBreak mixed content로 줄바꿈을 생성한다.
    /// 일반 SetField와 달리 \r/\n을 사용하지 않고 실제 XML 요소로 lineBreak를 만든다.
    /// </summary>
    public HwpxTemplateEngine SetMultilineField(string fieldName, object? value, string? sectionName = null)
    {
        var menus = ToMenuList(value);
        var token = HwpxPlaceholder.Token(fieldName);
        var targetSections = sectionName is null ? Package.SectionNames() : [sectionName];
        foreach (var name in targetSections)
        {
            if (!Package.Files.ContainsKey(name))
            {
                continue;
            }

            var root = Package.ReadXml(name);
            if (root.Root is not null && ReplaceMenuListInRoot(root.Root, token, menus))
            {
                Package.WriteXml(name, root);
            }
        }

        return this;
    }

    private static void SetFieldInElements(IReadOnlyList<XElement> roots, string fieldName, object? value)
    {
        var token = HwpxPlaceholder.Token(fieldName);
        var replacement = value is null ? "" : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var root in roots)
        {
            ReplacePlaceholderInRoot(root, token, replacement);
        }
    }

    /// <summary>
    /// 반복 페이지 내에서 메뉴 목록 필드를 lineBreak 요소로 치환한다.
    /// </summary>
    private static void SetMultilineFieldInElements(IReadOnlyList<XElement> roots, string fieldName, object? value)
    {
        var menus = ToMenuList(value);
        var token = HwpxPlaceholder.Token(fieldName);
        foreach (var root in roots)
        {
            ReplaceMenuListInRoot(root, token, menus);
        }
    }

    /// <summary>
    /// 다양한 입력 타입에서 메뉴 문자열 목록을 추출하고 정규화한다.
    /// - IEnumerable<string>: 각 항목을 Trim, CR/LF 제거, 빈 항목 제거
    /// - string: \r/\n/\r\n 기준으로 분할 후 동일하게 정규화
    /// </summary>
    private static IReadOnlyList<string> ToMenuList(object? value)
    {
        return value switch
        {
            null => [],
            string s => CleanSegments(s.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)),
            IEnumerable<string> items => CleanSegments(items),
            IEnumerable<object> items => CleanSegments(items.Select(item => item?.ToString() ?? "")),
            _ => CleanSegments([value.ToString() ?? ""]),
        };

        static IReadOnlyList<string> CleanSegments(IEnumerable<string> source)
            => source
                .Select(s => (s ?? "").Replace("\r", "").Replace("\n", "").Trim())
                .Where(s => s.Length > 0)
                .ToList();
    }

    /// <summary>
    /// hp:t 요소 내부에 메뉴 세그먼트와 lineBreak를 mixed content로 배치한다.
    /// - 첫 세그먼트 앞에는 lineBreak를 두지 않는다
    /// - 마지막 세그먼트 뒤에도 lineBreak를 두지 않는다
    /// - 빈 세그먼트는 건너뛴다
    /// 결과: XText("menu1") + XElement(lineBreak) + XText("menu2") + ...
    /// </summary>
    private static void ApplyMenuListToText(XElement textElement, IReadOnlyList<string> menus)
    {
        if (menus.Count == 0)
        {
            textElement.Value = "";
            return;
        }

        if (menus.Count == 1)
        {
            textElement.Value = menus[0];
            return;
        }

        textElement.RemoveNodes();
        textElement.Add(new XText(menus[0]));
        for (var i = 1; i < menus.Count; i++)
        {
            textElement.Add(new XElement(HwpxNamespaces.HpNs + "lineBreak"));
            textElement.Add(new XText(menus[i]));
        }
    }

    // ---- 조리지시서 스타일 ----

    /// <summary>
    /// header.xml에 LEFT 정렬 paraPr와 파란색 charPr를 없으면 추가한다.
    /// 기존 ensureCookingStyles에 대응. (left_para_pr_id, blue_char_pr_id) 반환.
    /// </summary>
    public (int LeftParaPrId, int BlueCharPrId) EnsureCookingStyles()
    {
        const string headerName = "Contents/header.xml";
        if (!Package.Files.TryGetValue(headerName, out var content))
        {
            return (16, 8);
        }

        var root = XDocument.Parse(DecodeUtf8(content), LoadOptions.PreserveWhitespace);
        var leftId = LeftParaPrId;
        var blueId = BlueCharPrId;

        var paraProperties = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "paraProperties");
        if (paraProperties is not null)
        {
            var existing = paraProperties.Elements()
                .Where(c => c.Name.LocalName == "paraPr")
                .Select(c => c.Attribute("id")?.Value)
                .ToHashSet();
            if (!existing.Contains(leftId.ToString()))
            {
                var source = paraProperties.Elements().FirstOrDefault(c => c.Name.LocalName == "paraPr" && c.Attribute("id")?.Value == "16");
                if (source is not null)
                {
                    var newPr = new XElement(source);
                    newPr.SetAttributeValue("id", leftId.ToString());
                    foreach (var sub in newPr.Elements().Where(s => s.Name.LocalName == "align"))
                    {
                        sub.SetAttributeValue("horizontal", "LEFT");
                    }

                    paraProperties.Add(newPr);
                    var itemCnt = int.TryParse(paraProperties.Attribute("itemCnt")?.Value, out var count) ? count : 0;
                    paraProperties.SetAttributeValue("itemCnt", (itemCnt + 1).ToString());
                }
            }
        }

        var charProperties = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "charProperties");
        if (charProperties is not null)
        {
            var existing = charProperties.Elements()
                .Where(c => c.Name.LocalName == "charPr")
                .Select(c => c.Attribute("id")?.Value)
                .ToHashSet();
            if (!existing.Contains(blueId.ToString()))
            {
                var source = charProperties.Elements().FirstOrDefault(c => c.Name.LocalName == "charPr" && c.Attribute("id")?.Value == "8");
                if (source is not null)
                {
                    var newCp = new XElement(source);
                    newCp.SetAttributeValue("id", blueId.ToString());
                    newCp.SetAttributeValue("textColor", "#2E74B5");
                    charProperties.Add(newCp);
                    var itemCnt = int.TryParse(charProperties.Attribute("itemCnt")?.Value, out var count) ? count : 0;
                    charProperties.SetAttributeValue("itemCnt", (itemCnt + 1).ToString());
                }
            }
        }

        Package.Files[headerName] = EncodeUtf8(root);
        return (leftId, blueId);
    }

    /// <summary>
    /// 여러 줄 필드 + 비고를 별도 파란색 run으로 삽입.
    /// 메인 라인은 hp:t 내부에 lineBreak mixed content로, 비고는 별도 run의 hp:t에 lineBreak로 시작.
    /// </summary>
    public HwpxTemplateEngine SetMultilineFieldWithNoteColor(
        string fieldName, IReadOnlyList<string> lines, string noteText, int noteCharPrId, string? sectionName = null)
    {
        var cleanedLines = lines
            .Select(l => (l ?? "").Replace("\r", "").Replace("\n", "").Trim())
            .Where(l => l.Length > 0)
            .ToList();
        var cleanedNote = (noteText ?? "").Replace("\r", "").Replace("\n", "").Trim();
        if (string.IsNullOrEmpty(cleanedNote))
        {
            return SetMultilineField(fieldName, cleanedLines, sectionName);
        }

        var token = HwpxPlaceholder.Token(fieldName);
        var targetSections = sectionName is null ? Package.SectionNames() : [sectionName];
        foreach (var name in targetSections)
        {
            if (!Package.Files.ContainsKey(name))
            {
                continue;
            }

            var root = Package.ReadXml(name);
            var changed = false;
            foreach (var paragraph in IterParagraphs(root))
            {
                var texts = OwnTextNodes(paragraph);
                if (texts.Count == 0)
                {
                    continue;
                }

                var joined = string.Concat(texts.Select(t => t.Value));
                if (!joined.Contains(token, StringComparison.Ordinal))
                {
                    continue;
                }

                for (var i = 1; i < texts.Count; i++)
                {
                    texts[i].Value = "";
                }

                if (joined.Trim() == token)
                {
                    ApplyMenuListToText(texts[0], cleanedLines);
                }
                else
                {
                    texts[0].Value = joined.Replace(token, string.Join(" ", cleanedLines), StringComparison.Ordinal);
                }

                var firstRun = texts[0].Parent;
                if (firstRun is not null && firstRun.Name.LocalName == "run")
                {
                    var noteRun = new XElement(HwpxNamespaces.HpNs + "run",
                        new XAttribute("charPrIDRef", noteCharPrId.ToString()));
                    var noteT = new XElement(HwpxNamespaces.HpNs + "t");
                    noteT.Add(new XElement(HwpxNamespaces.HpNs + "lineBreak"));
                    noteT.Add(new XText(cleanedNote));
                    noteRun.Add(noteT);
                    firstRun.AddAfterSelf(noteRun);
                }

                changed = true;
            }

            if (changed)
            {
                Package.WriteXml(name, root);
            }
        }

        return this;
    }

    // ---- 반복 페이지 ----

    /// <summary>
    /// 반복 페이지 블록을 복제/로컬 치환/추가한다.
    /// 기존 applyRepeatPages에 대응. 마커가 없으면 false.
    /// </summary>
    public bool ApplyRepeatPages(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> pages,
        string sectionName = "Contents/section0.xml",
        bool applyPageBreakOnClone = true)
    {
        if (!Package.Files.ContainsKey(sectionName))
        {
            return false;
        }

        var root = Package.ReadXml(sectionName);
        var children = root.Root?.Nodes().ToList() ?? [];
        int? startIndex = null;
        int? endIndex = null;
        for (var index = 0; index < children.Count; index++)
        {
            if (children[index] is not XComment comment)
            {
                continue;
            }

            var text = comment.Value.Trim();
            if (text.StartsWith(HwpxPlaceholder.RepeatPageStart, StringComparison.Ordinal))
            {
                startIndex = index;
            }
            else if (text.StartsWith(HwpxPlaceholder.RepeatPageEnd, StringComparison.Ordinal))
            {
                endIndex = index;
            }
        }

        if (startIndex is null || endIndex is null)
        {
            return false;
        }

        if (endIndex <= startIndex)
        {
            throw new HwpxTemplateError("반복 페이지 마커 구간이 올바르지 않습니다.");
        }

        var templateBlock = children.Skip(startIndex.Value + 1).Take(endIndex.Value - startIndex.Value - 1).Select(CloneNode).ToList();
        if (templateBlock.Count == 0)
        {
            throw new HwpxTemplateError("반복 페이지 템플릿 블록이 비어 있습니다.");
        }

        for (var index = startIndex.Value; index <= endIndex.Value; index++)
        {
            children[index].Remove();
        }

        var effectivePages = pages.Count > 0 ? pages : [new Dictionary<string, object?>()];
        for (var pageIndex = 0; pageIndex < effectivePages.Count; pageIndex++)
        {
            var pageNodes = templateBlock.Select(CloneNode).ToList();
            if (pageIndex > 0 && applyPageBreakOnClone)
            {
                SetPageBreakOnFirstTopLevelParagraph(pageNodes);
            }

            BindLocalFields(pageNodes, effectivePages[pageIndex]);
            foreach (var node in pageNodes)
            {
                root.Root?.Add(node);
            }
        }

        Package.WriteXml(sectionName, root);
        return true;
    }

    private static void BindLocalFields(IReadOnlyList<XNode> roots, IReadOnlyDictionary<string, object?> fields)
    {
        foreach (var (fieldName, value) in fields)
        {
            if (value is IEnumerable<string> lines)
            {
                SetMultilineFieldInElements(roots.OfType<XElement>().ToList(), fieldName, lines.ToList());
            }
            else
            {
                SetFieldInElements(roots.OfType<XElement>().ToList(), fieldName, value);
            }
        }
    }

    private static void SetPageBreakOnFirstTopLevelParagraph(IReadOnlyList<XNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is XElement element && element.Name.LocalName == "p")
            {
                element.SetAttributeValue("pageBreak", "1");
                return;
            }
        }
    }

    // ---- 블록 복제/삭제 (cloneRow/cloneBlock/removeBlock) ----

    public HwpxTemplateEngine CloneRow(string marker, int count, string? sectionName = null, int occurrence = 0)
        => Clone(marker, count, ["tr"], sectionName, occurrence);

    public HwpxTemplateEngine CloneBlock(string marker, int count, string? sectionName = null, int occurrence = 0)
        => Clone(marker, count, ["tc", "tr", "p"], sectionName, occurrence);

    public HwpxTemplateEngine RemoveBlock(string marker, string? sectionName = null, int occurrence = 0)
        => Remove(marker, ["tc", "tr", "p"], sectionName, occurrence);

    private HwpxTemplateEngine Clone(string marker, int count, IReadOnlyList<string> targetTags, string? sectionName, int occurrence)
    {
        if (count < 1)
        {
            throw new HwpxTemplateError("복제 개수는 1개 이상이어야 합니다.");
        }

        if (count == 1)
        {
            return this;
        }

        var token = marker.StartsWith("{{", StringComparison.Ordinal) ? marker : HwpxPlaceholder.Token(marker);
        var sections = sectionName is null ? Package.SectionNames() : [sectionName];
        var found = FindTarget(token, sections, targetTags, occurrence);
        if (found is null)
        {
            throw new HwpxTemplateError($"복제할 블록을 찾을 수 없습니다: {marker}");
        }

        var (section, root, parent, target) = found.Value;
        var insertIndex = parent.Elements().ToList().IndexOf(target) + 1;
        for (var i = 0; i < count - 1; i++)
        {
            var clone = CloneNode(target);
            var elements = parent.Elements().ToList();
            elements.Insert(insertIndex, (XElement)clone);
            parent.RemoveNodes();
            foreach (var element in elements)
            {
                parent.Add(element);
            }

            insertIndex++;
        }

        Package.WriteXml(section, root);
        return this;
    }

    private HwpxTemplateEngine Remove(string marker, IReadOnlyList<string> targetTags, string? sectionName, int occurrence)
    {
        var token = marker.StartsWith("{{", StringComparison.Ordinal) ? marker : HwpxPlaceholder.Token(marker);
        var sections = sectionName is null ? Package.SectionNames() : [sectionName];
        var found = FindTarget(token, sections, targetTags, occurrence);
        if (found is null)
        {
            throw new HwpxTemplateError($"삭제할 블록을 찾을 수 없습니다: {marker}");
        }

        var (section, root, parent, target) = found.Value;
        target.Remove();
        Package.WriteXml(section, root);
        return this;
    }

    private (string Section, XDocument Root, XElement Parent, XElement Target)? FindTarget(
        string token, IReadOnlyList<string> sections, IReadOnlyList<string> targetTags, int occurrence)
    {
        var seen = 0;
        foreach (var sectionName in sections)
        {
            if (!Package.Files.ContainsKey(sectionName))
            {
                continue;
            }

            var root = Package.ReadXml(sectionName);
            var parents = BuildParentMap(root.Root);
            foreach (var node in root.Descendants().Where(n => n.Name.LocalName == "t"))
            {
                if (!(node.Value ?? "").Contains(token, StringComparison.Ordinal))
                {
                    continue;
                }

                var container = CandidateAncestor(node, parents, targetTags);
                if (container is null)
                {
                    continue;
                }

                if (seen == occurrence)
                {
                    if (!parents.TryGetValue(container, out var parent))
                    {
                        return null;
                    }

                    return (sectionName, root, parent, container);
                }

                seen++;
            }
        }

        return null;
    }

    private static Dictionary<XElement, XElement> BuildParentMap(XElement? root)
    {
        var mapping = new Dictionary<XElement, XElement>();
        if (root is null)
        {
            return mapping;
        }

        foreach (var parent in root.DescendantsAndSelf())
        {
            foreach (var child in parent.Elements())
            {
                mapping[child] = parent;
            }
        }

        return mapping;
    }

    private static XElement? CandidateAncestor(XElement node, Dictionary<XElement, XElement> parentMap, IReadOnlyList<string> targetTags)
    {
        var current = node;
        while (parentMap.TryGetValue(current, out var parent))
        {
            current = parent;
            if (targetTags.Contains(current.Name.LocalName))
            {
                return current;
            }
        }

        return null;
    }

    // ---- 검증/저장 ----

    public HwpxValidationResult ValidatePackage(string? documentType = null, IReadOnlySet<string>? requiredPlaceholders = null, bool allowRemainingPlaceholders = true)
        => Package.Validate(documentType, requiredPlaceholders, allowRemainingPlaceholders);

    public byte[] Save(string? outputPath = null, bool validate = true)
    {
        if (validate)
        {
            ValidatePackage(allowRemainingPlaceholders: false);
        }

        return Package.Save(outputPath);
    }

    // ---- 내부 헬퍼 ----

    /// <summary>paragraph에 직접 속하는 t 노드만 수집 (중첩 p는 제외).</summary>
    private static List<XElement> OwnTextNodes(XElement paragraph)
    {
        var result = new List<XElement>();
        Walk(paragraph);
        return result;

        void Walk(XElement element)
        {
            foreach (var child in element.Elements())
            {
                if (child.Name.LocalName == "p")
                {
                    continue;
                }

                if (child.Name.LocalName == "t")
                {
                    result.Add(child);
                }

                Walk(child);
            }
        }
    }

    private static List<XElement> IterParagraphs(XDocument root)
        => root.Descendants().Where(n => n.Name.LocalName == "p").ToList();

    private static bool ReplacePlaceholderInParagraph(XElement paragraph, string token, string? replacement)
    {
        var texts = OwnTextNodes(paragraph);
        if (texts.Count == 0)
        {
            return false;
        }

        var joined = string.Concat(texts.Select(t => t.Value));
        if (!joined.Contains(token, StringComparison.Ordinal))
        {
            return false;
        }

        texts[0].Value = joined.Replace(token, replacement, StringComparison.Ordinal);
        for (var i = 1; i < texts.Count; i++)
        {
            texts[i].Value = "";
        }

        return true;
    }

    /// <summary>
    /// 메뉴 목록 placeholder를 hp:t 내부 mixed content (XText + lineBreak)로 치환한다.
    /// 일반 ReplacePlaceholderInParagraph와 달리 \r/\n 대신 XML lineBreak 요소를 사용한다.
    /// </summary>
    private static bool ReplaceMenuListInParagraph(XElement paragraph, string token, IReadOnlyList<string> menus)
    {
        var texts = OwnTextNodes(paragraph);
        if (texts.Count == 0)
        {
            return false;
        }

        var joined = string.Concat(texts.Select(t => t.Value));
        if (!joined.Contains(token, StringComparison.Ordinal))
        {
            return false;
        }

        if (menus.Count == 0)
        {
            texts[0].Value = joined.Replace(token, "", StringComparison.Ordinal);
            for (var i = 1; i < texts.Count; i++)
            {
                texts[i].Value = "";
            }
            return true;
        }

        if (joined.Trim() == token)
        {
            for (var i = 1; i < texts.Count; i++)
            {
                texts[i].Value = "";
            }
            ApplyMenuListToText(texts[0], menus);
        }
        else
        {
            texts[0].Value = joined.Replace(token, string.Join(" ", menus), StringComparison.Ordinal);
            for (var i = 1; i < texts.Count; i++)
            {
                texts[i].Value = "";
            }
        }

        return true;
    }

    private static bool ReplaceMenuListInRoot(XElement root, string token, IReadOnlyList<string> menus)
    {
        var changed = false;
        foreach (var paragraph in root.DescendantsAndSelf().Where(n => n.Name.LocalName == "p"))
        {
            changed = ReplaceMenuListInParagraph(paragraph, token, menus) || changed;
        }

        return changed;
    }

    private static bool ReplacePlaceholderInRoot(XElement root, string token, string? replacement)
    {
        var changed = false;
        foreach (var paragraph in root.DescendantsAndSelf().Where(n => n.Name.LocalName == "p"))
        {
            changed = ReplacePlaceholderInParagraph(paragraph, token, replacement) || changed;
        }

        return changed;
    }

    /// <summary>XNode 깊은 복사 (XNode.DeepClone은 .NET에 없음).</summary>
    private static XNode CloneNode(XNode node) => node switch
    {
        XElement element => new XElement(element),
        XComment comment => new XComment(comment.Value),
        XCData cdata => new XCData(cdata.Value),
        XText text => new XText(text.Value),
        XProcessingInstruction pi => new XProcessingInstruction(pi.Target, pi.Data),
        _ => throw new NotSupportedException($"지원하지 않는 노드 형식: {node.NodeType}"),
    };

    private static string DecodeUtf8(byte[] content)
    {
        var offset = content.Length >= 3 && content[0] == 0xEF && content[1] == 0xBB && content[2] == 0xBF ? 3 : 0;
        return System.Text.Encoding.UTF8.GetString(content, offset, content.Length - offset);
    }

    private static byte[] EncodeUtf8(XDocument root)
    {
        using var stream = new MemoryStream();
        root.Save(stream, SaveOptions.DisableFormatting);
        return stream.ToArray();
    }
}
