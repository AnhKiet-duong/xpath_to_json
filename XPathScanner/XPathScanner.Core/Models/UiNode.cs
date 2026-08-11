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
