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
