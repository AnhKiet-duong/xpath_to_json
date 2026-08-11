using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using XPathScanner.Core.Models;

namespace XPathScanner.Core.Services
{
    public class JsonMergeService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            // Mặc định System.Text.Json escape " thành \u0022, "+" thành \u002B...
            // Dùng encoder này để xuất dạng \" cho khớp văn phong file mẫu của người dùng.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static readonly Regex AutomationIdRegex = new(@"@AutomationId=\\?[""']([^""'\\]+)\\?[""']", RegexOptions.Compiled);
        private static readonly Regex NameRegex = new(@"@Name=\\?[""']([^""'\\]+)\\?[""']", RegexOptions.Compiled);

        // ---- Kết quả diff của lần Merge gần nhất (BƯỚC 9) ----
        public List<UiNode> AddedNodes { get; } = new();
        public List<PathChange> ChangedPaths { get; } = new();
        public List<UiNode> UnmatchedOldNodes { get; } = new();

        public UiNode? LoadIfExists(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            string content = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<UiNode>(content, JsonOptions);
        }

        public void Save(UiNode node, string filePath)
        {
            string json = JsonSerializer.Serialize(node, JsonOptions);
            File.WriteAllText(filePath, json);
        }

        // Ghi file <jsonFilePath>.diff.json mô tả thay đổi của lần Merge gần nhất.
        public void SaveDiff(string jsonFilePath)
        {
            var diff = new
            {
                updatedAt = DateTime.Now,
                added = AddedNodes,
                changedPath = ChangedPaths,
                unmatchedOld = UnmatchedOldNodes
            };

            string json = JsonSerializer.Serialize(diff, JsonOptions);
            File.WriteAllText(jsonFilePath + ".diff.json", json);
        }

        // Trích khoá so khớp từ chuỗi path (dùng cho CẢ node cũ đọc từ file lẫn node mới quét).
        // Ưu tiên AutomationId, sau đó Name. Nếu path rỗng hoặc không trích được → trả về "".
        public string ExtractKey(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";

            var idMatch = AutomationIdRegex.Match(path);
            if (idMatch.Success) return "id::" + idMatch.Groups[1].Value;

            var nameMatch = NameRegex.Match(path);
            if (nameMatch.Success) return "name::" + nameMatch.Groups[1].Value;

            return ""; // không trích được gì đáng tin cậy → coi như không so khớp được
        }

        public UiNode Merge(UiNode? oldNode, UiNode newNode)
        {
            AddedNodes.Clear();
            ChangedPaths.Clear();
            UnmatchedOldNodes.Clear();

            if (oldNode == null) return newNode;

            var merged = new UiNode
            {
                Name = oldNode.Name,               // giữ tên cũ do người dùng đặt
                Path = string.IsNullOrEmpty(newNode.Path) ? oldNode.Path : newNode.Path,
                Children = MergeChildren(oldNode.Children, newNode.Children)
            };

            return merged;
        }

        // Xoá các node nằm trong toRemove khỏi cây gốc root. Trả về số node đã xoá.
        public int RemoveNodes(UiNode root, ISet<UiNode> toRemove)
        {
            int removed = 0;
            root.Children.RemoveAll(child =>
            {
                if (toRemove.Contains(child)) { removed++; return true; }
                removed += RemoveNodes(child, toRemove);
                return false;
            });
            return removed;
        }

        private List<UiNode> MergeChildren(List<UiNode> oldChildren, List<UiNode> newChildren)
        {
            var result = new List<UiNode>();
            var matchedOldIndexes = new HashSet<int>();

            // Gom old children theo key để tra cứu nhanh (bỏ qua key rỗng)
            var oldByKey = new Dictionary<string, (UiNode node, int index)>();
            for (int i = 0; i < oldChildren.Count; i++)
            {
                string key = ExtractKey(oldChildren[i].Path);
                if (!string.IsNullOrEmpty(key) && !oldByKey.ContainsKey(key))
                    oldByKey[key] = (oldChildren[i], i);
            }

            foreach (var newChild in newChildren)
            {
                string key = ExtractKey(newChild.Path);

                if (!string.IsNullOrEmpty(key) && oldByKey.TryGetValue(key, out var oldMatch))
                {
                    matchedOldIndexes.Add(oldMatch.index);

                    // BƯỚC 9: ghi nhận node đổi path
                    if (oldMatch.node.Path != newChild.Path)
                    {
                        ChangedPaths.Add(new PathChange
                        {
                            Name = oldMatch.node.Name,
                            OldPath = oldMatch.node.Path,
                            NewPath = newChild.Path
                        });
                    }

                    result.Add(new UiNode
                    {
                        Name = oldMatch.node.Name,   // giữ tên cũ
                        Path = newChild.Path,         // cập nhật path mới nhất
                        Children = MergeChildren(oldMatch.node.Children, newChild.Children)
                    });
                }
                else
                {
                    // BƯỚC 9: phần tử hoàn toàn mới
                    AddedNodes.Add(newChild);
                    result.Add(newChild);
                }
            }

            // Thêm lại các node cũ KHÔNG match được (kể cả path rỗng / node viết tay)
            for (int i = 0; i < oldChildren.Count; i++)
            {
                if (!matchedOldIndexes.Contains(i))
                {
                    // BƯỚC 9: ghi nhận node cũ không còn match — để người dùng xem xét dọn.
                    // CHỈ flag node có key (path không rỗng). Node viết tay (path rỗng) luôn
                    // được giữ lại im lặng, không bao giờ bị đề nghị xoá.
                    if (!string.IsNullOrEmpty(ExtractKey(oldChildren[i].Path)))
                        UnmatchedOldNodes.Add(oldChildren[i]);

                    result.Add(oldChildren[i]);
                }
            }

            return result;
        }
    }
}
