using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace os.Models
{
    public class NameSelectionModel
    {
        public int SpeakerId { get; set; }
        public string? SpeakerName { get; set; }
        public List<NameItem> Names { get; set; } = new List<NameItem>();
        public string? ReturnUrl { get; set; }
    }

    public class NameItem
    {
        public string? Name { get; set; }
        public string? Start { get; set; }
        public string? End { get; set; }
        public bool Selected { get; set; } = true;
    }
}