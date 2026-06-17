using System.Collections.Generic;

namespace ValorantAutoClicker.Models
{
    public class DeviceSelectionModel
    {
        public List<string> SelectedKeyboards { get; set; } = new();
        public List<string> SelectedMonitors { get; set; } = new();
        public List<string> SelectedMice { get; set; } = new();
    }
}
