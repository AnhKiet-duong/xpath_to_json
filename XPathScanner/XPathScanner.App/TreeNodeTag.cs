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
