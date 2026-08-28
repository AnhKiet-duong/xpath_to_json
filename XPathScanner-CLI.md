# XPathScanner — Xuất JSON qua dòng lệnh (cmd)

Tính năng cho phép quét UI của một ứng dụng đang chạy và xuất ra file JSON **trực tiếp từ cmd**, không cần mở cửa sổ WPF. Cùng một file `XPathScanner.exe`: chạy không đối số → mở giao diện WPF; chạy có đối số → chế độ dòng lệnh.

---

## 1. Chuẩn bị

- Build bản phát hành (single-file, không cần cài .NET trên máy đích):

```bat
build-exe.bat
```

- File chạy được: `publish\XPathScanner.exe`
- Bản dev (khi phát triển):

```bat
build.bat
```

File dev nằm ở `XPathScanner\XPathScanner.App\bin\Debug\net8.0-windows\XPathScanner.exe`.

> ⚠️ Mở cmd trước, rồi trỏ vào thư mục chứa `XPathScanner.exe` (hoặc dùng đường dẫn đầy đủ).

---

## 2. Lệnh `list` — xem ứng dụng đang chạy

```bat
XPathScanner.exe list
```

Kết quả liệt kê các ứng dụng đang chạy **có cửa sổ chính**, kèm `PID`, tên process và tiêu đề cửa sổ. Dùng cột **Tên process** hoặc **PID** làm giá trị cho `--app` ở lệnh export.

Ví dụ:

```text
PID      Tên process              Tiêu đề cửa sổ
--------------------------------------------------------------------------------
19336    Notepad                  *dựa vào các file test case trong fo - Notepad
6364     Code                     g:\Xpath to Json Project\... - Visual Studio Code
```

---

## 3. Lệnh `export` — quét và xuất JSON

```bat
XPathScanner.exe export --app <pid|tên> --screen <tên> [tuỳ chọn]
```

### Tham số bắt buộc

| Tham số | Mô tả |
|---------|-------|
| `--app <pid|tên>` | PID hoặc tên process của ứng dụng cần quét. VD: `notepad`, `chrome`, hoặc `19336`. |
| `--screen <tên>` | Tên màn hình / feature (trở thành `name` của node gốc). VD: `"PrintOut"`, `NavBar`. |

### Tham số tuỳ chọn

| Tham số | Mô tả |
|---------|-------|
| `--root <path>` | Root anchor path. Để trống = quét toàn bộ cửa sổ. Nếu path không tìm thấy → **dừng quét** (không tự quét toàn bộ). |
| `--out <file>` | Đường dẫn file JSON đầu ra. Mặc định: `<tên màn hình>.json` trong thư mục hiện tại. |
| `--merge <file>` | File JSON cũ để **cập nhật (merge)** thay vì tạo mới. Giữ tên đã đổi tay, không xoá node; đồng thời sinh file `.diff.json`. |
| `--keep-duplicates` | **Giữ** các node lá có path trùng. Mặc định (không khai báo) là **bỏ trùng**. |
| `-h`, `--help` | Hiện hướng dẫn sử dụng. |

### Ví dụ

```bat
:: Xem danh sách ứng dụng
XPathScanner.exe list

:: Quét toàn bộ cửa sổ Notepad, xuất ra PrintOut.json
XPathScanner.exe export --app notepad --screen "PrintOut"

:: Quét bằng PID, quét từ root anchor, ghi ra file chỉ định
XPathScanner.exe export --app 19336 --screen NavBar --root "/Pane[0]/Pane[1]" --out out.json

:: Cập nhật (merge) vào file JSON đã có + sinh file diff
XPathScanner.exe export --app notepad --screen TASnode --merge TASnode.json

:: Giữ các node lá trùng path
XPathScanner.exe export --app notepad --screen "PrintOut" --keep-duplicates
```

> 💡 Nếu tên process có dấu cách hoặc chứa ký tự đặc biệt, bọc trong dấu nháy kép. Cũng nên bọc `--screen` và `--root` trong dấu nháy kép nếu có khoảng trắng.

---

## 4. File JSON đầu ra

Đúng schema `{name, path, children}` — không thêm field nào khác (hệ thống automation của bạn đọc đúng schema này):

```json
{
  "name": "PrintOut",
  "path": "",
  "children": [
    {
      "name": "Click_Print",
      "path": "/Button[@AutomationId=\"Print\"]",
      "children": []
    }
  ]
}
```

- `path` là tương đối so với node cha gần nhất có path khác rỗng.
- Chuỗi `"` được escape dạng `\"` (không phải `\u0022`).
- Tên tự sinh chỉ là gợi ý (`Click_`, `Input_`, `Select_`, ...) — trong CLI chưa có bước đổi tên tay; nếu cần tên tuỳ chỉnh, hãy mở file bằng giao diện WPF để đổi tên rồi lưu.

### File `.diff.json` (khi dùng `--merge`)

Ghi lại thay đổi giữa lần quét cũ và mới:

```json
{
  "updatedAt": "2026-08-13T14:35:19.4042499+07:00",
  "added": [ ... ],
  "changedPath": [ ... ],
  "unmatchedOld": [ ... ]
}
```

---

## 5. Mã thoát (exit code)

| Mã | Ý nghĩa |
|----|---------|
| `0` | Thành công. |
| `1` | Không có đối số (in hướng dẫn). |
| `2` | Sai cú pháp / thiếu tham số bắt buộc. |
| `3` | Không tìm thấy ứng dụng (hoặc file merge không tồn tại). |
| `4` | Lỗi khi quét / không có kết quả. |

Trong batch script có thể kiểm tra qua `%ERRORLEVEL%`:

```bat
XPathScanner.exe export --app notepad --screen "PrintOut"
if errorlevel 1 (
  echo Xuất thất bại.
  exit /b 1
)
echo Xuất thành công.
```

---

## 6. Ghi chú kỹ thuật

- Scan chạy trên luồng **STA** (yêu cầu của FlaUI/UIA3) — CLI đã tự xử lý.
- Ứng dụng đích phải **đang mở cửa sổ** trước khi quét; nếu đóng cửa sổ sẽ báo lỗi.
- Chạy `XPathScanner.exe` **không có đối số** để mở giao diện WPF như bình thường (tính năng cũ không thay đổi).
