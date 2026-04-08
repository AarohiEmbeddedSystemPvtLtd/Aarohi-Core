using Aarohi.Core.PLC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Networking
{
    public static class PlcNetworkInfo
    {
        // ---------------- BACKING FIELD ----------------
        private static string _ip = "";

        public static string IP
        {
            get => _ip;
            set
            {
                _ip = value?.Trim() ?? "";
                _cachedDns = null;
                _cachedMac = null;
                _cachedReachable = null;
            }
        }
        
        // ---------------- CACHE ----------------
        private static string? _cachedDns;
        private static string? _cachedMac;
        private static bool? _cachedReachable;

        // ---------------- DNS NAME ----------------
        public static string DnsName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(IP))
                    return "IP not set";

                if (_cachedDns != null)
                    return _cachedDns;

                try
                {
                    var host = Dns.GetHostEntry(IP);
                    _cachedDns = host.HostName;
                }
                catch
                {
                    _cachedDns = "No DNS entry found";
                }

                return _cachedDns;
            }
        }

        // ---------------- REACHABLE ----------------
        public static bool IsReachable
        {
            get
            {
                if (string.IsNullOrWhiteSpace(IP))
                    return false;

                if (_cachedReachable.HasValue)
                    return _cachedReachable.Value;

                try
                {
                    using var ping = new Ping();
                    var reply = ping.Send(IP, 1000);
                    _cachedReachable = reply.Status == IPStatus.Success;
                }
                catch
                {
                    _cachedReachable = false;
                }

                return _cachedReachable.Value;
            }
        }

        // ---------------- MAC ADDRESS ----------------
        public static string MacAddress
        {
            get
            {
                if (string.IsNullOrWhiteSpace(IP))
                    return "IP not set";

                if (_cachedMac != null)
                    return _cachedMac;

                try
                {
                    var process = new Process();
                    process.StartInfo.FileName = "arp";
                    process.StartInfo.Arguments = "-a " + IP;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    var match = Regex.Match(output,
                        @"([0-9a-f]{2}-){5}[0-9a-f]{2}",
                        RegexOptions.IgnoreCase);

                    _cachedMac = match.Success ? match.Value : "MAC not found";
                }
                catch
                {
                    _cachedMac = "MAC read failed";
                }

                return _cachedMac;
            }
        }

        // ---------------- FULL REPORT ----------------
        public static string FullReport
        {
            get
            {
                var sb = new StringBuilder();

                sb.AppendLine($"IP Address: {IP}");
                sb.AppendLine($"Reachable: {IsReachable}");
                sb.AppendLine($"DNS Name: {DnsName}");
                sb.AppendLine($"MAC Address: {MacAddress}");

                return sb.ToString();
            }
        }

        public static async Task<ClassPLC.PlcMetaData?>
    DiscoverSinglePlcAsync(string ip, int rack = 0, int slot = 1, int timeoutMs = 1000)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return null;

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, timeoutMs);

                if (reply.Status != IPStatus.Success)
                    return null;

                using var plc = new Aarohi.Core.PLC.ClassPLC(ip, rack, slot)
                {
                    ConnectTimeoutMs = timeoutMs
                };

                await plc.ConnectAsync();

                return plc.GetMeta();
            }
            catch
            {
                return null;
            }
        }


    }
}
