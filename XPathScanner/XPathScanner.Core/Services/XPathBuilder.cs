using FlaUI.Core.AutomationElements;

namespace XPathScanner.Core.Services
{
    public static class XPathBuilder
    {
        // Sinh 1 đoạn xpath (segment) cho MỘT phần tử, ưu tiên AutomationId > Name.
        // Phần tử không có id/name → chỉ trả về controlType (không kèm index [N]).
        public static string BuildSegment(AutomationElement element)
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

            return controlType;
        }

        private static string Escape(string input) => input.Replace("\"", "\\\"");
    }
}
