using EasyModbus;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aarohi.Core.DeviceManager
{
    public class RS485Manager : IDisposable
    {

        public class RegisterDefinition
        {
            public string Register_Name { get; set; } = "";
            public string Register_Type { get; set; } = "";        // "HOLDING", "INPUT", "COIL", "DISCRETE"
            public int Register_Number { get; set; }               // Modbus address
            public string Register_Data_Type { get; set; } = "";   // "INT16", "UINT16", "FLOAT", etc.
        }

        public class ConnectionTestResult
        {
            public bool PortOpenOk { get; set; }
            public bool ModbusResponding { get; set; }
            public bool SupportsFC08 { get; set; }
            public string Message { get; set; } = "";
        }


        private readonly object _syncRoot = new object();
        private ModbusClient _client;

        public string PortName { get; }
        public int BaudRate { get; }
        public Parity Parity { get; }
        public StopBits StopBits { get; }
        public int UnitId { get; }

        /// <summary>
        /// Indicates whether the Modbus RTU client is connected.
        /// </summary>
        public bool IsConnected => _client != null && _client.Connected;

        /// <summary>
        /// Raised when connection state changes (true = connected, false = disconnected).
        /// </summary>
        public event Action<bool> ConnectionStateChanged;

        /// <summary>
        /// Raised when any communication error occurs.
        /// </summary>
        public event Action<string, Exception> CommunicationError;

        public RS485Manager(
            string portName,
            int baudRate = 9600,
            Parity parity = Parity.None,
            StopBits stopBits = StopBits.One,
            int unitId = 1)
        {
            if (string.IsNullOrWhiteSpace(portName))
                throw new ArgumentNullException(nameof(portName));

            PortName = portName;
            BaudRate = baudRate;
            Parity = parity;
            StopBits = stopBits;
            UnitId = unitId;

            _client = CreateClient();
        }

        private ModbusClient CreateClient()
        {
            // For RTU, EasyModbus uses COM-port constructor
            var client = new ModbusClient(PortName)
            {
                UnitIdentifier = (byte)UnitId,
                Baudrate = BaudRate,
                Parity = Parity,
                StopBits = StopBits
            };

            return client;
        }

        #region Connection Management

        public void Connect()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (_client == null)
                        _client = CreateClient();

                    if (!_client.Connected)
                    {
                        _client.Connect();
                        ConnectionStateChanged?.Invoke(true);
                    }
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Error while connecting Modbus RTU.", ex);
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
                    CommunicationError?.Invoke("Error while disconnecting Modbus RTU.", ex);
                }
            }
        }

        #endregion

        #region Read Operations

        /// <summary>
        /// Reads holding registers (FC03).
        /// </summary>
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
                    CommunicationError?.Invoke("Error reading holding registers (RTU).", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Reads input registers (FC04).
        /// </summary>
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
                    CommunicationError?.Invoke("Error reading input registers (RTU).", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Reads coils (FC01).
        /// </summary>
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
                    CommunicationError?.Invoke("Error reading coils (RTU).", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Reads discrete inputs (FC02).
        /// </summary>
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
                    CommunicationError?.Invoke("Error reading discrete inputs (RTU).", ex);
                    throw;
                }
            }
        }

        #endregion

        #region Write Operations

        /// <summary>
        /// Writes a single holding register (FC06).
        /// </summary>
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
                    CommunicationError?.Invoke("Error writing single register (RTU).", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Writes multiple holding registers (FC16).
        /// </summary>
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
                    CommunicationError?.Invoke("Error writing multiple registers (RTU).", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Writes a single coil (FC05).
        /// </summary>
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
                    CommunicationError?.Invoke("Error writing single coil (RTU).", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Writes multiple coils (FC15).
        /// </summary>
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
                    CommunicationError?.Invoke("Error writing multiple coils (RTU).", ex);
                    throw;
                }
            }
        }

        #endregion

        public object? ReadSingleRegister(RS485Manager manager, RegisterDefinition reg)
        {
            switch (reg.Register_Type.ToUpperInvariant())
            {
                case "Holding Register":
                    {
                        int[] words = manager.ReadHoldingRegisters(reg.Register_Number, 1);
                        return ConvertByDataType(words, reg.Register_Data_Type);
                    }

                case "Input Status":
                    {
                        int[] words = manager.ReadInputRegisters(reg.Register_Number, 1);
                        return ConvertByDataType(words, reg.Register_Data_Type);
                    }

                case "Coil Status":
                    {
                        bool[] bits = manager.ReadCoils(reg.Register_Number, 1);
                        return bits[0];
                    }

                case "Input Register":
                    {
                        bool[] bits = manager.ReadDiscreteInputs(reg.Register_Number, 1);
                        return bits[0];
                    }

                default:
                    throw new NotSupportedException($"Unknown Register_Type: {reg.Register_Type}");
            }
        }

        private object ConvertByDataType(int[] words, string dataType)
        {
            int raw = words[0];

            switch (dataType.ToUpperInvariant())
            {
                case "INT16":
                    return (short)raw;

                case "UINT16":
                    return (ushort)raw;

                case "INT32":
                    // combine 2 words if needed (make sure you read count=2 above)
                    throw new NotImplementedException("INT32 not wired yet.");

                case "FLOAT":
                    // with EasyModbus you can later use ConvertRegistersToFloat etc.
                    throw new NotImplementedException("FLOAT conversion not wired yet.");

                default:
                    // fallback: just return raw 16-bit
                    return raw;
            }
        }

        public bool TestConnection(
    int testRegisterAddress = 0,
    string testRegisterType = "Holding Register")
        {
            lock (_syncRoot)
            {
                try
                {
                    EnsureConnected();

                    switch (testRegisterType.ToUpperInvariant())
                    {
                        case "HOLDING REGISTER":
                            _client.ReadHoldingRegisters(testRegisterAddress, 1);
                            break;

                        case "INPUT REGISTER":
                            _client.ReadInputRegisters(testRegisterAddress, 1);
                            break;

                        case "COIL":
                            _client.ReadCoils(testRegisterAddress, 1);
                            break;

                        case "DISCRETE INPUT":
                            _client.ReadDiscreteInputs(testRegisterAddress, 1);
                            break;

                        default:
                            throw new ArgumentException("Invalid test register type.");
                    }

                    return true; // ✅ communication success
                }
                catch (Exception ex)
                {
                    CommunicationError?.Invoke("Modbus RTU connection test failed.", ex);
                    return false;
                }
            }
        }

        public async Task<bool> TestConnectionAsync(
    int testRegisterAddress = 0,
    string testRegisterType = "Holding Register",
    CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                return TestConnection(testRegisterAddress, testRegisterType);
            }, cancellationToken);
        }


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
