namespace Proxus_MQTT_Bench.Infrastructure
{
    /// <summary>
    /// Provides methods to retrieve system information across different operating systems.
    /// </summary>
    public static class SystemInfo
    {
        private const int Kb = 1024;
        private const int Mb = Kb * Kb;
        private const long Gb = Mb * Kb;

        /// <summary>
        /// Retrieves and formats system information including OS, CPU, RAM, and disk details.
        /// </summary>
        /// <returns>A string containing formatted system information.</returns>
        public static string GetSystemInfo()
        {
            var systemInfo = new StringBuilder("");

            // Operating System Information
            var osVersion = Environment.OSVersion.Version;
            var osName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" : 
                         RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "MacOS" : "Linux";
            systemInfo.AppendLine($"OS: {osName} {osVersion.Major}.{osVersion.Minor}");

            // CPU Information
            var cpuName = GetProcessorName().Replace("machdep.cpu.brand_string:", "");
            var cpuCores = Environment.ProcessorCount;
            var architecture = Environment.Is64BitProcess ? "64-bit" : "32-bit";
            systemInfo.AppendLine($"CPU: {cpuName} - Cores: {cpuCores} - Architecture: {architecture}");

            // RAM Information
            var totalRam = GetTotalRamSize();
            systemInfo.AppendLine($"RAM: {totalRam} GB");

            // Disk Information
            var drives = DriveInfo.GetDrives();
            foreach (var drive in drives)
            {
                if (drive.IsReady && drive.Name == "/")
                {
                    var totalSize = drive.TotalSize / Gb;
                    systemInfo.AppendLine($"Disk: {totalSize} GB");
                }
            }

            return systemInfo.ToString();
        }

        /// <summary>
        /// Retrieves the processor name based on the current operating system.
        /// </summary>
        /// <returns>The name of the processor.</returns>
        private static string GetProcessorName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsProcessorName();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetLinuxProcessorName();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacProcessorName();
            return string.Empty;
        }

        private static string GetWindowsProcessorName()
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
                return obj["Name"].ToString();
            return string.Empty;
        }

        private static string GetLinuxProcessorName()
        {
            using var reader = new StreamReader("/proc/cpuinfo");
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("model name"))
                    return line.Split(':')[1].Trim();
            }
            return string.Empty;
        }

        private static string GetMacProcessorName()
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/sysctl",
                    Arguments = "machdep.cpu.brand_string",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return result;
        }

        /// <summary>
        /// Retrieves the total RAM size based on the current operating system.
        /// </summary>
        /// <returns>The total RAM size in GB.</returns>
        private static double GetTotalRamSize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsTotalRamSize();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetLinuxTotalRamSize();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacTotalRamSize();
            return 0;
        }

        private static double GetWindowsTotalRamSize()
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
                return Math.Round(Convert.ToDouble(obj["TotalVisibleMemorySize"]) / Mb, 2);
            return 0;
        }

        private static double GetLinuxTotalRamSize()
        {
            using var reader = new StreamReader("/proc/meminfo");
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("MemTotal:"))
                    return Math.Round(Convert.ToDouble(line.Split(':')[1].Trim().Split(' ')[0]) / Kb / Kb, 2);
            }
            return 0;
        }

        private static double GetMacTotalRamSize()
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/usr/sbin/sysctl",
                    Arguments = "hw.memsize",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            var match = Regex.Match(output, @"\d+");
            process.WaitForExit();
            return match.Success ? Math.Round(Convert.ToDouble(match.Value) / Gb, 2) : 0;
        }

        /// <summary>
        /// Retrieves the used RAM size based on the current operating system.
        /// </summary>
        /// <returns>The used RAM size in GB.</returns>
        private static double GetUsedRamSize()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return GetWindowsUsedRamSize();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return GetLinuxUsedRamSize();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return GetMacUsedRamSize();
            return 0;
        }

        private static double GetWindowsUsedRamSize()
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
                return Math.Round((Convert.ToDouble(obj["TotalVisibleMemorySize"]) - Convert.ToDouble(obj["FreePhysicalMemory"])) / Mb, 2);
            return 0;
        }

        private static double GetLinuxUsedRamSize()
        {
            double memTotal = 0, memFree = 0;
            using var reader = new StreamReader("/proc/meminfo");
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("MemTotal:"))
                    memTotal = Convert.ToDouble(line.Split(':')[1].Trim().Split(' ')[0]);
                if (line.StartsWith("MemFree:") || line.StartsWith("Buffers:") || line.StartsWith("Cached:"))
                    memFree += Convert.ToDouble(line.Split(':')[1].Trim().Split(' ')[0]);
                if (memTotal != 0 && memFree != 0)
                    return Math.Round((memTotal - memFree) / Kb / Kb, 2);
            }
            return 0;
        }

        private static double GetMacUsedRamSize()
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"top -l 1 -s 0 | grep PhysMem | sed 's/, /\\n         /g'\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            var match = Regex.Match(output, @"\d+(\.\d+)?");
            process.WaitForExit();
            return match.Success ? Math.Round(Convert.ToDouble(match.Value) / Mb, 2) : 0;
        }
    }
}