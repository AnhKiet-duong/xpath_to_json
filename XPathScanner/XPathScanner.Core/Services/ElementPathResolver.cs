using System;
using System.Collections.Generic;
using System.Linq;
using FlaUI.Core.AutomationElements;

namespace XPathScanner.Core.Services
{
    public class ElementPathResolver
    {
        // mainWindow: cửa sổ chính của ứng dụng đích (dùng để thử kiểu path TƯƠNG ĐỐI)
        // desktopRoot: gốc Desktop ảo, lấy qua automation.GetDesktop() (dùng để thử kiểu path TUYỆT ĐỐI)
        // resolveMode: "relative" / "absolute" — cho biết kiểu path nào đã resolve thành công
        public AutomationElement? Resolve(
            AutomationElement mainWindow,
            AutomationElement desktopRoot,
            string path,
            out string errorMessage,
            out string resolveMode)
        {
            errorMessage = "";
            resolveMode = "";

            var segments = PathParser.Parse(path);
            if (segments.Count == 0)
            {
                errorMessage = "Path rỗng, không có gì để tìm.";
                return null;
            }

            // THỬ 1: coi path là TƯƠNG ĐỐI, gốc là mainWindow
            var relativeResult = ResolveFrom(mainWindow, segments, out string relativeError);
            if (relativeResult != null)
            {
                resolveMode = "relative";
                return relativeResult;
            }

            // THỬ 2: coi path là TUYỆT ĐỐI, gốc là Desktop
            // (áp dụng khi segment đầu tiên chính là desktopRoot hoặc tổ tiên của mainWindow)
            var absoluteResult = ResolveFrom(desktopRoot, segments, out string absoluteError);
            if (absoluteResult != null)
            {
                resolveMode = "absolute";
                return absoluteResult;
            }

            errorMessage =
                $"Không tìm thấy theo kiểu tương đối (từ cửa sổ chính): {relativeError} " +
                $"| Không tìm thấy theo kiểu tuyệt đối (từ Desktop): {absoluteError}";
            return null;
        }

        // Logic lõi: dò từng segment bắt đầu từ 1 root bất kỳ (mainWindow HOẶC desktopRoot).
        // Nếu CHÍNH root khớp segment đầu tiên (VD: desktopRoot chính là
        // Pane[@Name="Desktop 1"]) → coi segment đó đã "dùng" cho root, bắt đầu tìm trong
        // children từ segment kế tiếp — KHÔNG tìm kiếm trong con của chính nó (sẽ không bao giờ
        // thấy, vì nó là root chứ không phải con của root).
        // Duyệt XUỐNG SÂU (DFS) cho từng segment để chịu được path do BuildRelativePath sinh ra
        // đã "gộp bỏ" các container trong suốt (không AutomationId/Name, chỉ 1 con) — giữa 2
        // segment liên tiếp có thể có node trung gian bị lược bỏ.
        private AutomationElement? ResolveFrom(AutomationElement root, List<PathSegment> segments, out string errorMessage)
        {
            errorMessage = "";
            AutomationElement current = root;
            int startIndex = 0;

            // ---- SỬA MỚI: kiểm tra xem CHÍNH root có khớp segment đầu tiên không ----
            // Nếu có, coi như segment đó đã "dùng" cho chính root, KHÔNG tìm trong children
            // cho segment này — bắt đầu tìm children từ segment tiếp theo (index 1).
            if (segments.Count > 0 && MatchesSegment(root, segments[0]))
            {
                startIndex = 1;
            }
            // --------------------------------------------------------------------------

            for (int i = startIndex; i < segments.Count; i++)
            {
                var segment = segments[i];

                var found = FindDescendant(current, segment);
                if (found == null)
                {
                    errorMessage = $"Không tìm thấy phần tử khớp segment '{segment.ControlType}" +
                                    (segment.Attributes.Count > 0
                                        ? "[" + string.Join(",", segment.Attributes.Select(a => $"@{a.Key}=\"{a.Value}\"")) + "]"
                                        : "") + "'.";
                    return null;
                }

                current = found;
            }

            return current;
        }

        // Kiểm tra 1 element có khớp mô tả của 1 segment hay không (dùng cho việc so khớp
        // CHÍNH root, khác với so khớp trong danh sách children).
        private bool MatchesSegment(AutomationElement element, PathSegment segment)
        {
            if (!MatchesControlType(element, segment.ControlType))
                return false;

            if (segment.Attributes.Count > 0)
                return MatchesAllAttributes(element, segment.Attributes);

            // Segment không có thuộc tính cụ thể (chỉ có ControlType, hoặc có Index) —
            // không đủ căn cứ để khẳng định "chính root" khớp segment này, coi là KHÔNG khớp
            // để thuật toán vẫn tìm trong children như bình thường (an toàn hơn).
            return false;
        }

        // Tìm segment trong cây con của parent: khớp children trực tiếp trước, nếu không có
        // thì đệ quy xuống sâu để vượt qua node trung gian bị gộp (container trong suốt).
        private AutomationElement? FindDescendant(AutomationElement parent, PathSegment segment)
        {
            AutomationElement[] children;
            try
            {
                children = parent.FindAllChildren();
            }
            catch
            {
                return null;
            }

            var candidates = children.Where(c => MatchesControlType(c, segment.ControlType)).ToArray();

            AutomationElement? found = null;

            if (segment.Attributes.Count > 0)
            {
                found = candidates.FirstOrDefault(c => MatchesAllAttributes(c, segment.Attributes));
            }
            else if (segment.Index.HasValue)
            {
                found = segment.Index.Value < candidates.Length ? candidates[segment.Index.Value] : null;
            }
            else
            {
                // Không có thuộc tính lẫn index → lấy phần tử đầu tiên cùng ControlType
                found = candidates.FirstOrDefault();
            }

            if (found != null)
                return found;

            // Không khớp ở children trực tiếp → đệ quy xuống sâu để vượt qua node trung gian bị gộp
            foreach (var child in children)
            {
                var deep = FindDescendant(child, segment);
                if (deep != null)
                    return deep;
            }

            return null;
        }

        private bool MatchesControlType(AutomationElement element, string expectedControlType)
        {
            try
            {
                if (!element.Properties.ControlType.IsSupported) return false;
                return element.Properties.ControlType.Value.ToString() == expectedControlType;
            }
            catch
            {
                return false;
            }
        }

        private bool MatchesAllAttributes(AutomationElement element, Dictionary<string, string> attributes)
        {
            foreach (var kv in attributes)
            {
                if (GetAttributeValue(element, kv.Key) != kv.Value)
                    return false;
            }
            return true;
        }

        private string GetAttributeValue(AutomationElement element, string attributeName)
        {
            try
            {
                return attributeName switch
                {
                    "AutomationId" => element.Properties.AutomationId.ValueOrDefault ?? "",
                    "Name" => element.Properties.Name.ValueOrDefault ?? "",
                    "ClassName" => element.Properties.ClassName.ValueOrDefault ?? "",
                    _ => ""
                };
            }
            catch
            {
                return "";
            }
        }
    }
}
