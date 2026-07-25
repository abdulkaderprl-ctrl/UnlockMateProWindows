using System;
using System.IO;

namespace AdbEasyInstaller.Models
{
    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; } = false;
        public long SizeBytes { get; set; } = 0;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string Permissions { get; set; } = "rw-rw----";
        public string Owner { get; set; } = "root";

        public string Type => IsDirectory ? "Folder" : (Path.GetExtension(Name).ToUpper().TrimStart('.') + " File");

        public string FormattedSize
        {
            get
            {
                if (IsDirectory) return "<DIR>";
                if (SizeBytes >= 1073741824) return $"{SizeBytes / 1073741824.0:F2} GB";
                if (SizeBytes >= 1048576) return $"{SizeBytes / 1048576.0:F2} MB";
                if (SizeBytes >= 1024) return $"{SizeBytes / 1024.0:F1} KB";
                return $"{SizeBytes} B";
            }
        }

        public string IconBadge => IsDirectory ? "📁" : "📄";
        public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm");
    }
}
