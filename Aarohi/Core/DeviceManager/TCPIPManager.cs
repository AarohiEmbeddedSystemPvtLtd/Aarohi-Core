using System;
using System.Threading;
using System.Threading.Tasks;
using EasyModbus;

namespace Aarohi.Core.DeviceManager
{
    /// <summary>
    /// Simple TCP/IP Modbus manager using EasyModbus.
    /// Handles connect/disconnect and basic read/write operations.
    /// </summary>
    public class TCPIPManager : IDisposable
    {
        public class RegisterDefinition
        {
            public string Register_Name { get; set; } = "";
            public string Register_Type { get; set; } = "";        // "HOLDING", "INPUT", "COIL", "DISCRETE"
            public int Register_Number { get; set; }               // Modbus address
            public string Register_Data_Type { get; set; } = "";   // "INT16", "UINT16", "FLOAT", etc.
        }


        private readonly object _syncRoot = new object();
        private ModbusClient _client;

        public string IpAddress { get; }
        public int Port { get; }
        public int UnitId { get; }

        public bool IsConnected => _client != null && _client.Connected;

        public event Action<bool> ConnectionStateChanged;

        public event Action<string, Exception> CommunicationError;

        public TCPIPManager(string ipAddress, int port = 502, int unitId = 1)
        {
            IpAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
            Port = port;
            UnitId = unitId;

            _client = new ModbusClient(IpAddress, Port)
            {
                UnitIdentifier = (byte)UnitId
            };
        }

        #region Connection Management

        public void Connect()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (_client == null)
                    {
                        _client = new ModbusClient(IpAddress, Port)
                        {
                            UnitIdentifier = (byte)UnitId
                        };
                    }

                    if (!_client.Connected)
                    {
                        _client.Connect();
                        ConnectionStateChanged?.Invoke(true);
                    }
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error while connecting Modbus TCP.", ex);
                    throw;
                }
            }
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(() => Connect(), cancellationToken);
        }

        public void Disconnect()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (_client != null && _client.Connected)
                    {
                        _client.Disconnect();
                        ConnectionStateChanged?.Invoke(false);
                    }
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error while disconnecting Modbus TCP.", ex);
                }
            }
        }

        public bool TestConnection(int testRegisterAddress = 0)
        {
            lock (_syncRoot)
            {
                try
                {
                    var testClient = new ModbusClient(IpAddress, Port);
                    testClient.UnitIdentifier = (byte)UnitId;
                    testClient.ConnectionTimeout = 2000;

                    testClient.Connect();

                    testClient.ReadHoldingRegisters(testRegisterAddress, 1);

                    testClient.Disconnect();
                    return true;

                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Modbus TCP connection test failed.", ex);
                    return false;
                }
            }
        }


        #endregion

        #region Read Operations

        public int[] ReadHoldingRegisters(int startAddress, int count)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                try
                {
                    return _client.ReadHoldingRegisters(startAddress, count);
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error reading holding registers.", ex);
                    throw;
                }
            }
        }

        public int[] ReadInputRegisters(int startAddress, int count)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                try
                {
                    return _client.ReadInputRegisters(startAddress, count);
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error reading input registers.", ex);
                    throw;
                }
            }
        }

        public bool[] ReadCoils(int startAddress, int count)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                try
                {
                    return _client.ReadCoils(startAddress, count);
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error reading coils.", ex);
                    throw;
                }
            }
        }

        public bool[] ReadDiscreteInputs(int startAddress, int count)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                try
                {
                    return _client.ReadDiscreteInputs(startAddress, count);
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error reading discrete inputs.", ex);
                    throw;
                }
            }
        }

        #endregion

        #region Write Operations

        public void WriteSingleRegister(int address, int value)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                try
                {
                    _client.WriteSingleRegister(address, value);
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error writing single register.", ex);
                    throw;
                }
            }
        }

        public void WriteMultipleRegisters(int startAddress, int[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values array must not be null or empty.", nameof(values));

            lock (_syncRoot)
            {
                EnsureConnected();
                try
                {
                    _client.WriteMultipleRegisters(startAddress, values);
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error writing multiple registers.", ex);
                    throw;
                }
            }
        }

        public void WriteSingleCoil(int address, bool value)
        {
            lock (_syncRoot)
            {
                EnsureConnected();
                try
                {
                    _client.WriteSingleCoil(address, value);
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error writing single coil.", ex);
                    throw;
                }
            }
        }

        public void WriteMultipleCoils(int startAddress, bool[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Values array must not be null or empty.", nameof(values));

            lock (_syncRoot)
            {
                EnsureConnected();
                try
                {
                    _client.WriteMultipleCoils(startAddress, values);
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error writing multiple coils.", ex);
                    throw;
                }
            }
        }

        #endregion

        #region Single function

        public static object? ReadSingleRegisterOverTcp(TCPIPManager manager, RegisterDefinition reg)
        {
            if (reg == null)
                throw new ArgumentNullException(nameof(reg));

            string type = reg.Register_Type?.ToUpperInvariant() ?? string.Empty;

            switch (type)
            {
                case "HOLDING":
                    {
                        // For 16-bit values
                        int[] words = manager.ReadHoldingRegisters(reg.Register_Number, 1);
                        return ConvertTcpRegisterValue(words, reg.Register_Data_Type);
                    }

                case "INPUT":
                case "INPUTREGISTER":
                    {
                        int[] words = manager.ReadInputRegisters(reg.Register_Number, 1);
                        return ConvertTcpRegisterValue(words, reg.Register_Data_Type);
                    }

                case "COIL":
                    {
                        bool[] bits = manager.ReadCoils(reg.Register_Number, 1);
                        return bits.Length > 0 ? bits[0] : false;
                    }

                case "DISCRETE":
                case "DISCRETEINPUT":
                case "INPUTBIT":
                    {
                        bool[] bits = manager.ReadDiscreteInputs(reg.Register_Number, 1);
                        return bits.Length > 0 ? bits[0] : false;
                    }

                default:
                    throw new NotSupportedException($"Unknown Register_Type: {reg.Register_Type}");
            }
        }

        private static object ConvertTcpRegisterValue(int[] words, string? dataType)
        {
            if (words == null || words.Length == 0)
                throw new ArgumentException("No data read from TCP/IP register.", nameof(words));

            string dt = (dataType ?? "").ToUpperInvariant();

            int raw = words[0];

            switch (dt)
            {
                case "INT16":
                    return unchecked((short)raw);

                case "UINT16":
                    return unchecked((ushort)raw);

                case "INT32":
                    throw new NotImplementedException("INT32 not implemented yet. Increase register count and combine words.");

                case "FLOAT":
                case "REAL":
                    throw new NotImplementedException("FLOAT not implemented yet. Read 2 registers and use ConvertRegistersToFloat.");

                default:
                    return raw;
            }
        }


        #endregion

        #region Helpers / Dispose

        private void EnsureConnected()
        {
            if (_client == null || !_client.Connected)
            {
                Connect();
            }
        }

        public void Dispose()
        {
            Disconnect();
            lock (_syncRoot)
            {
                _client = null;
            }
        }

        #endregion
    }
}
