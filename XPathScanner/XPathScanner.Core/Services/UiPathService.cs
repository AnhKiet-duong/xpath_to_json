using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;

namespace XPathScanner.Core.Services
{
    // BƯỚC 9: tiện ích xử lý "root anchor path" — dựng path từ phần tử được chọn,
    // và định vị lại phần tử theo path tương đối trong cây UIA sống.
    public static class UiPathService
    {
        public sealed class Segment
        {
            public string Type = "";
            public string? AutomationId;
            public string? Name;
            public string? ClassName;
            public int? Index;
        }

        public static List<Segment> ParseSegments(string relativePath)
        {
            var segs = new List<Segment>();
            if (string.IsNullOrWhiteSpace(relativePath)) return segs;

            foreach (var raw in relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var s = new Segment();
                int br = raw.IndexOf('[');
                if (br < 0)
                {
                    s.Type = raw;
                }
                else
                {
                    s.Type = raw.Substring(0, br);
                    string inner = raw.Substring(br + 1, raw.Length - br - 2);
                    var m = Regex.Match(inner, @"@(\w+)=\\?[""']([^""']+)\\?[""']");
                    if (m.Success)
                    {
                        switch (m.Groups[1].Value.ToLowerInvariant())
                        {
                            case "automationid": s.AutomationId = m.Groups[2].Value; break;
                            case "name": s.Name = m.Groups[2].Value; break;
                            case "classname": s.ClassName = m.Groups[2].Value; break;
                        }
                    }
                    else if (int.TryParse(inner, out int idx))
                    {
                        s.Index = idx;
                    }
                }
                segs.Add(s);
            }
            return segs;
        }

        // Định vị phần tử theo path tương đối tính từ root. Duyệt sâu (DFS) để chịu được
        // các node trung gian đã bị gộp (transparent) trong path.
        public static AutomationElement? Locate(AutomationElement root, string relativePath)
        {
            var segs = ParseSegments(relativePath);
            if (segs.Count == 0) return root;

            AutomationElement current = root;
            foreach (var seg in segs)
            {
                var found = FindDescendant(current, seg);
                if (found == null) return null;
                current = found;
            }
            return current;
        }

        // Dựng path tương đối từ mainWindow tới element (gộp node trung gian trong suốt),
        // dùng để điền vào ô "Root anchor path" khi người dùng pick 1 phần tử.
        public static string BuildRelativePath(AutomationElement mainWindow, AutomationElement element)
        {
            var chain = new List<AutomationElement>();
            AutomationElement? cur = element;
            while (cur != null && !ReferenceEquals(cur, mainWindow))
            {
                chain.Add(cur);
                try { cur = cur.Parent; } catch { break; }
            }
            chain.Reverse(); // từ trên xuống: child của mainWindow ... element

            var kept = new List<string>();
            for (int i = 0; i < chain.Count; i++)
            {
                var el = chain[i];
                bool isPicked = (i == chain.Count - 1);
                if (!isPicked && IsTransparent(el)) continue;
                kept.Add(BuildSegment(el));
            }

            if (kept.Count == 0) return "";
            return "/" + string.Join("/", kept);
        }

        private static AutomationElement? FindDescendant(AutomationElement parent, Segment seg)
        {
            AutomationElement[] children;
            try { children = parent.FindAllChildren(); }
            catch { return null; }

            if (seg.Index.HasValue)
            {
                if (seg.Index.Value < children.Length) return children[seg.Index.Value];
                return null;
            }

            foreach (var child in children)
            {
                if (Matches(child, seg)) return child;
            }
            foreach (var child in children)
            {
                var deep = FindDescendant(child, seg);
                if (deep != null) return deep;
            }
            return null;
        }

        private static bool Matches(AutomationElement el, Segment seg)
        {
            string ct = el.Properties.ControlType.IsSupported
                ? el.Properties.ControlType.Value.ToString()
                : "Element";
            if (!string.Equals(ct, seg.Type, StringComparison.OrdinalIgnoreCase)) return false;

            if (seg.AutomationId != null &&
                !string.Equals(el.Properties.AutomationId.ValueOrDefault ?? "", seg.AutomationId, StringComparison.Ordinal))
                return false;

            if (seg.Name != null &&
                !string.Equals(el.Properties.Name.ValueOrDefault ?? "", seg.Name, StringComparison.Ordinal))
                return false;

            if (seg.ClassName != null &&
                !string.Equals(el.Properties.ClassName.ValueOrDefault ?? "", seg.ClassName, StringComparison.Ordinal))
                return false;

            return true;
        }

        private static bool IsTransparent(AutomationElement el)
        {
            string id = el.Properties.AutomationId.ValueOrDefault ?? "";
            string name = el.Properties.Name.ValueOrDefault ?? "";
            if (!string.IsNullOrWhiteSpace(id) || !string.IsNullOrWhiteSpace(name)) return false;

            AutomationElement[] children;
            try { children = el.FindAllChildren(); }
            catch { return false; }
            return children.Length == 1;
        }

        private static string BuildSegment(AutomationElement el)
        {
            string ct = el.Properties.ControlType.IsSupported
                ? el.Properties.ControlType.Value.ToString()
                : "Element";
            string id = el.Properties.AutomationId.ValueOrDefault ?? "";
            string name = el.Properties.Name.ValueOrDefault ?? "";

            if (!string.IsNullOrWhiteSpace(id)) return $"{ct}[@AutomationId=\"{Escape(id)}\"]";
            if (!string.IsNullOrWhiteSpace(name)) return $"{ct}[@Name=\"{Escape(name)}\"]";
            return $"{ct}";
        }

        private static string Escape(string s) => s.Replace("\"", "\\\"");
    }
}
