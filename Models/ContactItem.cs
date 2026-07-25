namespace AdbEasyInstaller.Models
{
    public class ContactItem
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class SmsItem
    {
        public string Id { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string Type { get; set; } = "1"; // 1 = Received, 2 = Sent
        public string TypeName => Type == "1" ? "Received" : "Sent";
    }

    public class CallLogItem
    {
        public string Id { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string CachedName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string DurationSeconds { get; set; } = "0";
        public string Type { get; set; } = "1"; // 1 = Incoming, 2 = Outgoing, 3 = Missed
        public string TypeName => Type switch
        {
            "1" => "Incoming",
            "2" => "Outgoing",
            "3" => "Missed",
            _ => "Unknown"
        };
    }
}
