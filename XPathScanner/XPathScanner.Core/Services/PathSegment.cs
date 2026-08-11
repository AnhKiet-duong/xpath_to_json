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
