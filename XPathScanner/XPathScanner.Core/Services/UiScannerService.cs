using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using XPathScanner.Core.Models;

namespace XPathScanner.Core.Services
{
    public class UiScannerService
    {
        private const int MaxCollapseChain = 10;
        private const int MaxDepth = 40; // giới hạn an toàn tổng độ sâu đệ quy

        private readonly ElementPathResolver _pathResolver = new();

        public List<string> Warnings { get; } = new();

        // Cho UI biết lần quét gần nhất có thực sự dùng Root anchor path hay không.
        public bool LastScanUsedRootAnchor { get; private set; } = false;

        // Điểm vào: quét 1 ứng dụng, trả về node gốc đại diện cho "màn hình" người dùng đặt tên.
        // screenName: người dùng nhập trên UI (VD: "PrintOut", "NavBar"...).
        // rootAnchorPath: tuỳ chọn, nếu người dùng muốn root là 1 khu vực cụ thể (có thể để rỗng).
        public UiNode ScanApplication(int processId, string screenName, string rootAnchorPath)
        {
            Warnings.Clear();
            LastScanUsedRootAnchor = false;

            using var automation = new UIA3Automation();
            var app = FlaUI.Core.Application.Attach(processId);
            var mainWindow = app.GetMainWindow(automation);

            if (mainWindow == null)
            {
                Warnings.Add("Không tìm thấy cửa sổ chính của ứng dụng.");
                return new UiNode { Name = screenName, Path = rootAnchorPath ?? "" };
            }

            // ĐIỂM QUÉT XUẤT PHÁT: mặc định là mainWindow (toàn bộ cửa sổ)
            AutomationElement scanStartElement = mainWindow;

            // NẾU người dùng có nhập Root anchor path → tìm đúng phần tử đó, KHÔNG fallback im lặng
            if (!string.IsNullOrWhiteSpace(rootAnchorPath))
            {
                // QUAN TRỌNG: resolve lại NGAY TRƯỚC khi quét để lấy AutomationElement còn sống
                // (không tái sử dụng object đã lưu từ lúc chọn root — có thể đã stale).
                var desktopRoot = automation.GetDesktop();
                var resolved = _pathResolver.Resolve(mainWindow, desktopRoot, rootAnchorPath, out string resolveError, out string resolveMode);

                if (resolved == null)
                {
                    Warnings.Add(
                        $"KHÔNG tìm thấy phần tử tương ứng với Root anchor path đã nhập. " +
                        $"Chi tiết: {resolveError} " +
                        $"→ ĐÃ DỪNG QUÉT (không tự động quét toàn bộ cửa sổ để tránh nhầm phạm vi). " +
                        $"Hãy kiểm tra lại chuỗi path hoặc để trống ô này nếu muốn quét toàn bộ cửa sổ.");

                    return new UiNode { Name = screenName, Path = rootAnchorPath ?? "" }; // KHÔNG quét gì thêm
                }

                scanStartElement = resolved;
                LastScanUsedRootAnchor = true;
                Warnings.Add(resolveMode == "absolute"
                    ? "Đã tìm thấy phần tử gốc theo Root anchor path (kiểu TUYỆT ĐỐI từ Desktop), bắt đầu quét từ đó."
                    : "Đã tìm thấy phần tử gốc theo Root anchor path (kiểu TƯƠNG ĐỐI từ cửa sổ chính), bắt đầu quét từ đó.");
            }

            return ScanFromRoot(scanStartElement, screenName, rootAnchorPath ?? "");
        }

        // Quét toàn bộ cây con bắt đầu từ rootElement (đã resolve) về UiNode.
        // Tách riêng để vừa dùng được cho "quét toàn cửa sổ" lẫn "quét từ root anchor",
        // và để log rõ ràng số children + cảnh báo khi không có con (tránh children: [] im lặng).
        public UiNode ScanFromRoot(AutomationElement rootElement, string screenName, string rootPath)
        {
            // Luôn tạo root node với children RỖNG ban đầu, sẽ điền vào ngay sau đây
            var rootNode = new UiNode
            {
                Name = screenName,
                Path = rootPath ?? ""
            };

            if (rootElement == null)
            {
                Warnings.Add("Phần tử gốc null — không thể quét con.");
                return rootNode;
            }

            // BƯỚC BẮT BUỘC: lấy danh sách con trực tiếp của rootElement
            AutomationElement[] children;
            try
            {
                children = rootElement.FindAllChildren();
            }
            catch (Exception ex)
            {
                Warnings.Add($"Lỗi khi đọc children của phần tử gốc: {ex.Message}");
                return rootNode; // trả về root KHÔNG con, nhưng có log lý do rõ ràng
            }

            Warnings.Add($"Tìm thấy {children.Length} phần tử con trực tiếp của root.");

            if (children.Length == 0)
            {
                Warnings.Add(
                    "CẢNH BÁO: FindAllChildren() trả về 0 phần tử. Khả năng cao: " +
                    "(1) phần tử gốc đã 'stale' — hãy thử chọn lại phần tử ngay trước khi quét, " +
                    "(2) ứng dụng đích đang ở trạng thái khác lúc chọn root (đã đổi tab/đóng panel), " +
                    "hoặc (3) phần tử thực sự không có con (kiểm tra lại bằng Inspect.exe để xác nhận).");
            }

            // BƯỚC BẮT BUỘC: đệ quy quét từng con, depth PHẢI reset về 0 tại đây
            foreach (var child in children)
            {
                var resolvedNode = ResolveNode(child, prefix: "", depth: 0);
                if (resolvedNode != null)
                    rootNode.Children.Add(resolvedNode);
            }

            Warnings.Add($"Quét hoàn tất, tạo được {rootNode.Children.Count} node con cấp 1.");

            return rootNode;
        }

        // Đệ quy: trả về 1 UiNode duy nhất đại diện cho "nhánh" bắt đầu từ element.
        // Nếu element "trong suốt" (xem quy tắc 5.1) → gộp segment vào prefix, đệ quy tiếp.
        private UiNode? ResolveNode(AutomationElement element, string prefix, int depth)
        {
            if (depth >= MaxDepth)
            {
                Warnings.Add("Đạt độ sâu tối đa khi quét, dừng nhánh này.");
                return null;
            }

            string automationId;
            string name;
            string controlType;

            try
            {
                automationId = element.Properties.AutomationId.ValueOrDefault ?? "";
                name = element.Properties.Name.ValueOrDefault ?? "";
                controlType = element.Properties.ControlType.IsSupported
                    ? element.Properties.ControlType.Value.ToString()
                    : "Element";
            }
            catch
            {
                return null; // phần tử lỗi khi đọc property, bỏ qua an toàn
            }

            AutomationElement[] children;
            try
            {
                children = element.FindAllChildren();
            }
            catch
            {
                children = Array.Empty<AutomationElement>();
            }

            string segment = XPathBuilder.BuildSegment(element, 0);
            string currentPath = string.IsNullOrEmpty(prefix) ? "/" + segment : prefix + "/" + segment;

            bool isTransparent =
                string.IsNullOrWhiteSpace(automationId) &&
                string.IsNullOrWhiteSpace(name) &&
                children.Length == 1 &&
                depth < MaxCollapseChain;

            if (isTransparent)
            {
                // Gộp: không tạo node, đệ quy thẳng xuống đứa con duy nhất với prefix đã nối
                return ResolveNode(children[0], currentPath, depth + 1);
            }

            // Không "trong suốt" → tạo node thật
            var node = new UiNode
            {
                Name = SuggestName(controlType, automationId, name),
                Path = currentPath,
                RawAutomationId = automationId,
                RawControlName = name,
                RawControlType = controlType
            };

            foreach (var child in children)
            {
                // path của node con là TƯƠNG ĐỐI theo node cha gần nhất có path khác rỗng
                // (đúng quy tắc 2 trong phần 0: không cộng dồn path cha)
                var childNode = ResolveNode(child, "", depth + 1);
                if (childNode != null)
                    node.Children.Add(childNode);
            }

            return node;
        }

        private string SuggestName(string controlType, string automationId, string name)
        {
            string? suffix = !string.IsNullOrWhiteSpace(automationId) ? automationId
                            : !string.IsNullOrWhiteSpace(name) ? name
                            : null;

            if (suffix == null)
            {
                Warnings.Add($"Phần tử loại {controlType} không có AutomationId/Name — cần đổi tên thủ công.");
                suffix = "Unnamed";
            }

            suffix = SanitizeName(suffix);

            return controlType switch
            {
                "Button" => $"Click_{suffix}",
                "CheckBox" => $"CheckBox_{suffix}",
                "Edit" => $"Input_{suffix}",
                "ComboBox" => $"Select_{suffix}",
                "RadioButton" => $"Radio_{suffix}",
                "Tab" => $"Tab_{suffix}",
                "TabItem" => $"Tab_{suffix}",
                _ => suffix
            };
        }

        private string SanitizeName(string input)
        {
            // Chỉ giữ chữ, số, gạch dưới — thay các ký tự khác bằng "_"
            return Regex.Replace(input, @"[^a-zA-Z0-9_]", "_");
        }
    }
}
