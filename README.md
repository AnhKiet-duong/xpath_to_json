<div align="center">

# 🛰️ XPathScanner

**Quét cây UI Automation (UIA) → Xuất JSON dạng XPath**

Công cụ Windows (WPF) quét cây **UI Automation** của một ứng dụng đang chạy và xuất ra file JSON
dạng cây đệ quy `{ name, path, children }`, trong đó `path` là **XPath** trỏ tới từng phần tử UI.

<br>

![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue)
![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet)
![UI](https://img.shields.io/badge/UI-WPF-512BD4)
![Scanner](https://img.shields.io/badge/Scanner-FlaUI%20UIA3-orange)
![License](https://img.shields.io/badge/License-Private-lightgrey)

</div>

---

## 📑 Mục lục

- [✨ Tính năng](#-tính-năng)
- [🖥️ Yêu cầu hệ thống](#️-yêu-cầu-hệ-thống)
- [🔧 Build & chạy (phát triển)](#-build--chạy-phát-triển)
- [📦 Đóng gói exe phân phối](#-đóng-gói-exe-phân-phối)
- [🚀 Hướng dẫn sử dụng](#-hướng-dẫn-sử-dụng)
- [🗂️ Schema JSON](#️-schema-json)
- [📁 Cấu trúc thư mục](#-cấu-trúc-thư-mục)
- [💡 Ghi chú kỹ thuật](#-ghi-chú-kỹ-thuật)
- [📚 Tài liệu liên quan](#-tài-liệu-liên-quan)

---

## ✨ Tính năng

| | Tính năng |
|---|---|
| 🔍 | Quét toàn bộ cửa sổ chính của một ứng dụng, hoặc **giới hạn vào một root anchor** (3 cách chọn). |
| 🧭 | Tự động sinh XPath cho từng phần tử: `Button[@AutomationId="..."]`, `Edit[@Name="..."]`,... |
| 🧹 | Gộp các node trung gian "trong suốt" (không AutomationId/Name, đúng 1 con) để path gọn, không rác. |
| 🔄 | **Cập nhật gia tăng**: merge vào file JSON cũ, giữ `name` do người dùng đặt, cập nhật `path` mới, **không bao giờ tự xoá** node. |
| 🧾 | Xuất kèm file `<file>.diff.json` (thêm / đổi path / không match). |
| ✏️ | Chỉnh sửa cây trước khi lưu: **đổi tên**, **tham số hoá `{}`**, **kéo-thả sắp xếp**, **dọn node lỗi thời**. |
| 📦 | Đóng gói **self-contained single-file exe** (~70 MB) — máy khác không cần cài .NET. |

---

## 🖥️ Yêu cầu hệ thống

| Mục đích | Yêu cầu |
|---|---|
| **Chạy exe phân phối** | Windows 10/11 64-bit. Không cần cài thêm gì (đã gói runtime). |
| **Build / phát triển** | Windows 10/11 64-bit + **.NET 8 SDK**. |

---

## 🔧 Build & chạy (phát triển)

```bash
# Build solution
dotnet build XPathScanner\XPathScanner.sln

# Chạy từ nguồn
dotnet run --project XPathScanner\XPathScanner.App
```

> ⚠️ **Lưu ý:** nếu gặp lỗi `MSB3026/MSB3027` (*"Core.dll bị khoá"*), nghĩa là app XPathScanner
> đang chạy — hãy đóng app rồi build lại.
>
> Cảnh báo `NU1701` về `FlaUI.Core` / `FlaUI.UIA3` là **đã biết, vô hại** (gói nhắm .NET Framework).

---

## 📦 Đóng gói exe phân phối

Chạy file **`build-exe.bat`** ở thư mục gốc dự án, hoặc lệnh tương đương:

```bash
dotnet publish XPathScanner\XPathScanner.App\XPathScanner.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o publish
```

Kết quả: **`publish\XPathScanner.App.exe`** — self-contained, single-file.

📌 Chỉ cần copy **1 file exe** này sang máy khác (Windows 64-bit) là chạy được. Các file `*.pdb`
là debug symbol, không cần copy.

> Máy đích 32-bit thì đổi `win-x64` thành `win-x86` trong `build-exe.bat`.

---

## 🚀 Hướng dẫn sử dụng

### Luồng cơ bản

1. **Làm mới danh sách** → chọn ứng dụng cần quét.
2. Nhập **Tên màn hình** (VD: `NavBar`, `PrintOut`...) — đây là `name` của node gốc.
3. (Tuỳ chọn) nhập **Root anchor path** để giới hạn phạm vi quét.
4. Bấm **Quét** → cây kết quả hiện trong TreeView, kèm log chi tiết.
5. Bấm **Lưu JSON mới** để ghi file, hoặc **Cập nhật JSON hiện có** để merge vào file cũ.

### Chọn Root anchor path (3 cách)

| Cách | Thao tác |
|---|---|
| ⌨️ **Gõ tay** | Nhập trực tiếp chuỗi path vào ô "Root anchor path" (tương đối từ cửa sổ chính, hoặc tuyệt đối từ Desktop). |
| 🖱️ **Trỏ chuột** | Bấm **Chọn gốc (Ctrl+R)** → di chuột lên phần tử trong app đích → nhấn <kbd>Ctrl</kbd>+<kbd>R</kbd> → path tự điền. |
| 🌳 **Chọn từ cây** | Chọn 1 node trên cây kết quả → bấm **Đặt node đang chọn làm Root** → full path được điền vào ô. |

> Path resolve thử **tương đối** trước, **tuyệt đối** (từ Desktop) sau; log ghi rõ chế độ đã dùng.
> Nếu path không resolve được → **dừng quét** và log lý do (không tự ý quét toàn cửa sổ).

### Chỉnh sửa cây trước khi lưu

| Thao tác | Cách làm |
|---|---|
| ✏️ **Đổi tên** | Double-click vào node → nhập tên mới (chỉ đổi `name`, không đụng `path`). |
| ⚙️ **Tham số hoá `{}`** | Chọn node → bấm **Tham số hoá {}** → thay `@AutomationId`/`@Name` cuối path bằng `"{ }"`. |
| 🖱️ **Kéo-thả** | Kéo node thả vào / trước / sau node khác (theo 1/3 chiều cao) để sắp xếp. |
| 🗑️ **Dọn node lỗi thời** | Sau khi "Cập nhật JSON hiện có", bấm **Xoá node lỗi thời** → tích chọn node muốn xoá. |

> Node viết tay (path rỗng) **không bao giờ** bị đề nghị xoá.

### Cập nhật JSON hiện có (merge + diff)

1. Chọn file JSON cũ → app merge: giữ `name` cũ, cập nhật `path` mới, giữ lại node không match.
2. Ghi lại file + xuất **`<file>.diff.json`**:
   - `added` — node mới
   - `changedPath` — node đổi path
   - `unmatchedOld` — node cũ không còn match
3. Có thể mở **CleanupDialog** ngay để dọn node lỗi thời.

---

## 🗂️ Schema JSON

```json
{
    "name": "TênMànHình",
    "path": "/Pane[@AutomationId=\"...\"]/Button[@Name=\"...\"]",
    "children": [
        { "name": "Click_...", "path": "/Button[@Name=\"...\"]", "children": [] }
    ]
}
```

| Trường | Mô tả |
|---|---|
| `name` | Tên node — tự sinh theo loại điều khiển hoặc do người dùng đặt tay. |
| `path` | XPath của node; có thể để rỗng với node nhóm logic. |
| `children` | Mảng đệ quy đúng schema này. |

📄 File mẫu để kiểm thử luồng "Cập nhật JSON hiện có": **`TASnode.json`**.

---

## 📁 Cấu trúc thư mục

```text
XPath to Json Project/
├── README.md
├── QWEN.md
├── build-exe.bat                     # script publish exe self-contained
├── make-icon.ps1                     # script sinh app-icon.ico từ PNG nguồn
├── xpath-scanner-plan-v1.0.0.md      # tài liệu thiết kế / đối chiếu code
├── TASnode.json                      # file JSON mẫu để kiểm thử merge
├── Untitled design.png               # ảnh nguồn của icon
├── publish/
│   └── XPathScanner.App.exe          # exe self-contained (~70 MB)
└── XPathScanner/
    ├── XPathScanner.sln
    ├── XPathScanner.Core/            # class library: toàn bộ logic
    │   ├── Models/    # UiNode.cs, PathChange.cs
    │   └── Services/  # ProcessListService, PathSegment, PathParser, XPathBuilder,
    │                  # ElementPathResolver, UiScannerService, UiPathService, JsonMergeService
    └── XPathScanner.App/             # WPF UI
        ├── App.xaml / App.xaml.cs
        ├── MainWindow.xaml / MainWindow.xaml.cs
        ├── TreeNodeTag.cs
        ├── RenameDialog.xaml / RenameDialog.xaml.cs
        ├── CleanupDialog.xaml / CleanupDialog.xaml.cs
        └── Assets/
            └── app-icon.ico
```

---

## 💡 Ghi chú kỹ thuật

- **Icon app** — nguồn là `Untitled design.png`. Chạy
  `powershell -ExecutionPolicy Bypass -File make-icon.ps1` để sinh lại
  `XPathScanner.App\Assets\app-icon.ico` (16–256 px). Script tự động xoá nền trắng ngoài logo
  bằng **flood-fill từ biên** (giữ nguyên chi tiết trắng bên trong như mũi tên trắng).
- **FlaUI** — `FlaUI.Core` / `FlaUI.UIA3` 5.0.0: cảnh báo NU1701 đã biết, không ảnh hưởng.
- **JSON escape** — app dùng `UnsafeRelaxedJsonEscaping` để xuất `\"` thay vì `\u0022`, khớp văn
  phong file mẫu của người dùng.

---

## 📚 Tài liệu liên quan

- 📄 [`xpath-scanner-plan-v1.0.0.md`](./xpath-scanner-plan-v1.0.0.md) — bản thiết kế tổng hợp,
  đối chiếu **100% với code hiện tại**.
- Các file yêu cầu bổ sung: `yeu-cau-bo-sung-resolve-absolute-path.md`,
  `yeu-cau-bo-sung-resolve-root-tu-khop.md`, `yeu-cau-bo-sung-chon-root-tu-treeview.md`.
