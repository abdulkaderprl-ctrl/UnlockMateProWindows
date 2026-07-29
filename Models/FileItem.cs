using System;
using System.IO;

namespace UnlockMatePro.Models
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

        public bool IsHidden => Name.StartsWith(".", StringComparison.Ordinal);
        public string Extension => Path.GetExtension(Name).ToLowerInvariant();

        public string Type
        {
            get
            {
                if (IsDirectory) return "Folder";
                string ext = Extension.TrimStart('.');
                return string.IsNullOrWhiteSpace(ext) ? "File" : $"{ext.ToUpper()} File";
            }
        }

        public string FileCategory
        {
            get
            {
                if (IsDirectory) return "Folder";
                switch (Extension)
                {
                    case ".jpg": case ".jpeg": case ".png": case ".gif": case ".bmp": case ".webp": case ".svg": case ".heic":
                        return "Image";
                    case ".mp4": case ".mkv": case ".avi": case ".mov": case ".wmv": case ".flv": case ".3gp":
                        return "Video";
                    case ".apk": case ".apks": case ".xapk":
                        return "APK Application";
                    case ".pdf":
                        return "PDF Document";
                    case ".mp3": case ".wav": case ".flac": case ".aac": case ".m4a": case ".ogg":
                        return "Audio";
                    case ".zip": case ".rar": case ".7z": case ".tar": case ".gz":
                        return "Archive";
                    case ".txt": case ".doc": case ".docx": case ".xls": case ".xlsx": case ".json": case ".xml":
                        return "Document";
                    default:
                        return "File";
                }
            }
        }

        public string IconBadge
        {
            get
            {
                if (IsDirectory) return "📁";
                switch (FileCategory)
                {
                    case "Image": return "🖼️";
                    case "Video": return "🎬";
                    case "APK Application": return "📱";
                    case "PDF Document": return "📕";
                    case "Audio": return "🎵";
                    case "Archive": return "🗜️";
                    case "Document": return "📝";
                    default: return "📄";
                }
            }
        }

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

        public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm");
    }
}

