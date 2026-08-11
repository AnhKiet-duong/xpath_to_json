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
