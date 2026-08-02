using System;
using System.Windows.Input;

namespace UnlockMatePro.Models
{
    public class RibbonMenuItem
    {
        public string CommandParameter { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        
        // Parameterless constructor for JSON serialization
        public RibbonMenuItem() { }
        
        public RibbonMenuItem(string commandParameter, string content)
        {
            CommandParameter = commandParameter;
            Content = content;
        }
    }
}
