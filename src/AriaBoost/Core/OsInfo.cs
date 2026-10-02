using System;

namespace AriaBoost.Core
{
    /// <summary>
    /// Windows sürüm bilgisi. Environment.OSVersion yerine kayıt defteri okunur;
    /// "ProductName" Windows 11'de bile "Windows 10" yazdığı için sürüm, derleme
    /// numarasından (22000 ve üstü = Windows 11) belirlenir.
    /// </summary>
    public sealed class OsInfo
    {
        public const int MinSupportedBuild = 17763; // Windows 10 1809
        public const int Windows11Build = 22000;

        public int Build { get; private set; }
        public int Ubr { get; private set; }
        public string DisplayVersion { get; private set; } = "";
        public string Edition { get; private set; } = "";

        public bool IsWindows11 => Build >= Windows11Build;
        public bool IsSupported => Build >= MinSupportedBuild;
        public string Name => IsWindows11 ? "Windows 11" : "Windows 10";

        public string FullName
        {
            get
            {
                var edition = EditionLabel(Edition);
                var name = string.IsNullOrEmpty(edition) ? Name : $"{Name} {edition}";
                if (!string.IsNullOrEmpty(DisplayVersion)) name += " " + DisplayVersion;
                return name;
            }
        }

        public string BuildLabel => Ubr > 0 ? $"{Build}.{Ubr}" : Build.ToString();

        private static OsInfo current;
        public static OsInfo Current => current ?? (current = Read());

        public static OsInfo Read()
        {
            const string path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            var info = new OsInfo();
            int.TryParse(Convert.ToString(Reg.Read(Hive.LocalMachine, path, "CurrentBuildNumber") ?? Reg.Read(Hive.LocalMachine, path, "CurrentBuild")), out var build);
            info.Build = build > 0 ? build : Environment.OSVersion.Version.Build;
            var ubr = Reg.Read(Hive.LocalMachine, path, "UBR");
            info.Ubr = ubr is int u ? u : 0;
            info.DisplayVersion = Convert.ToString(Reg.Read(Hive.LocalMachine, path, "DisplayVersion") ?? Reg.Read(Hive.LocalMachine, path, "ReleaseId") ?? "");
            info.Edition = Convert.ToString(Reg.Read(Hive.LocalMachine, path, "EditionID") ?? "");
            return info;
        }

        private static string EditionLabel(string editionId)
        {
            switch (editionId)
            {
                case "Core": return "Home";
                case "CoreSingleLanguage": return "Home Single Language";
                case "Professional": return "Pro";
                case "ProfessionalWorkstation": return "Pro for Workstations";
                case "Enterprise": return "Enterprise";
                case "Education": return "Education";
                case "IoTEnterprise": return "IoT Enterprise";
                default: return editionId ?? "";
            }
        }
    }
}
