# KẾ HOẠCH TỔNG HỢP v1.0.0: Ứng dụng quét XPath UI Windows

> Đây là bản tổng hợp DUY NHẤT, thay thế toàn bộ các file rời rạc trước đó
> (`xpath-scanner-plan.md` + các file "yêu cầu bổ sung"). Tài liệu này mô tả **trạng thái CUỐI
> CÙNG, ĐÚNG** của toàn bộ code của bản v1.0.0 — **đã đối chiếu và khớp 100% với code hiện tại
> của app** (cập nhật lần cuối: sau khi triển khai thêm tính năng chọn gốc bằng trỏ chuột /
> tham số hoá / dọn node lỗi thời / kéo-thả / xuất .diff.json).
>
> Dùng tài liệu này để: build lại từ đầu, hoặc đối chiếu từng file với file tương ứng đang có
> để đảm bảo khớp 100%. Nếu sửa code, hãy cập nhật lại tài liệu này theo đúng quy trình.

---

## PHẦN 0 — TỔNG QUAN

### Mục tiêu ứng dụng

Công cụ chạy trên Windows 10/11, giao diện WPF, cho phép:
1. Chọn 1 ứng dụng đang chạy trên máy.
2. Quét cây UI Automation (UIA) của ứng dụng đó, sinh ra XPath cho từng phần tử.
3. Giới hạn phạm vi quét vào 1 khu vực cụ thể (root anchor) — theo 3 cách:
   - **Nhập tay** chuỗi path (hỗ trợ cả kiểu tương đối từ cửa sổ chính lẫn tuyệt đối từ Desktop).
   - **Chọn trực tiếp trên TreeView** đã quét (nút "Đặt node đang chọn làm Root").
   - **Trỏ chuột** vào phần tử trong ứng dụng đích rồi nhấn phím tắt **Ctrl+R** (giống Inspect.exe).
4. Xuất kết quả ra file JSON dạng cây đệ quy `{name, path, children}` — đúng theo văn phong
   các file tài liệu automation hiện có của người dùng (VD: `KICNavBar.json`, `KICPrintOut.json`...).
5. Cho phép quét lại và **cập nhật gia tăng** vào file JSON đã có, bảo toàn tên do người dùng
   chỉnh sửa thủ công, không tự động xoá bất kỳ node nào.
6. Hỗ trợ thao tác chỉnh sửa cây trước khi lưu: đổi tên (double-click), tham số hoá `{}`,
   kéo-thả sắp xếp, dọn node lỗi thời (chủ động, không tự động), xuất kèm file `.diff.json`.

### Công nghệ

| Thành phần | Lựa chọn |
|---|---|
| Nền tảng | Windows 10/11 |
| Ngôn ngữ | C# .NET 8 |
| UI Framework | WPF |
| Thư viện quét UI | FlaUI (`FlaUI.Core`, `FlaUI.UIA3`) |
| Lưu trữ | JSON (`System.Text.Json`) |

### Cấu trúc thư mục cuối cùng

```
XPathScanner/
├── XPathScanner.sln
├── XPathScanner.App/                  (WPF - giao diện)
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── TreeNodeTag.cs
│   ├── RenameDialog.xaml / RenameDialog.xaml.cs
│   ├── CleanupDialog.xaml / CleanupDialog.xaml.cs
│   ├── Assets/
│   │   └── app-icon.ico              (icon exe + cửa sổ; sinh từ PNG bằng make-icon.ps1)
│   └── XPathScanner.App.csproj
└── XPathScanner.Core/                 (Class Library - logic)
    ├── Models/
    │   ├── UiNode.cs
    │   └── PathChange.cs
    ├── Services/
    │   ├── ProcessListService.cs
    │   ├── PathSegment.cs
    │   ├── PathParser.cs
    │   ├── XPathBuilder.cs
    │   ├── ElementPathResolver.cs
    │   ├── UiScannerService.cs
    │   ├── UiPathService.cs
    │   └── JsonMergeService.cs
    └── XPathScanner.Core.csproj
```

### Build & Publish

- **Build (dev):** `dotnet build XPathScanner\XPathScanner.sln`
- **Publish exe chạy máy khác (self-contained single-file):** file `build-exe.bat` ở thư mục gốc
  dự án, output ra `publish\XPathScanner.App.exe` (~72 MB, gói luôn .NET 8 runtime, máy đích
  không cần cài gì; máy đích phải là Windows 64-bit — nếu 32-bit sửa `win-x64` thành `win-x86`).

---

## PHẦN 1 — SCHEMA JSON OUTPUT (đọc kỹ trước khi code, chi phối toàn bộ thiết kế)

Toàn bộ output của ứng dụng phải khớp đúng schema các file mẫu thật do người dùng cung cấp
(`KICNavBar.json`, `KICPrintOut.json`, `KICSettings.json`, `KICRunProfile.json`,
`KICProcessWindow.json`, `KICDataSearch.json`):

```json
{
    "name": "TênMànHình_hoặc_TênHànhĐộng",
    "path": "đường dẫn XPath tương đối, có thể để rỗng",
    "children": [ /* mảng các node con, đệ quy đúng schema này */ ]
}
```

### Quy tắc bắt buộc:

1. **Mỗi file JSON = 1 màn hình/tính năng** của ứng dụng. Node gốc có `name` = tên màn hình đó.
2. **`path` là tương đối**, nối tiếp vào `path` của **node cha gần nhất (trong cây JSON) có
   `path` khác rỗng**. Node cha có `path` rỗng chỉ là nhóm logic, không cộng dồn.
3. **Container trung gian không có `AutomationId`/`Name` riêng và chỉ có 1 con** phải được
   **gộp** vào path của node lá kế tiếp, không tạo node JSON riêng (xem thuật toán ở PHẦN 4.6).
4. **`path: ""` hợp lệ và phổ biến** — dùng cho root của file, và cho các "hành động logic"
   viết tay không gắn với 1 phần tử UI cụ thể (`no_action`, `tooltip`...).
5. **`name` mang tính ngữ nghĩa** (`Click_X`, `Select_X`, `CheckBox_X`, `Input_X`...), công cụ
   chỉ **gợi ý** tên mặc định theo `ControlType` + `AutomationId`/`Name`, người dùng **bắt
   buộc phải có khả năng đổi tên trực tiếp trên UI** trước khi lưu.
6. **KHÔNG được thêm field lạ** vào JSON export (`controlType`, `stale`, `firstSeen`...) — chỉ
   đúng 3 field `name`, `path`, `children`.
7. **Không bao giờ tự động xoá node khi merge/cập nhật** — kể cả node không còn khớp với phần
   tử UIA nào ở lần quét sau (có thể là node viết tay, hoặc UI tạm thời thay đổi).

### Schema file `.diff.json` (sinh kèm mỗi lần "Cập nhật JSON hiện có")

```json
{
    "updatedAt": "2026-08-11T10:58:00",
    "added": [ /* các node hoàn toàn mới ở lần quét này */ ],
    "changedPath": [
        { "name": "TênNode", "oldPath": "path cũ", "newPath": "path mới" }
    ],
    "unmatchedOld": [ /* node cũ không còn match được ở lần quét mới (chỉ node có path khác rỗng) */ ]
}
```

---

## PHẦN 2 — KHỞI TẠO SOLUTION

```bash
mkdir XPathScanner
cd XPathScanner
dotnet new sln -n XPathScanner

dotnet new classlib -n XPathScanner.Core -o XPathScanner.Core
dotnet new wpf -n XPathScanner.App -o XPathScanner.App

dotnet sln add XPathScanner.Core/XPathScanner.Core.csproj
dotnet sln add XPathScanner.App/XPathScanner.App.csproj

dotnet add XPathScanner.App/XPathScanner.App.csproj reference XPathScanner.Core/XPathScanner.Core.csproj

cd XPathScanner.Core
dotnet add package FlaUI.Core
dotnet add package FlaUI.UIA3
cd ..
```

**DoD:** `dotnet build` chạy được ở thư mục gốc, không lỗi.

> Lưu ý: `FlaUI.Core`/`FlaUI.UIA3` 5.0.0 nhắm .NET Framework nên khi build có cảnh báo
> NU1701 — đã biết, harmless, không cần xử lý.

---

## PHẦN 3 — MODEL

### 3.1. File: `XPathScanner.Core/Models/UiNode.cs`

```csharp
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XPathScanner.Core.Models
{
    public class UiNode
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("children")]
        public List<UiNode> Children { get; set; } = new();

        // ----- Các field dưới đây CHỈ dùng nội bộ khi quét/merge, -----
        // ----- KHÔNG được xuất ra file JSON (đánh dấu JsonIgnore). -----

        [JsonIgnore]
        public string RawAutomationId { get; set; } = "";

        [JsonIgnore]
        public string RawControlName { get; set; } = "";

        [JsonIgnore]
        public string RawControlType { get; set; } = "";
    }
}
```

**DoD:** Serialize 1 `UiNode` bất kỳ → JSON output chỉ có đúng 3 key `name`, `path`, `children`.

### 3.2. File: `XPathScanner.Core/Models/PathChange.cs`

> Bản ghi diff: node được merge (khớp key) nhưng path đã đổi giữa 2 lần quét.

```csharp
using System.Text.Json.Serialization;

namespace XPathScanner.Core.Models
{
    // Một bản ghi diff: node được merge (khớp key) nhưng path đã đổi giữa 2 lần quét.
    public class PathChange
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("oldPath")]
        public string OldPath { get; set; } = "";

        [JsonPropertyName("newPath")]
        public string NewPath { get; set; } = "";
    }
}
```

---

## PHẦN 4 — SERVICES (toàn bộ logic lõi)

### 4.1. File: `Services/ProcessListService.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace XPathScanner.Core.Services
{
    public class RunningAppInfo
    {
        public int ProcessId { get; set; }
        public string ProcessName { get; set; } = "";
        public string WindowTitle { get; set; } = "";
        public IntPtr MainWindowHandle { get; set; }
    }

    public class ProcessListService
    {
        // Trả về danh sách các tiến trình có cửa sổ chính (dùng để đổ vào ComboBox trên UI)
        public List<RunningAppInfo> GetRunningApps()
        {
            var result = new List<RunningAppInfo>();
            var processes = Process.GetProcesses();

            foreach (var p in processes)
            {
                try
                {
                    if (p.MainWindowHandle != IntPtr.Zero &&
                        !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                    {
                        result.Add(new RunningAppInfo
                        {
                            ProcessId = p.Id,
                            ProcessName = p.ProcessName,
                            WindowTitle = p.MainWindowTitle,
                            MainWindowHandle = p.MainWindowHandle
                        });
                    }
                }
                catch
                {
                    // Bỏ qua tiến trình không đọc được (thiếu quyền, đã thoát...)
                    continue;
                }
            }

            return result.OrderBy(x => x.ProcessName).ToList();
        }
    }
}
```

---

### 4.2. File: `Services/PathSegment.cs`

```csharp
using System.Collections.Generic;

namespace XPathScanner.Core.Services
{
    // Đại diện cho 1 mắt xích trong chuỗi path, ví dụ:
    // Custom[@ClassName="PrintSetting"]  →  ControlType="Custom", Attributes={ClassName: "PrintSetting"}
    public class PathSegment
    {
        public string ControlType { get; set; } = "";
        public Dictionary<string, string> Attributes { get; set; } = new();
        public int? Index { get; set; } // dùng khi segment dạng Button[2] (không có @thuộc_tính)
    }
}
```

---

### 4.3. File: `Services/PathParser.cs`

> Chuyển 1 chuỗi path (string) thành danh sách `PathSegment` — chiều ngược lại với
> `XPathBuilder` (sinh path từ element). Có chuẩn hoá dấu ngoặc kép "thông minh" (smart quotes)
> để tránh lỗi khi path bị dán từ nguồn có auto-correct (Word, Zalo, Messenger...).

```csharp
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
```

---

### 4.4. File: `Services/XPathBuilder.cs`

```csharp
using FlaUI.Core.AutomationElements;

namespace XPathScanner.Core.Services
{
    public static class XPathBuilder
    {
        // Sinh 1 đoạn xpath (segment) cho MỘT phần tử, ưu tiên AutomationId > Name > index.
        public static string BuildSegment(AutomationElement element, int siblingIndexInSameType)
        {
            string controlType = element.Properties.ControlType.IsSupported
                ? element.Properties.ControlType.Value.ToString()
                : "Element";

            string automationId = element.Properties.AutomationId.ValueOrDefault ?? "";
            string name = element.Properties.Name.ValueOrDefault ?? "";

            if (!string.IsNullOrWhiteSpace(automationId))
                return $"{controlType}[@AutomationId=\"{Escape(automationId)}\"]";

            if (!string.IsNullOrWhiteSpace(name))
                return $"{controlType}[@Name=\"{Escape(name)}\"]";

            return $"{controlType}[{siblingIndexInSameType}]";
        }

        private static string Escape(string input) => input.Replace("\"", "\\\"");
    }
}
```

---

### 4.5. File: `Services/ElementPathResolver.cs`

> Tìm ngược lại `AutomationElement` thật từ 1 chuỗi path. Hỗ trợ **CẢ 2 kiểu path**:
> - **Tương đối** — bắt đầu từ `mainWindow` (kiểu do chính app tự sinh khi quét).
> - **Tuyệt đối** — bắt đầu từ Desktop root (kiểu path đầy đủ copy từ công cụ khác, hoặc từ
>   1 lần quét trước bao gồm cả segment mô tả chính cửa sổ ứng dụng).
>
> Xử lý đúng trường hợp **segment đầu tiên mô tả CHÍNH root** truyền vào (không phải con của
> root) — ví dụ khi `desktopRoot` chính là phần tử `Pane[@Name="Desktop 1"]`.
>
> Mỗi segment được tìm **xuống sâu (DFS)** để chịu được path do `BuildRelativePath` sinh ra đã
> "gộp bỏ" các container trong suốt (không AutomationId/Name, chỉ 1 con) — giữa 2 segment liên
> tiếp có thể có node trung gian bị lược bỏ.
>
> `resolveMode` trả về kiểu path đã resolve thành công: `"relative"` (từ cửa sổ chính) hoặc
> `"absolute"` (từ Desktop) — dùng để log cho người dùng biết.

```csharp
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
```

---

### 4.6. File: `Services/UiScannerService.cs`

> Đây là service trung tâm. Bao gồm: quét toàn cửa sổ HOẶC quét giới hạn từ 1 root anchor path
> (resolve qua `ElementPathResolver`), với thuật toán **gộp node trung gian "trong suốt"**, và
> luôn **quét đầy đủ children** của root (dù root là `mainWindow` hay 1 phần tử con được resolve).

#### 4.6.1. Quy tắc gộp node trung gian

Một phần tử UIA là **"trong suốt"** (không tạo node JSON riêng, gộp segment vào node con) khi
thoả ĐỦ CẢ 3: không có `AutomationId`, không có `Name`, và chỉ có ĐÚNG 1 con. Giới hạn gộp tối
đa liên tiếp `MaxCollapseChain = 10` cấp. Tổng độ sâu đệ quy tối đa `MaxDepth = 40`.

#### 4.6.2. Quy tắc đặt tên gợi ý

| ControlType | Tiền tố gợi ý |
|---|---|
| Button | `Click_` |
| CheckBox | `CheckBox_` |
| Edit | `Input_` |
| ComboBox | `Select_` |
| RadioButton | `Radio_` |
| Tab / TabItem | `Tab_` |
| Còn lại | (không tiền tố, dùng thẳng AutomationId/Name) |

Hậu tố ưu tiên: `AutomationId` → `Name` → `Unnamed` (kèm cảnh báo cần đổi tên thủ công).
Ký tự đặc biệt trong tên được thay bằng `_` (`SanitizeName`).

#### 4.6.3. Code đầy đủ

```csharp
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
        // Nếu element "trong suốt" (xem quy tắc 4.6.1) → gộp segment vào prefix, đệ quy tiếp.
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
                // (đúng quy tắc 2 trong PHẦN 1: không cộng dồn path cha)
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
```

---

### 4.7. File: `Services/UiPathService.cs`

> Tiện ích cho tính năng **chọn phần tử làm gốc bằng trỏ chuột** (phím tắt Ctrl+R): dựng path
> tương đối từ `mainWindow` tới phần tử được chọn (có gộp node trung gian trong suốt), và định
> vị lại phần tử theo path tương đối trong cây UIA sống (DFS).

```csharp
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
```

---

### 4.8. File: `Services/JsonMergeService.cs`

> Đọc/ghi JSON + merge gia tăng: khoá so khớp trích trực tiếp từ chuỗi `path` (không cần field
> phụ). Giữ nguyên `name` cũ do người dùng đặt tay, cập nhật `path` nếu đổi, **không bao giờ
> tự xoá** node không match được. Kèm theo: theo dõi diff (thêm/đổi path/không match), xuất
> `.diff.json`, và xoá node lỗi thời **chủ động** theo lựa chọn của người dùng.

```csharp
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
```

---

# PHẦN 5 — Tầng giao diện WPF (`XPathScanner.App`)

> Toàn bộ mã nguồn dưới đây **khớp 100%** với code hiện tại trong thư mục
> `XPathScanner\XPathScanner.App\` (đã kiểm tra từng file). Gồm 6 file:
> `App.xaml.cs`, `TreeNodeTag.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`,
> `RenameDialog.xaml/.cs`, `CleanupDialog.xaml/.cs`.

### 5.1. File: `TreeNodeTag.cs`

> Gắn kèm vào `TreeViewItem.Tag` — lưu cả `UiNode` gốc lẫn "full path" đã ghép sẵn từ root
> xuống tới node, để dùng ngay khi người dùng chọn "Đặt làm Root".

```csharp
using XPathScanner.Core.Models;

namespace XPathScanner.App
{
    // Gắn kèm vào TreeViewItem.Tag — lưu cả UiNode gốc lẫn "full path" đã ghép sẵn từ root
    // xuống tới node này, để dùng ngay khi người dùng chọn "Đặt làm Root".
    public class TreeNodeTag
    {
        public UiNode Node { get; set; } = null!;
        public string FullPath { get; set; } = "";
    }
}
```

---

### 5.2. File: `MainWindow.xaml`

> Bố cục 6 hàng: chọn ứng dụng → tên màn hình + root anchor path → cây kết quả (double-click
> đổi tên, kéo-thả sắp xếp) → hàng nút thao tác → log. `SourceInitialized` dùng để đăng ký
> hotkey Ctrl+R.

```xml
<Window x:Class="XPathScanner.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="XPath Scanner" Height="680" Width="980"
        Icon="Assets/app-icon.ico"
        SourceInitialized="Window_SourceInitialized">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- Hàng 1: chọn ứng dụng -->
        <StackPanel Orientation="Horizontal" Grid.Row="0" Margin="0,0,0,8">
            <ComboBox x:Name="AppComboBox" Width="350" DisplayMemberPath="WindowTitle"/>
            <Button x:Name="RefreshButton" Content="Làm mới danh sách" Margin="10,0,0,0" Click="RefreshButton_Click"/>
        </StackPanel>

        <!-- Hàng 2: tên màn hình + root anchor path -->
        <StackPanel Orientation="Horizontal" Grid.Row="1" Margin="0,0,0,8">
            <TextBlock Text="Tên màn hình:" VerticalAlignment="Center"/>
            <TextBox x:Name="ScreenNameTextBox" Width="200" Margin="5,0,15,0"/>
            <TextBlock Text="Root anchor path:" VerticalAlignment="Center"/>
            <TextBox x:Name="RootAnchorTextBox" Width="300" Margin="5,0,0,0"/>
            <Button x:Name="PickAnchorButton" Content="Chọn gốc (Ctrl+R)" Margin="8,0,0,0" Click="PickAnchorButton_Click"/>
        </StackPanel>

        <!-- Hàng 3: cây kết quả quét (double-click đổi tên, kéo-thả sắp xếp) -->
        <TreeView x:Name="ResultTreeView" Grid.Row="3"
                  MouseDoubleClick="ResultTreeView_MouseDoubleClick"
                  AllowDrop="True"
                  PreviewMouseLeftButtonDown="ResultTreeView_PreviewMouseLeftButtonDown"
                  PreviewMouseMove="ResultTreeView_PreviewMouseMove"
                  DragOver="ResultTreeView_DragOver"
                  DragLeave="ResultTreeView_DragLeave"
                  Drop="ResultTreeView_Drop"/>

        <!-- Hàng 4: các nút thao tác -->
        <StackPanel Orientation="Horizontal" Grid.Row="4" Margin="0,10,0,10">
            <Button x:Name="ScanButton" Content="Quét" Width="90" Click="ScanButton_Click"/>
            <Button x:Name="SaveNewButton" Content="Lưu JSON mới" Width="120" Margin="10,0,0,0" Click="SaveNewButton_Click"/>
            <Button x:Name="UpdateExistingButton" Content="Cập nhật JSON hiện có" Width="170" Margin="10,0,0,0" Click="UpdateExistingButton_Click"/>
            <Button x:Name="ParameterizeButton" Content="Tham số hoá {}" Width="120" Margin="10,0,0,0" Click="ParameterizeButton_Click"/>
            <Button x:Name="SetAsRootButton" Content="Đặt node đang chọn làm Root" Width="190" Margin="10,0,0,0" Click="SetAsRootButton_Click"/>
            <Button x:Name="CleanupButton" Content="Xoá node lỗi thời" Width="130" Margin="10,0,0,0" Click="CleanupButton_Click"/>
        </StackPanel>

        <!-- Hàng 5: log -->
        <TextBox x:Name="LogTextBox" Grid.Row="5" Height="90" IsReadOnly="True" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto"/>
    </Grid>
</Window>
```

---

### 5.3. File: `MainWindow.xaml.cs`

> Toàn bộ logic giao diện: quét, lưu/cập nhật JSON (merge + diff + dọn node lỗi thời), chọn
> phần tử gốc bằng hotkey Ctrl+R (Win32 `RegisterHotKey`), tham số hoá `{}`, đặt node làm Root
> từ cây, kéo-thả sắp xếp cây, đổi tên node bằng double-click.

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Microsoft.Win32;
using XPathScanner.Core.Models;
using XPathScanner.Core.Services;

namespace XPathScanner.App
{
    public enum DropPosition { Before, After, Into }

    public partial class MainWindow : System.Windows.Window
    {
        private readonly ProcessListService _processListService = new();
        private readonly UiScannerService _scannerService = new();
        private readonly JsonMergeService _jsonMergeService = new();

        private UiNode? _lastScanResult;

        // ---- Trạng thái chọn phần tử làm gốc (hotkey) ----
        private HwndSource? _hwndSource;
        private bool _pickActive;
        private int _pickProcessId;
        private const int HotKeyId = 0x9001;
        private const int WmHotKey = 0x0312;
        private const uint ModControl = 0x0002;
        private const uint VkR = 0x52;

        // ---- Trạng thái kéo-thả ----
        private TreeViewItem? _dragSourceItem;
        private Point _dragStartPoint;
        private TreeViewItem? _dropTargetItem;
        private DropPosition _dropPosition;

        public MainWindow()
        {
            InitializeComponent();
            RefreshButton_Click(this, new RoutedEventArgs());
        }

        // ================= Win32 (hotkey + con trỏ) =================
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
            _hwndSource?.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotKey && wParam.ToInt32() == HotKeyId)
            {
                handled = true;
                CaptureElementUnderCursor();
            }
            return IntPtr.Zero;
        }

        // ================= Sự kiện UI =================
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            var apps = _processListService.GetRunningApps();
            AppComboBox.ItemsSource = apps;
            Log($"Tìm thấy {apps.Count} ứng dụng đang chạy.");
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppComboBox.SelectedItem is not RunningAppInfo selected)
            {
                Log("Vui lòng chọn một ứng dụng trước khi quét.");
                return;
            }

            if (string.IsNullOrWhiteSpace(ScreenNameTextBox.Text))
            {
                Log("Vui lòng nhập Tên màn hình trước khi quét.");
                return;
            }

            try
            {
                Log($"Đang quét: {selected.WindowTitle} ...");
                _lastScanResult = _scannerService.ScanApplication(
                    selected.ProcessId,
                    ScreenNameTextBox.Text.Trim(),
                    RootAnchorTextBox.Text.Trim());

                foreach (var w in _scannerService.Warnings)
                    Log("Cảnh báo: " + w);

                if (!string.IsNullOrWhiteSpace(RootAnchorTextBox.Text) && !_scannerService.LastScanUsedRootAnchor)
                {
                    Log("⚠️ CẢNH BÁO: Bạn đã nhập Root anchor path nhưng lần quét này KHÔNG dùng được " +
                        "nó (xem chi tiết lỗi ở log phía trên). Kết quả quét có thể KHÔNG đúng như mong đợi.");
                }
                else if (_scannerService.LastScanUsedRootAnchor)
                {
                    Log("✅ Đã quét đúng phạm vi Root anchor path đã nhập.");
                }

                RenderTree(_lastScanResult);
                Log("Quét hoàn tất.");
            }
            catch (Exception ex)
            {
                Log("Lỗi khi quét: " + ex.Message);
            }
        }

        private void SaveNewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScanResult == null)
            {
                Log("Chưa có dữ liệu quét. Hãy bấm Quét trước.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = ScreenNameTextBox.Text.Trim() + ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                _jsonMergeService.Save(_lastScanResult, dialog.FileName);
                Log("Đã lưu: " + dialog.FileName);
            }
        }

        private void UpdateExistingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScanResult == null)
            {
                Log("Chưa có dữ liệu quét. Hãy bấm Quét trước.");
                return;
            }

            var openDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (openDialog.ShowDialog() != true) return;

            var oldResult = _jsonMergeService.LoadIfExists(openDialog.FileName);
            var merged = _jsonMergeService.Merge(oldResult, _lastScanResult);
            _lastScanResult = merged;

            _jsonMergeService.Save(merged, openDialog.FileName);
            _jsonMergeService.SaveDiff(openDialog.FileName); // BƯỚC 9: xuất .diff.json
            Log("Đã cập nhật file: " + openDialog.FileName);
            Log($"Diff: thêm {_jsonMergeService.AddedNodes.Count}, đổi path {_jsonMergeService.ChangedPaths.Count}, node cũ không match {_jsonMergeService.UnmatchedOldNodes.Count}.");

            RenderTree(merged);

            // BƯỚC 9: đề xuất dọn node lỗi thời
            if (_jsonMergeService.UnmatchedOldNodes.Count > 0)
            {
                var cleanup = new CleanupDialog(_jsonMergeService.UnmatchedOldNodes) { Owner = this };
                if (cleanup.ShowDialog() == true && cleanup.SelectedNodes.Count > 0)
                {
                    int removed = _jsonMergeService.RemoveNodes(merged, cleanup.SelectedNodes);
                    _jsonMergeService.Save(merged, openDialog.FileName);
                    RenderTree(merged);
                    Log($"Đã xoá {removed} node lỗi thời và ghi lại file.");
                }
            }
        }

        // ================= Chọn phần tử làm gốc (BƯỚC 9) =================
        private void PickAnchorButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppComboBox.SelectedItem is not RunningAppInfo selected)
            {
                Log("Vui lòng chọn ứng dụng trước khi chọn phần tử gốc.");
                return;
            }

            if (_pickActive)
            {
                UnregisterHotKey();
                Log("Đã huỷ chọn phần tử gốc.");
                return;
            }

            if (_hwndSource == null || _hwndSource.Handle == IntPtr.Zero)
            {
                Log("Cửa sổ chưa sẵn sàng để đăng ký hotkey.");
                return;
            }

            if (RegisterHotKey(_hwndSource.Handle, HotKeyId, ModControl, VkR))
            {
                _pickActive = true;
                _pickProcessId = selected.ProcessId;
                PickAnchorButton.Content = "Huỷ chọn gốc";
                Log("Chế độ chọn gốc: di chuyển chuột lên phần tử trong app đích, nhấn Ctrl+R để chọn.");
            }
            else
            {
                Log("Không đăng ký được hotkey Ctrl+R (có thể bị app khác chiếm).");
            }
        }

        private void CaptureElementUnderCursor()
        {
            UnregisterHotKey(); // chỉ chọn 1 lần
            try
            {
                GetCursorPos(out var pt);
                using var automation = new UIA3Automation();
                var element = automation.FromPoint(new System.Drawing.Point(pt.X, pt.Y));
                if (element == null)
                {
                    Log("Không lấy được phần tử dưới con trỏ.");
                    return;
                }

                var app = FlaUI.Core.Application.Attach(_pickProcessId);
                var mainWindow = app.GetMainWindow(automation);
                if (mainWindow == null)
                {
                    Log("Không tìm thấy cửa sổ chính của app đã chọn.");
                    return;
                }

                int procId = element.Properties.ProcessId.ValueOrDefault;
                if (procId != 0 && procId != _pickProcessId)
                {
                    Log($"Phần tử dưới con trỏ thuộc process {procId}, không phải {_pickProcessId} — bỏ qua.");
                    return;
                }

                string path = UiPathService.BuildRelativePath(mainWindow, element);
                RootAnchorTextBox.Text = path;
                Log("Đã điền root anchor path: " + path);
            }
            catch (Exception ex)
            {
                Log("Lỗi khi chọn phần tử: " + ex.Message);
            }
            finally
            {
                _pickActive = false;
                PickAnchorButton.Content = "Chọn gốc (Ctrl+R)";
            }
        }

        private void UnregisterHotKey()
        {
            if (_hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
                UnregisterHotKey(_hwndSource.Handle, HotKeyId);
            _pickActive = false;
            PickAnchorButton.Content = "Chọn gốc (Ctrl+R)";
        }

        // ================= Tham số hoá {} (BƯỚC 9) =================
        private void ParameterizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultTreeView.SelectedItem is not TreeViewItem item || item.Tag is not TreeNodeTag tag)
            {
                Log("Hãy chọn 1 node trong cây trước khi Tham số hoá.");
                return;
            }

            var node = tag.Node;

            if (string.IsNullOrWhiteSpace(node.Path))
            {
                Log("Node này không có path (node hành động logic) — không thể tham số hoá.");
                return;
            }

            // Tham số hoá @AutomationId / @Name CUỐI CÙNG trong path (segment sâu nhất của node)
            var m = Regex.Match(node.Path, @"(@(?:AutomationId|Name))=\\?[""']([^""']+)\\?[""']\s*$");
            if (!m.Success)
            {
                Log("Path không chứa @AutomationId/@Name để tham số hoá.");
                return;
            }

            node.Path = node.Path.Substring(0, m.Index) + m.Groups[1].Value + "=\"{}\"";
            item.Header = string.IsNullOrEmpty(node.Path) ? node.Name : $"{node.Name}  ({node.Path})";
            Log("Đã tham số hoá: " + node.Path);
        }

        // ================= Đặt node đang chọn làm Root (BƯỚC 9) =================
        private void SetAsRootButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultTreeView.SelectedItem is not TreeViewItem selectedItem)
            {
                Log("Vui lòng chọn 1 node trên cây kết quả trước khi bấm nút này.");
                return;
            }

            if (selectedItem.Tag is not TreeNodeTag tag)
            {
                Log("Không đọc được thông tin node đã chọn.");
                return;
            }

            if (string.IsNullOrEmpty(tag.FullPath))
            {
                Log("Node này không có path riêng (là node nhóm logic, path rỗng) — " +
                    "không thể dùng làm Root anchor. Hãy chọn 1 node khác có path cụ thể.");
                return;
            }

            RootAnchorTextBox.Text = tag.FullPath;

            Log($"Đã điền Root anchor path từ node '{tag.Node.Name}':\n{tag.FullPath}\n" +
                "→ Bấm 'Quét' để quét lại (lấy dữ liệu mới nhất) bắt đầu từ node này.");
        }

        // ================= Dọn node lỗi thời (BƯỚC 9) =================
        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonMergeService.UnmatchedOldNodes.Count == 0)
            {
                Log("Không có node lỗi thời nào để dọn (chạy 'Cập nhật JSON hiện có' trước nếu cần).");
                return;
            }

            if (_lastScanResult == null) return;

            var dialog = new CleanupDialog(_jsonMergeService.UnmatchedOldNodes) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedNodes.Count > 0)
            {
                int removed = _jsonMergeService.RemoveNodes(_lastScanResult, dialog.SelectedNodes);
                RenderTree(_lastScanResult);
                Log($"Đã xoá {removed} node lỗi thời (bấm Lưu JSON mới / Cập nhật để ghi file).");
            }
        }

        // ================= Kéo-thả sắp xếp cây (BƯỚC 9) =================
        private void ResultTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSourceItem = FindTreeViewItemFromPoint(e.GetPosition(ResultTreeView));
            _dragStartPoint = e.GetPosition(this);
        }

        private void ResultTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSourceItem == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            Point pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (_dragSourceItem.Tag is not TreeNodeTag dragTag) return;
            var dragged = dragTag.Node;

            _dragSourceItem = null; // drag bắt đầu; drop sẽ xử lý bằng data
            DragDrop.DoDragDrop(ResultTreeView, dragged, DragDropEffects.Move);
        }

        private void ResultTreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (e.Data.GetData(typeof(UiNode)) is not UiNode) return;

            var item = FindTreeViewItemFromPoint(e.GetPosition(ResultTreeView));
            _dropTargetItem = item;
            if (item == null) return;

            _dropPosition = GetDropPosition(item, e.GetPosition(item));
            item.IsSelected = true; // phản hồi trực quan
            e.Effects = DragDropEffects.Move;
        }

        private void ResultTreeView_DragLeave(object sender, DragEventArgs e)
        {
            _dropTargetItem = null;
        }

        private void ResultTreeView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(UiNode)) is not UiNode dragged) return;
            if (_dropTargetItem?.Tag is not TreeNodeTag targetTag) return;
            var target = targetTag.Node;
            if (dragged == target) return;
            if (IsAncestor(dragged, target)) // không cho thả vào chính con của nó
            {
                Log("Không thể thả node vào chính nhánh con của nó.");
                return;
            }

            MoveNode(dragged, target, _dropPosition);
            _lastScanResult ??= dragged;
            RenderTree(_lastScanResult);
            Log($"Đã di chuyển node '{dragged.Name}'.");
        }

        private void MoveNode(UiNode node, UiNode target, DropPosition pos)
        {
            var nodeParent = FindParent(_lastScanResult, node);
            nodeParent?.Children.Remove(node);

            if (pos == DropPosition.Into)
            {
                target.Children.Add(node);
                return;
            }

            var targetParent = FindParent(_lastScanResult, target);
            if (targetParent == null)
            {
                _lastScanResult?.Children.Add(node);
                return;
            }

            int idx = targetParent.Children.IndexOf(target);
            if (idx < 0)
            {
                targetParent.Children.Add(node);
                return;
            }

            int insert = pos == DropPosition.Before ? idx : idx + 1;
            if (insert > targetParent.Children.Count) insert = targetParent.Children.Count;
            targetParent.Children.Insert(insert, node);
        }

        private DropPosition GetDropPosition(TreeViewItem item, Point posInItem)
        {
            double h = item.ActualHeight;
            if (h <= 0) return DropPosition.Into;
            if (posInItem.Y < h / 3) return DropPosition.Before;
            if (posInItem.Y > h * 2 / 3) return DropPosition.After;
            return DropPosition.Into;
        }

        private TreeViewItem? FindTreeViewItemFromPoint(Point p)
        {
            var hit = VisualTreeHelper.HitTest(ResultTreeView, p);
            if (hit == null) return null;

            DependencyObject? current = hit.VisualHit;
            while (current != null && current is not TreeViewItem && current != ResultTreeView)
                current = VisualTreeHelper.GetParent(current);

            return current as TreeViewItem;
        }

        private bool IsAncestor(UiNode node, UiNode target)
        {
            if (node == target) return true;
            foreach (var child in node.Children)
                if (IsAncestor(child, target)) return true;
            return false;
        }

        private UiNode? FindParent(UiNode? root, UiNode child)
        {
            if (root == null) return null;
            foreach (var c in root.Children)
            {
                if (c == child) return root;
                var p = FindParent(c, child);
                if (p != null) return p;
            }
            return null;
        }

        // ================= Render + rename =================
        private void RenderTree(UiNode root)
        {
            ResultTreeView.Items.Clear();
            // parentFullPath rỗng vì root luôn là điểm bắt đầu (full path của root = chính
            // Path của root, hoặc rỗng nếu quét toàn bộ cửa sổ không có root anchor)
            ResultTreeView.Items.Add(BuildTreeViewItem(root, parentFullPath: ""));
        }

        private TreeViewItem BuildTreeViewItem(UiNode node, string parentFullPath)
        {
            // Ghép full path: node.Path rỗng → full path = full path của cha (node nhóm logic,
            // không cộng thêm gì). node.Path khác rỗng → nối tiếp vào full path của cha.
            string fullPath = string.IsNullOrEmpty(node.Path)
                ? parentFullPath
                : parentFullPath + node.Path;

            var tag = new TreeNodeTag { Node = node, FullPath = fullPath };

            var item = new TreeViewItem
            {
                Header = string.IsNullOrEmpty(node.Path) ? node.Name : $"{node.Name}  ({node.Path})",
                Tag = tag
            };

            foreach (var child in node.Children)
                item.Items.Add(BuildTreeViewItem(child, fullPath));

            return item;
        }

        private void ResultTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultTreeView.SelectedItem is not TreeViewItem item) return;
            if (item.Tag is not TreeNodeTag tag) return;
            var node = tag.Node;

            var dialog = new RenameDialog(node.Name)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
            {
                node.Name = dialog.NewName.Trim();
                item.Header = string.IsNullOrEmpty(node.Path) ? node.Name : $"{node.Name}  ({node.Path})";
            }
        }

        private void Log(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            LogTextBox.ScrollToEnd();
        }
    }
}
```

---

### 5.4. File: `RenameDialog.xaml` + `RenameDialog.xaml.cs`

> Hộp thoại đổi tên node (mở bằng double-click trên cây kết quả). Nhập tên mới, bấm OK →
> `NewName` được set và `DialogResult = true`.

**RenameDialog.xaml:**

```xml
<Window x:Class="XPathScanner.App.RenameDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Đổi tên node" SizeToContent="WidthAndHeight"
        WindowStartupLocation="CenterOwner" ResizeMode="NoResize" ShowInTaskbar="False">
    <StackPanel Margin="12">
        <TextBlock x:Name="PromptText" Text="Nhập tên mới cho node:" Margin="0,0,0,6"/>
        <TextBox x:Name="NameTextBox" Width="280" Margin="0,0,0,10"/>
        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="OK" Width="70" IsDefault="True" Click="OkButton_Click" Margin="0,0,8,0"/>
            <Button Content="Huỷ" Width="70" IsCancel="True"/>
        </StackPanel>
    </StackPanel>
</Window>
```

**RenameDialog.xaml.cs:**

```csharp
using System.Windows;

namespace XPathScanner.App
{
    public partial class RenameDialog : Window
    {
        public string NewName { get; private set; } = "";

        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameTextBox.Text = currentName;
            NameTextBox.SelectAll();
            NameTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            NewName = NameTextBox.Text.Trim();
            DialogResult = true;
        }
    }
}
```

---

### 5.5. File: `CleanupDialog.xaml` + `CleanupDialog.xaml.cs`

> Hộp thoại liệt kê các node cũ không match trong lần merge gần nhất (`UnmatchedOldNodes`).
> Người dùng tích chọn node muốn xoá; `SelectedNodes` trả về tập node đã chọn. Mặc định tích
> hết; nút "Chọn tất cả" để tích lại nhanh.

**CleanupDialog.xaml:**

```xml
<Window x:Class="XPathScanner.App.CleanupDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Dọn node lỗi thời"
        Width="480" Height="420"
        WindowStartupLocation="CenterOwner"
        ShowInTaskbar="False">
    <Grid Margin="12">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0" TextWrapping="Wrap" Margin="0,0,0,8"
                   Text="Các node dưới đây không còn xuất hiện trong lần quét mới. Tích chọn để xoá (node viết tay sẽ luôn được giữ lại nếu không chọn)."/>

        <ListBox Grid.Row="1" x:Name="NodeListBox" BorderBrush="Gray" BorderThickness="1"/>

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
            <Button x:Name="SelectAllButton" Content="Chọn tất cả" Width="100" Margin="0,0,8,0" Click="SelectAllButton_Click"/>
            <Button Content="OK" Width="80" Margin="0,0,8,0" IsDefault="True" Click="OkButton_Click"/>
            <Button Content="Huỷ" Width="80" IsCancel="True"/>
        </StackPanel>
    </Grid>
</Window>
```

**CleanupDialog.xaml.cs:**

```csharp
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using XPathScanner.Core.Models;

namespace XPathScanner.App
{
    // BƯỚC 9: hộp thoại liệt kê các node cũ không match trong lần merge gần nhất.
    // Người dùng tích chọn node muốn xoá; SelectedNodes trả về tập node đã chọn.
    public partial class CleanupDialog : Window
    {
        private readonly List<UiNode> _nodes;

        public CleanupDialog(List<UiNode> nodes)
        {
            InitializeComponent();
            _nodes = nodes;

            foreach (var node in nodes)
            {
                var checkBox = new CheckBox
                {
                    Content = string.IsNullOrEmpty(node.Path) ? node.Name : $"{node.Name}  ({node.Path})",
                    Tag = node,
                    IsChecked = true,
                    Margin = new Thickness(2, 3, 2, 3)
                };
                NodeListBox.Items.Add(checkBox);
            }
        }

        public HashSet<UiNode> SelectedNodes { get; } = new();

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in NodeListBox.Items)
            {
                if (item is CheckBox checkBox)
                    checkBox.IsChecked = true;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedNodes.Clear();
            foreach (var item in NodeListBox.Items)
            {
                if (item is CheckBox { IsChecked: true } checkBox && checkBox.Tag is UiNode node)
                    SelectedNodes.Add(node);
            }
            DialogResult = true;
        }
    }
}
```

---

### 5.6. File: `App.xaml.cs`

```csharp
﻿using System.Configuration;
using System.Data;
using System.Windows;

namespace XPathScanner.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
}
```

---

# PHẦN 6 — Checklist kiểm tra (đối chiếu với app hiện tại)

> Checklist dưới đây phản ánh **đầy đủ tính năng app đang có** (đã cập nhật theo BƯỚC 9
> và các yêu cầu bổ sung: root tự khớp, chọn root từ cây, hotkey Ctrl+R).

### 6.1. Luồng cơ bản

- [x] Bấm "Làm mới danh sách" → liệt kê các ứng dụng có cửa sổ chính đang chạy (ComboBox hiển thị `WindowTitle`).
- [x] Chọn ứng dụng, nhập "Tên màn hình", bấm "Quét" → hiện cây `{name, path, children}` trong TreeView.
- [x] "Lưu JSON mới" → chọn file đích, ghi JSON (indent 4, escape `\"` kiểu người dùng).
- [x] "Cập nhật JSON hiện có" → chọn file JSON cũ, merge giữ tên cũ, cập nhật path mới, giữ node không match, xuất `<file>.diff.json`, log số liệu diff.

### 6.2. Root anchor path (3 cách chọn root)

- [x] Gõ tay chuỗi path (tương đối từ cửa sổ chính, hoặc tuyệt đối từ Desktop) vào ô "Root anchor path" → quét đúng phạm vi đó; resolve thử tương đối trước, tuyệt đối sau.
- [x] **Pick bằng chuột**: bấm "Chọn gốc (Ctrl+R)" → di chuột lên phần tử trong app đích → nhấn **Ctrl+R** → path tương đối được tự động điền vào ô Root anchor (kiểm tra process của phần tử để tránh pick nhầm app).
- [x] **Chọn từ cây**: chọn 1 node trên cây kết quả → bấm "Đặt node đang chọn làm Root" → full path (ghép từ root qua các segment) điền vào ô Root anchor; không tự quét lại (nguyên tắc không hành động ngầm).
- [x] Root anchor path khớp với chính node root (segment đầu mô tả root) → vẫn resolve được nhờ `MatchesSegment` + `startIndex=1`.
- [x] Path không resolve được → dừng quét, log lý do đầy đủ (gộp lỗi của cả 2 chế độ), KHÔNG fallback quét toàn cửa sổ.
- [x] Log phân biệt rõ chế độ đã dùng: "TUYỆT ĐỐI từ Desktop" / "TƯƠNG ĐỐI từ cửa sổ chính" (`resolveMode`); UI hiển thị ✅/⚠️ tương ứng.

### 6.3. Quét & gộp cây

- [x] Node trung gian "trong suốt" (không AutomationId, không Name, đúng 1 con, chuỗi ≤ 10) bị gộp vào path, không tạo node rác.
- [x] Độ sâu an toàn tối đa 40 (MaxDepth) — quá sâu thì dừng nhánh và log.
- [x] `FindAllChildren()` trả về 0 phần tử → cảnh báo 3 nguyên nhân rõ ràng (stale / đổi trạng thái / thực sự không có con), không để `children: []` im lặng.
- [x] Đọc property lỗi → bỏ qua phần tử an toàn, vẫn log cảnh báo khi thiếu AutomationId/Name (đề nghị đổi tên thủ công).
- [x] Tên gợi ý theo loại điều khiển: `Click_`, `CheckBox_`, `Input_`, `Select_`, `Radio_`, `Tab_`; ký tự đặc biệt thay bằng `_`.
- [x] Merge giữ `name` cũ (do người dùng đặt tay), cập nhật `path` mới; node viết tay (path rỗng) không bao giờ bị flag/xoá.
- [x] Node cũ không match được liệt kê trong CleanupDialog — chỉ xoá khi người dùng tích chọn.

### 6.4. Chỉnh sửa cây (BƯỚC 9)

- [x] Double-click node → RenameDialog đổi tên (chỉ sửa `name`, không đụng `path`).
- [x] Chọn node → "Tham số hoá {}" → thay `@AutomationId`/`@Name` cuối cùng trong path bằng `"{ }"` để tạo mẫu tham số.
- [x] Kéo-thả node trên cây (thả vào / trước / sau theo 1/3 chiều cao) để sắp xếp lại; chặn thả node vào chính nhánh con của nó.
- [x] "Xoá node lỗi thời" → mở CleanupDialog với danh sách `UnmatchedOldNodes` của lần merge gần nhất.

### 6.5. Xuất bản

- [x] `build-exe.bat` publish self-contained single-file (win-x64, ~72 MB) chạy được trên máy khác không cần cài .NET.

---

# PHẦN 7 — Tổng kết tính năng (v1.0.0)

> Toàn bộ tính năng liệt kê trong các bản plan trước đã được **triển khai đầy đủ** và đang
> chạy trong app hiện tại. Bảng dưới đây là tổng kết cuối cùng.

| Tính năng | Trạng thái | Ghi chú |
|---|---|---|
| Quét cây UIA → JSON `{name, path, children}` | ✅ Hoàn tất | v2 schema, Raw* ẩn khi ghi file |
| Path tương đối từ cửa sổ chính | ✅ Hoàn tất | segment có `@AutomationId`/`@Name`/`@ClassName`/index |
| Path tuyệt đối từ Desktop | ✅ Hoàn tất | resolve tương đối trước, tuyệt đối sau |
| Root anchor path khớp chính node root | ✅ Hoàn tất | `MatchesSegment` + `startIndex=1` |
| DFS xuyên node trung gian trong suốt | ✅ Hoàn tất | cả resolver lẫn path pick |
| Quét dừng khi path lỗi (không fallback) | ✅ Hoàn tất | log gộp lỗi 2 chế độ + `resolveMode` |
| Cảnh báo 0 children rõ ràng (3 nguyên nhân) | ✅ Hoàn tất | chống `children: []` im lặng |
| Merge giữ tên cũ, cập nhật path mới | ✅ Hoàn tất | không tự xoá node |
| Diff tracking + `<file>.diff.json` | ✅ Hoàn tất | added / changedPath / unmatchedOld |
| Dọn node lỗi thời (CleanupDialog) | ✅ Hoàn tất | chỉ xoá theo lựa chọn người dùng |
| Chọn phần tử gốc bằng chuột (Ctrl+R) | ✅ Hoàn tất | Win32 RegisterHotKey, kiểm tra process |
| Chọn node trên cây làm Root | ✅ Hoàn tất | TreeNodeTag.FullPath ghép tại lúc render |
| Tham số hoá `{}` | ✅ Hoàn tất | thay `@AutomationId`/`@Name` cuối path |
| Kéo-thả sắp xếp cây | ✅ Hoàn tất | Before/After/Into, chặn thả vào con |
| Đổi tên node (double-click) | ✅ Hoàn tất | RenameDialog |
| Xuất exe self-contained | ✅ Hoàn tất | `build-exe.bat` → `publish\XPathScanner.App.exe` |

---

# PHẦN 8 — Hướng phát triển tiếp theo

> Tất cả các mục từng được ghi trong "Phần 8" của các bản plan trước (thêm nút chọn root,
> tham số hoá, dọn node lỗi thời, kéo-thả...) **đã được hiện thực hoá** trong v1.0.0.
> Phần này giữ vai trò nơi ghi các ý tưởng tương lai — chưa có mục nào được cam kết.

**Ý tưởng (chưa triển khai):**

- Ghi log quét ra file (ngoài TextBox) để dễ tra cứu khi quét số lượng lớn.
- Tự động "Tham số hoá" nhiều node cùng lúc (chọn nhiều, bấm một lần).
- Undo/redo cho các thao tác sửa cây (rename, tham số hoá, kéo-thả, xoá).
- Lưu nhiều "screen" trong 1 file JSON với root chung.
- Tích hợp đọc/ghi Excel hoặc xuất CSV từ cây kết quả.
- Hỗ trợ chế độ quét nền (không chặn UI) khi cây quá lớn.

---

# GHI CHÚ CUỐI

- Tài liệu này được cập nhật để **khớp 100% với code hiện tại** của app (đối chiếu từng file
  trong `XPathScanner.Core` và `XPathScanner.App`). Nếu có thay đổi code sau này, hãy cập nhật
  lại tài liệu tương ứng.
- Các file yêu cầu bổ sung trong quá trình phát triển: `yeu-cau-bo-sung-resolve-absolute-path.md`,
  `yeu-cau-bo-sung-resolve-root-tu-khop.md`, `yeu-cau-bo-sung-chon-root-tu-treeview.md`.
- File dữ liệu mẫu để kiểm thử: `TASnode.json` (dùng cho luồng "Cập nhật JSON hiện có").
- Icon app: nguồn là `Untitled design.png` ở thư mục gốc. Chạy
  `powershell -ExecutionPolicy Bypass -File make-icon.ps1` để sinh lại
  `XPathScanner.App\Assets\app-icon.ico` (16–256 px) sau khi đổi ảnh. Script tự động
  xoá nền trắng ngoài logo bằng flood-fill từ biên (giữ nguyên chi tiết trắng bên trong
  như mũi tên trắng). `ApplicationIcon` (icon exe) + `Resource` (icon cửa sổ WPF) đều
  trỏ vào file này.
- Lệnh build: `dotnet build` (Debug) — nếu gặp lỗi MSB3026/MSB3027 (Core.dll bị khoá), hãy
  đóng app XPathScanner đang chạy rồi build lại.
- Lệnh xuất exe: chạy `build-exe.bat` (hoặc `dotnet publish -c Release -r win-x64 --self-contained true
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  -p:EnableCompressionInSingleFile=true -o publish`), kết quả tại `publish\XPathScanner.App.exe`.