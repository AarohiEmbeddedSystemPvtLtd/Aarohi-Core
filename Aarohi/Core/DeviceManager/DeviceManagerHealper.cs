using System;
using System.IO.Ports;

namespace Aarohi.Core.DeviceManager
{
    public static class DeviceManagerHealper
    {
        /// <summary>
        /// Converts string to System.IO.Ports.Parity.
        /// Accepts: None/N, Even/E, Odd/O, Mark/M, Space/S (with spaces/dashes allowed).
        /// </summary>
        public static Parity ToParity(string? parityText, Parity defaultParity = Parity.None)
        {
            if (string.IsNullOrWhiteSpace(parityText))
                return defaultParity;

            string p = parityText.Trim()
                                 .Replace(" ", "")
                                 .Replace("-", "")
                                 .ToUpperInvariant();

            return p switch
            {
                "NONE" or "N" => Parity.None,
                "EVEN" or "E" => Parity.Even,
                "ODD" or "O" => Parity.Odd,
                "MARK" or "M" => Parity.Mark,
                "SPACE" or "S" => Parity.Space,
                _ => defaultParity
            };
        }

        /// <summary>
        /// Converts string to StopBits.
        /// Accepts: 1, 1.5, 2, One, OnePointFive, Two
        /// </summary>
        public static StopBits ToStopBits(string? stopBitsText, StopBits defaultStopBits = StopBits.One)
        {
            if (string.IsNullOrWhiteSpace(stopBitsText))
                return defaultStopBits;

            string s = stopBitsText.Trim()
                                   .Replace(" ", "")
                                   .Replace("-", "")
                                   .ToUpperInvariant();

            return s switch
            {
                "1" or "ONE" => StopBits.One,
                "1.5" or "ONEPOINTFIVE" or "ONEANDFIVE" => StopBits.OnePointFive,
                "2" or "TWO" => StopBits.Two,
                _ => defaultStopBits
            };
        }

        /// <summary>
        /// Converts string to baud rate (int). Returns default if invalid.
        /// Accepts: "9600", "115200", etc.
        /// </summary>
        public static int ToBaudRate(string? baudText, int defaultBaudRate = 9600)
        {
            if (string.IsNullOrWhiteSpace(baudText))
                return defaultBaudRate;

            string s = baudText.Trim().Replace(" ", "");
            return int.TryParse(s, out int baud) && baud > 0 ? baud : defaultBaudRate;
        }

        /// <summary>
        /// Converts string to data bits (int). Returns default if invalid.
        /// Typical: 7 or 8.
        /// </summary>
        public static int ToDataBits(string? dataBitsText, int defaultDataBits = 8)
        {
            if (string.IsNullOrWhiteSpace(dataBitsText))
                return defaultDataBits;

            string s = dataBitsText.Trim().Replace(" ", "");
            return int.TryParse(s, out int bits) && bits >= 5 && bits <= 9 ? bits : defaultDataBits;
        }
    }
}
