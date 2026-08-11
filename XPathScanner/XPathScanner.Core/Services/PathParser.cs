using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XPathScanner.Core.Services
{
    public static class PathParser
    {
        // Regex bắt: TênControlType, theo sau là 0+ nhóm [@Key="Value"] hoặc [SốNguyên]
        private static readonly Regex SegmentPattern = new(
            @"^([A-Za-z]+)((?:\[[^\]]*\])*)$", RegexOptions.Compiled);

        private static readonly Regex AttributePattern = new(
            @"\[@([A-Za-z]+)=\\?[""']([^""'\\]*)\\?[""']\]", RegexOptions.Compiled);

        private static readonly Regex IndexPattern = new(
            @"\[(\d+)\]", RegexOptions.Compiled);

        // Input: "/Custom[@ClassName=\"PrintSetting\"]/Pane/Group[@ClassName=\"GroupBox\"]"
        // Output: danh sách PathSegment theo đúng thứ tự từ trái sang phải
        public static List<PathSegment> Parse(string path)
        {
            var result = new List<PathSegment>();

            if (string.IsNullOrWhiteSpace(path))
                return result;

            path = Normalize(path); // BƯỚC MỚI: chuẩn hoá trước khi parse

            // Bỏ dấu "/" đầu, tách theo "/"
            var rawSegments = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (var raw in rawSegments)
            {
                var match = SegmentPattern.Match(raw);
                if (!match.Success)
                {
                    throw new FormatException($"Không phân tích được segment: '{raw}' trong path '{path}'");
                }

                var segment = new PathSegment
                {
                    ControlType = match.Groups[1].Value
                };

                string bracketsPart = match.Groups[2].Value;

                foreach (Match attrMatch in AttributePattern.Matches(bracketsPart))
                {
                    segment.Attributes[attrMatch.Groups[1].Value] = attrMatch.Groups[2].Value;
                }

                // Nếu không có @thuộc_tính nào nhưng có [số] → đây là index
                if (segment.Attributes.Count == 0)
                {
                    var idxMatch = IndexPattern.Match(bracketsPart);
                    if (idxMatch.Success)
                        segment.Index = int.Parse(idxMatch.Groups[1].Value);
                }

                result.Add(segment);
            }

            return result;
        }

        // Chuyển các dấu ngoặc kép/đơn kiểu "thông minh" (cong) về dạng thẳng chuẩn,
        // để tránh lỗi parse khi người dùng copy-paste path từ nguồn có auto-correct.
        private static string Normalize(string input)
        {
            return input
                .Replace('\u201C', '"')  // “ → "
                .Replace('\u201D', '"')  // ” → "
                .Replace('\u2018', '\'') // ‘ → '
                .Replace('\u2019', '\'');// ’ → '
        }
    }
}
