using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Aarohi.Core.PLC
{
    public sealed class ClassPLC : IDisposable
    {
        private readonly S7Client _client = new();
        private readonly object _sync = new();

        public string IpAddress { get; }
        public int Rack { get; }
        public int Slot { get; }

        public int ConnectionType { get; set; } = S7Client.CONNTYPE_PG;

        public int ConnectTimeoutMs { get; set; } = 3000;

        public bool IsConnected { get { lock (_sync) return _client.Connected(); } }
        public string LastError { get; private set; } = string.Empty;

        public enum PlcAccess { Read, Write, ReadWrite }
        public enum PlcDataType { Bool, Int16, UInt16, Int32, UInt32, Real, Byte, Word, DWord, DInt, DWordU, S7String, CharArray }
        public Action<string>? Logger { get; set; }

        private void Log(string s) { try { Logger?.Invoke(s); } catch { } }
        private void SetLastError(string message)
        {
            LastError = message ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(LastError))
                Log(LastError);
        }

        public int SubscriptionCount
        {
            get { lock (_subsSync) return _subs.Count; }
        }

        public sealed class PlcMetaData
        {
            public string? ModuleTypeName { get; set; }
            public string? SerialNumber { get; set; }
            public string? ASName { get; set; }
            public string? ModuleName { get; set; }
            public string? Copyright { get; set; }
            public string? OrderCode { get; set; }
            public string? FirmwareVersion { get; set; }
        }

        private sealed class Sub
        {
            public Guid Id { get; } = Guid.NewGuid();
            public PlcAccess Access { get; init; }
            public PlcDataType DataType { get; init; }

            public int Db { get; init; }
            public int StartByte { get; init; }   // byte offset inside DB
            public int? Bit { get; init; }        // for Bool (0..7)
            public int SizeBytes { get; init; }   // 1/2/4 depending on datatype (Bool=1)
            public int? Length { get; init; } 

            public Action<object?>? OnRead { get; init; }

            // write-side
            public volatile bool Dirty;
            public object? Value;                 // last write value/request
        }

        private readonly Dictionary<Guid, Sub> _subs = new();
        private readonly Dictionary<int, List<Sub>> _subsByDb = new();
        private readonly object _subsSync = new();


        public Guid Subscribe(
            string address,
            PlcAccess access,
            PlcDataType type,
            Action<object?>? onRead = null,
            object? initialWrite = null)
        {
            ParseAddress(address, type, out int db, out int start, out int? bit, out int size, out int? len);

            var sub = new Sub
            {
                Access = access,
                DataType = type,
                Db = db,
                StartByte = start,
                Bit = bit,
                SizeBytes = size,
                Length = len,
                OnRead = onRead,
                Dirty = initialWrite is not null,
                Value = initialWrite
            };


            lock (_subsSync)
            {
                _subs[sub.Id] = sub;
                if (!_subsByDb.TryGetValue(db, out var list))
                    _subsByDb[db] = list = new List<Sub>();
                list.Add(sub);
            }
            return sub.Id;
        }

        public bool Unsubscribe(Guid id)
        {
            lock (_subsSync)
            {
                if (!_subs.Remove(id, out var sub)) return false;
                if (_subsByDb.TryGetValue(sub.Db, out var list))
                {
                    list.RemoveAll(s => s.Id == id);
                    if (list.Count == 0) _subsByDb.Remove(sub.Db);
                }
                return true;
            }
        }

        public void UnsubscribeAll()
        {
            lock (_subsSync)
            {
                foreach (var dbList in _subsByDb.Values)
                    dbList.Clear();

                _subsByDb.Clear();
                _subs.Clear();
            }
            Log("All subscriptions removed (UnsubscribeAll).");
        }


        public bool SetWriteValue(Guid id, object? value)
        {
            lock (_subsSync)
            {
                if (!_subs.TryGetValue(id, out var sub)) return false;
                sub.Value = value;
                sub.Dirty = true;
                return true;
            }
        }

        public void PollOnce()
        {
            EnsureConnected();
            LastError = string.Empty;

            List<KeyValuePair<int, List<Sub>>> groups;
            lock (_subsSync)
                groups = _subsByDb.ToList(); // snapshot

            if (groups.Count == 0)
            {
                Log("PollOnce: no subscription groups.");
                return;
            }

            foreach (var kv in groups)
            {
                int db = kv.Key;
                var list = kv.Value;
                if (list.Count == 0) continue;

                // ---------- READ ----------
                try
                {
                    var readers = list.Where(s => s.Access != PlcAccess.Write).ToList();
                    if (readers.Count > 0)
                    {
                        int readStart = readers.Min(s => s.StartByte);
                        int readEnd = readers.Max(s =>
                            s.DataType == PlcDataType.Bool && s.Bit.HasValue
                                ? s.StartByte
                                : s.StartByte + s.SizeBytes - 1);

                        int readSize = readEnd - readStart + 1;
                        var rbuf = new byte[readSize];

                        Log($"PollOnce[DB{db}]: READ span {readStart}..{readEnd} ({readSize} bytes) for {readers.Count} subs");

                        int rc = _client.DBRead(db, readStart, readSize, rbuf);
                        if (rc != 0)
                        {
                            SetLastError($"PollOnce[DB{db}]: DBRead failed rc={rc} '{_client.ErrorText(rc)}'");
                        }
                        else
                        {
                            foreach (var s in readers)
                            {
                                int ofs = s.StartByte - readStart;
                                object? val = Unpack(rbuf, ofs, s.DataType, s.Bit, s.Length);

                                try { s.OnRead?.Invoke(val); } catch (Exception cbEx) { Log($"OnRead cb error: {cbEx.Message}"); }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SetLastError($"PollOnce[DB{db}]: READ phase exception: {ex.Message}");
                }

                // ---------- WRITE ----------
                try
                {
                    var writers = list.Where(s => s.Access != PlcAccess.Read && s.Dirty).ToList();
                    if (writers.Count > 0)
                    {
                        int wStart = writers.Min(s => s.StartByte);
                        int wEnd = writers.Max(s =>
                            s.DataType == PlcDataType.Bool && s.Bit.HasValue
                                ? s.StartByte
                                : s.StartByte + s.SizeBytes - 1);
                        int wSize = wEnd - wStart + 1;
                        var wbuf = new byte[wSize];

                        Log($"PollOnce[DB{db}]: WRITE span {wStart}..{wEnd} ({wSize} bytes) for {writers.Count} subs");

                        // read existing span first (to preserve other bits/bytes)
                        int rcR = _client.DBRead(db, wStart, wSize, wbuf);
                        if (rcR != 0)
                        {
                            SetLastError($"PollOnce[DB{db}]: prewrite DBRead failed rc={rcR} '{_client.ErrorText(rcR)}'");
                            continue;
                        }

                        foreach (var s in writers)
                        {
                            int ofs = s.StartByte - wStart;
                            Pack(wbuf, ofs, s.DataType, s.Value, s.Bit, s.Length);

                        }

                        int rcW = _client.DBWrite(db, wStart, wSize, wbuf);
                        if (rcW != 0)
                        {
                            SetLastError($"PollOnce[DB{db}]: DBWrite failed rc={rcW} '{_client.ErrorText(rcW)}'");
                        }
                        else
                        {
                            foreach (var s in writers) s.Dirty = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    SetLastError($"PollOnce[DB{db}]: WRITE phase exception: {ex.Message}");
                }
            }
        }

        public bool TryPollOnce(out string? error)
        {
            try
            {
                PollOnce();
                error = string.IsNullOrWhiteSpace(LastError) ? null : LastError;
                return error == null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                SetLastError(error);
                return false;
            }
        }


        // ----------------- Helpers -----------------

        private static object? Unpack(byte[] buf, int ofs, PlcDataType type, int? bit, int? len)
        {
            switch (type)
            {
                case PlcDataType.Bool:
                    return bit.HasValue ? (buf[ofs] & (1 << bit.Value)) != 0 : buf[ofs] != 0;

                case PlcDataType.Byte:
                    return buf[ofs];

                case PlcDataType.Int16:
                    return (short)S7.GetIntAt(buf, ofs);

                case PlcDataType.UInt16:
                case PlcDataType.Word:
                    return S7.GetWordAt(buf, ofs);

                case PlcDataType.Int32:
                case PlcDataType.DInt:
                    return S7.GetDIntAt(buf, ofs);

                case PlcDataType.UInt32:
                case PlcDataType.DWord:
                    return S7.GetDWordAt(buf, ofs);

                case PlcDataType.Real:
                    return S7.GetRealAt(buf, ofs);

                case PlcDataType.S7String:
                    {
                        // STRING[n]: [max][cur][chars...]
                        int max = buf[ofs];
                        int cur = buf[ofs + 1];
                        int n = len ?? (max); // prefer provided len
                        if (cur < 0) cur = 0;
                        if (cur > n) cur = n;

                        return Encoding.ASCII.GetString(buf, ofs + 2, cur);
                    }

                case PlcDataType.CharArray:
                    {
                        int n = len ?? 0;
                        if (n <= 0) return "";
                        var s = Encoding.ASCII.GetString(buf, ofs, n);
                        return s.TrimEnd('\0', ' ');
                    }

                default:
                    return null;
            }
        }


        private static void Pack(byte[] buf, int ofs, PlcDataType type, object? value, int? bit, int? len)
        {
            switch (type)
            {
                case PlcDataType.Bool:
                    bool b = Convert.ToBoolean(value ?? false);
                    if (bit.HasValue)
                    {
                        if (b) buf[ofs] = (byte)(buf[ofs] | (1 << bit.Value));
                        else buf[ofs] = (byte)(buf[ofs] & ~(1 << bit.Value));
                    }
                    else buf[ofs] = (byte)(b ? 1 : 0);
                    break;

                case PlcDataType.Byte:
                    buf[ofs] = Convert.ToByte(value ?? 0);
                    break;

                case PlcDataType.Int16:
                    S7.SetIntAt(buf, ofs, Convert.ToInt16(value ?? 0));
                    break;

                case PlcDataType.UInt16:
                case PlcDataType.Word:
                    S7.SetWordAt(buf, ofs, Convert.ToUInt16(value ?? 0));
                    break;

                case PlcDataType.Int32:
                case PlcDataType.DInt:
                    S7.SetDIntAt(buf, ofs, Convert.ToInt32(value ?? 0));
                    break;

                case PlcDataType.UInt32:
                case PlcDataType.DWord:
                    S7.SetDWordAt(buf, ofs, Convert.ToUInt32(value ?? 0));
                    break;

                case PlcDataType.Real:
                    S7.SetRealAt(buf, ofs, Convert.ToSingle(value ?? 0f));
                    break;

                case PlcDataType.S7String:
                    {
                        int n = len ?? throw new ArgumentException("S7String requires length.");
                        string s = Convert.ToString(value ?? "") ?? "";
                        if (s.Length > n) s = s.Substring(0, n);

                        buf[ofs] = (byte)n;           // MaxLen
                        buf[ofs + 1] = (byte)s.Length; // CurLen

                        // Clear old area then copy
                        Array.Clear(buf, ofs + 2, n);
                        Encoding.ASCII.GetBytes(s, 0, s.Length, buf, ofs + 2);
                        break;
                    }

                case PlcDataType.CharArray:
                    {
                        int n = len ?? throw new ArgumentException("CharArray requires length.");
                        string s = Convert.ToString(value ?? "") ?? "";
                        if (s.Length > n) s = s.Substring(0, n);

                        Array.Clear(buf, ofs, n);
                        Encoding.ASCII.GetBytes(s, 0, s.Length, buf, ofs);
                        break;
                    }
            }
        }


        private static void ParseAddress(
    string s, PlcDataType type,
    out int db, out int startByte, out int? bit, out int sizeBytes, out int? len)
        {
            // Supports:
            //  DB1.DBX10.2          Bool
            //  DB1.DBD0             Real/Int32/UInt32
            //  DB1.DBW2             Int16/UInt16
            //  DB1.DBB20            Byte
            //  DB1.DBS10:20         S7 STRING[20] at byte 10  (total 22 bytes)
            //  DB1.DBC10:20         CHAR[20] at byte 10       (total 20 bytes)

            var up = s.ToUpperInvariant().Replace(" ", "");

            var m = Regex.Match(up,
                @"^DB(?<db>\d+)\.(?<area>DBX|DBD|DBW|DBB|DBS|DBC)(?<ofs>\d+)(?:\.(?<bit>[0-7]))?(?::(?<len>\d+))?$");
            if (!m.Success) throw new ArgumentException($"Invalid DB address: {s}");

            db = int.Parse(m.Groups["db"].Value);
            startByte = int.Parse(m.Groups["ofs"].Value);
            bit = m.Groups["bit"].Success ? int.Parse(m.Groups["bit"].Value) : null;

            len = m.Groups["len"].Success ? int.Parse(m.Groups["len"].Value) : (int?)null;

            // If address explicitly says DBS/DBC, force type consistency
            var area = m.Groups["area"].Value;
            if (area == "DBS" && type != PlcDataType.S7String)
                throw new ArgumentException($"Address {s} implies S7String, but type is {type}");
            if (area == "DBC" && type != PlcDataType.CharArray)
                throw new ArgumentException($"Address {s} implies CharArray, but type is {type}");

            sizeBytes = type switch
            {
                PlcDataType.Bool => 1,
                PlcDataType.Byte => 1,

                PlcDataType.Int16 => 2,
                PlcDataType.UInt16 => 2,
                PlcDataType.Word => 2,

                PlcDataType.Int32 => 4,
                PlcDataType.DInt => 4,
                PlcDataType.UInt32 => 4,
                PlcDataType.DWord => 4,
                PlcDataType.Real => 4,

                PlcDataType.S7String => (len ?? throw new ArgumentException($"S7String requires length. Use DBx.DBSy:len")) + 2,
                PlcDataType.CharArray => (len ?? throw new ArgumentException($"CharArray requires length. Use DBx.DBCy:len")),

                _ => 4
            };
        }


        public ClassPLC(string ipAddress, int rack = 0, int slot = 1)
        {
            IpAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            Rack = rack;
            Slot = slot;
        }

        public void Connect()
        {
            lock (_sync)
            {
                if (_client.Connected()) return;

                _client.SetConnectionType((ushort)ConnectionType);

                int rc = _client.ConnectTo(IpAddress, Rack, Slot);
                if (rc != 0)
                    throw ToSocketOrGeneric(rc,
                        $"PLC connect failed (IP={IpAddress}, Rack={Rack}, Slot={Slot}): {_client.ErrorText(rc)}");
            }
        }

        public bool TryConnect(out string? error)
        {
            try { Connect(); error = null; return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public async Task ConnectAsync(CancellationToken ct = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var t = Task.Run(Connect, cts.Token);

            if (await Task.WhenAny(t, Task.Delay(ConnectTimeoutMs, cts.Token)) != t)
            {
                cts.Cancel();
                throw new TimeoutException($"PLC connect timed out after {ConnectTimeoutMs} ms (IP={IpAddress}).");
            }
            await t; // propagate errors
        }

        public void Disconnect()
        {
            lock (_sync)
            {
                try { _client.Disconnect(); } catch { /* ignore */ }
            }
        }

        public bool CheckAlive()
        {
            lock (_sync)
            {
                if (_client.Connected())
                {
                    var info = new S7Client.S7CpuInfo();
                    return _client.GetCpuInfo(ref info) == 0;
                }
            }

            var probe = new S7Client();
            try
            {
                probe.SetConnectionType((ushort)ConnectionType);
                int rc = probe.ConnectTo(IpAddress, Rack, Slot);
                if (rc != 0) return false;

                var info = new S7Client.S7CpuInfo();
                return probe.GetCpuInfo(ref info) == 0;
            }
            finally
            {
                try { probe.Disconnect(); } catch { /* ignore */ }
            }
        }

        public void EnsureConnected()
        {
            if (IsConnected) return;
            Connect();
        }

        private static Exception ToSocketOrGeneric(int snap7Err, string message)
        {
            return new InvalidOperationException(message);
        }

        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }

        public PlcMetaData GetMeta()
        {
            EnsureConnected();

            var meta = new PlcMetaData();

            lock (_sync)
            {
                // -------- CPU INFO --------
                var cpuInfo = new S7Client.S7CpuInfo();
                int rc1 = _client.GetCpuInfo(ref cpuInfo);

                if (rc1 == 0)
                {
                    meta.ModuleTypeName = cpuInfo.ModuleTypeName?.Trim();
                    meta.SerialNumber = cpuInfo.SerialNumber?.Trim();
                    meta.ASName = cpuInfo.ASName?.Trim();
                    meta.ModuleName = cpuInfo.ModuleName?.Trim();
                    meta.Copyright = cpuInfo.Copyright?.Trim();
                }
                else
                {
                    Log($"GetCpuInfo failed: {_client.ErrorText(rc1)}");
                }

                // -------- ORDER CODE --------
                var order = new S7Client.S7OrderCode();
                int rc2 = _client.GetOrderCode(ref order);

                if (rc2 == 0)
                {
                    meta.OrderCode = order.Code?.Trim();
                    meta.FirmwareVersion = $"V{order.V1}.{order.V2}.{order.V3}";
                }
                else
                {
                    Log($"GetOrderCode failed: {_client.ErrorText(rc2)}");
                }
            }

            return meta;
        }

    }
}

