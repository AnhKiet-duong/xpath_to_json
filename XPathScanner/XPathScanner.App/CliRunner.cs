using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using XPathScanner.Core.Models;
using XPathScanner.Core.Services;

namespace XPathScanner.App
{
    /// <summary>
    /// Chế độ dòng lệnh (cmd): quét UI của một ứng dụng đang chạy và xuất ra file JSON
    /// mà không cần mở cửa sổ WPF. Được kích hoạt khi XPathScanner.exe nhận đối số cmd.
    /// </summary>
    internal static class CliRunner
    {
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        private const int AttachParentProcess = -1;

        // Trả về mã thoát: 0 = thành công, khác 0 = lỗi.
        public static int Run(string[] args)
        {
            // WinExe không tự gắn console: gắn vào console của cmd cha nếu có,
            // nếu không (chạy ngoài cmd) thì tự cấp một console để in kết quả.
            bool attached = AttachConsole(AttachParentProcess);
            if (!attached)
                AllocConsole();

            try
            {
                return RunCore(args);
            }
            finally
            {
                // Chỉ giải phóng console nếu chính mình cấp; nếu gắn vào console cha
                // (cmd) thì KHÔNG được đóng nó, kẻo cmd bị mất console.
                if (!attached)
                    FreeConsole();
            }
        }

        private static int RunCore(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "list":
                    return ListApps();
                case "export":
                    return Export(args);
                case "help":
                case "--help":
                case "-h":
                    PrintUsage();
                    return 0;
                default:
                    Console.Error.WriteLine("Không nhận diện được lệnh: " + args[0]);
                    PrintUsage();
                    return 2;
            }
        }

        private static int ListApps()
        {
            var apps = new ProcessListService().GetRunningApps();
            Console.WriteLine($"Tìm thấy {apps.Count} ứng dụng đang chạy có cửa sổ chính:");
            Console.WriteLine();
            Console.WriteLine($"{"PID",-8} {"Tên process",-24} Tiêu đề cửa sổ");
            Console.WriteLine(new string('-', 80));
            foreach (var a in apps)
            {
                Console.WriteLine($"{a.ProcessId,-8} {a.ProcessName,-24} {a.WindowTitle}");
            }
            return 0;
        }

        private static int Export(string[] args)
        {
            string? app = null, screen = null, root = null, outFile = null, mergeFile = null;
            bool keepDuplicates = false;

            int i = 1;
            while (i < args.Length)
            {
                string a = args[i];
                switch (a.ToLowerInvariant())
                {
                    case "--app": app = TakeValue(args, ref i, "--app"); break;
                    case "--screen": screen = TakeValue(args, ref i, "--screen"); break;
                    case "--root": root = TakeValue(args, ref i, "--root"); break;
                    case "--out": outFile = TakeValue(args, ref i, "--out"); break;
                    case "--merge": mergeFile = TakeValue(args, ref i, "--merge"); break;
                    case "--keep-duplicates": keepDuplicates = true; i++; break;
                    case "--help":
                    case "-h":
                        PrintUsage();
                        return 0;
                    default:
                        Console.Error.WriteLine("Tham số không hợp lệ: " + a);
                        PrintUsage();
                        return 2;
                }
            }

            if (string.IsNullOrWhiteSpace(app))
            {
                Console.Error.WriteLine("Thiếu tham số bắt buộc --app (PID hoặc tên process).");
                PrintUsage();
                return 2;
            }
            if (string.IsNullOrWhiteSpace(screen))
            {
                Console.Error.WriteLine("Thiếu tham số bắt buộc --screen (tên màn hình).");
                PrintUsage();
                return 2;
            }

            int pid = ResolveProcessId(app, out string appDisplay);
            if (pid < 0) return 3;

            // Quyết định file đầu ra
            if (string.IsNullOrWhiteSpace(outFile))
                outFile = string.IsNullOrWhiteSpace(mergeFile)
                    ? Path.Combine(Environment.CurrentDirectory, screen + ".json")
                    : mergeFile;

            Console.WriteLine($"Đang quét ứng dụng: {appDisplay}");
            Console.WriteLine($"Tên màn hình: {screen}");
            if (!string.IsNullOrWhiteSpace(root))
                Console.WriteLine($"Root anchor: {root}");
            Console.WriteLine($"Bỏ trùng node lá: {(keepDuplicates ? "KHÔNG (giữ trùng)" : "CÓ")}");
            Console.WriteLine();

            // Quét UI — FlaUI/UIA3 yêu cầu chạy trên luồng STA.
            UiNode? result = null;
            List<string> warnings = new();
            Exception? scanError = null;

            var scanThread = new Thread(() =>
            {
                try
                {
                    var scanner = new UiScannerService();
                    result = scanner.ScanApplication(pid, screen, root ?? "", skipDuplicateLeaves: !keepDuplicates);
                    warnings = new List<string>(scanner.Warnings);
                }
                catch (Exception ex)
                {
                    scanError = ex;
                }
            });
            scanThread.SetApartmentState(ApartmentState.STA);
            scanThread.IsBackground = true;
            scanThread.Start();
            scanThread.Join();

            if (scanError != null)
            {
                Console.Error.WriteLine("Lỗi khi quét: " + scanError.Message);
                return 4;
            }

            // In log / cảnh báo của lần quét
            foreach (var w in warnings)
                Console.WriteLine("  [log] " + w);

            Console.WriteLine();
            if (result == null)
            {
                Console.Error.WriteLine("Không có kết quả quét.");
                return 4;
            }

            // Ghi JSON
            var json = new JsonMergeService();
            if (!string.IsNullOrWhiteSpace(mergeFile))
            {
                if (!File.Exists(mergeFile))
                {
                    Console.Error.WriteLine($"Không tìm thấy file merge: {mergeFile}");
                    return 3;
                }
                var oldNode = json.LoadIfExists(mergeFile);
                var merged = json.Merge(oldNode, result);
                json.Save(merged, mergeFile);
                json.SaveDiff(mergeFile);
                Console.WriteLine($"Đã cập nhật (merge): {Path.GetFullPath(mergeFile)}");
                Console.WriteLine($"File diff: {Path.GetFullPath(mergeFile + ".diff.json")}");
            }
            else
            {
                json.Save(result, outFile);
                Console.WriteLine($"Đã xuất JSON: {Path.GetFullPath(outFile)}");
            }

            return 0;
        }

        // Đọc giá trị của một cờ "--key value". Luôn tiến i để tránh vòng lặp vô hạn.
        private static string? TakeValue(string[] args, ref int i, string flag)
        {
            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"Thiếu giá trị cho tham số {flag}.");
                i++;
                return null;
            }
            string value = args[i + 1];
            i += 2;
            return value;
        }

        // Chấp nhận PID (số) hoặc tên process. Trả về -1 nếu không tìm thấy.
        private static int ResolveProcessId(string appArg, out string display)
        {
            display = appArg;
            if (int.TryParse(appArg, out int pid))
            {
                try
                {
                    using var p = Process.GetProcessById(pid);
                    display = $"{p.ProcessName} (PID {pid})";
                    return pid;
                }
                catch
                {
                    Console.Error.WriteLine($"Không tìm thấy tiến trình có PID {pid}.");
                    return -1;
                }
            }

            var matches = new ProcessListService().GetRunningApps()
                .Where(a => a.ProcessName.Equals(appArg, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
            {
                Console.Error.WriteLine($"Không tìm thấy ứng dụng đang chạy có tên process: {appArg}");
                Console.Error.WriteLine("Chạy 'XPathScanner.exe list' để xem danh sách ứng dụng đang chạy.");
                return -1;
            }

            display = $"{matches[0].ProcessName} (PID {matches[0].ProcessId})";
            return matches[0].ProcessId;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("XPathScanner — xuất XPath JSON qua dòng lệnh (cmd)");
            Console.WriteLine();
            Console.WriteLine("Cách dùng:");
            Console.WriteLine("  XPathScanner.exe list");
            Console.WriteLine("      Liệt kê các ứng dụng đang chạy có cửa sổ chính (PID, tên process, tiêu đề).");
            Console.WriteLine();
            Console.WriteLine("  XPathScanner.exe export --app <pid|tên> --screen <tên> [tuỳ chọn]");
            Console.WriteLine("      Quét UI của ứng dụng và xuất ra file JSON.");
            Console.WriteLine();
            Console.WriteLine("Tham số export:");
            Console.WriteLine("  --app <pid|tên>    PID hoặc tên process của ứng dụng cần quét (bắt buộc).");
            Console.WriteLine("  --screen <tên>     Tên màn hình / feature (bắt buộc).");
            Console.WriteLine("  --root <path>      Root anchor path (tuỳ chọn). Để trống = quét toàn bộ cửa sổ.");
            Console.WriteLine("  --out <file>       Đường dẫn file JSON đầu ra (mặc định: <tên màn hình>.json).");
            Console.WriteLine("  --merge <file>     File JSON cũ để cập nhật (merge) thay vì tạo mới.");
            Console.WriteLine("  --keep-duplicates  Giữ các node lá có path trùng (mặc định: bỏ trùng).");
            Console.WriteLine("  -h, --help         Hiện hướng dẫn này.");
            Console.WriteLine();
            Console.WriteLine("Ví dụ:");
            Console.WriteLine("  XPathScanner.exe list");
            Console.WriteLine("  XPathScanner.exe export --app notepad --screen \"PrintOut\"");
            Console.WriteLine("  XPathScanner.exe export --app 1234 --screen NavBar --root \"/Pane[0]/Pane[1]\" --out out.json");
            Console.WriteLine("  XPathScanner.exe export --app notepad --screen TASnode --merge TASnode.json");
        }
    }
}
