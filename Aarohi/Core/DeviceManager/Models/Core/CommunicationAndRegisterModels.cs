using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace Aarohi.Core.DeviceManager.Models
{
    public enum DeviceProtocol
    {
        Unknown = 0,
        ModbusRtu = 1,
        ModbusAscii = 2,
        ModbusTcp = 3,
        SerialRs232 = 4,
        SiemensS7 = 5
    }

    public enum CommunicationMode
    {
        Unknown = 0,
        ModbusTcpIp = 1,
        ModbusRs485 = 2,
        SerialRs232 = 3,
        SiemensS7 = 4
    }

    public enum DeviceType
    {
        Unknown = 0,
        FlowMeter = 1,
        PressureTransmitter = 2,
        TemperatureController = 3,
        VFD = 4,
        PLC = 5,
        EnergyMeter = 6,
        LevelTransmitter = 7,
        ValveController = 8,
        Other = 99
    }

    public enum RegisterArea
    {
        Coil = 1,
        DiscreteInput = 2,
        InputRegister = 3,
        HoldingRegister = 4
    }

    public enum RegisterDataType
    {
        Bool = 1,
        Int16 = 2,
        UInt16 = 3,
        Int32 = 4,
        UInt32 = 5,
        Float = 6,
        Double = 7,
        Int64 = 8,
        UInt64 = 9,
        String = 10,
        Char = 11
    }

    public enum RegisterAccess
    {
        ReadOnly = 1,
        WriteOnly = 2,
        ReadWrite = 3
    }

    public enum RegisterDefinitionType
    {
        ModbusRegister = 1,
        VMreg = 2,
        S7Register = 3
    }

    public enum ByteOrderType
    {
        Default = 0,
        ABCD = 1,
        BADC = 2,
        CDAB = 3,
        DCBA = 4
    }

    public class CommunicationSettings
    {
        public CommunicationMode Mode { get; set; } = CommunicationMode.ModbusRs485;

        public ModbusCommonSettings Modbus { get; set; } = new();

        public SerialPortSettings Serial { get; set; } = new();

        public TcpIpSettings TcpIp { get; set; } = new();

        public S7Settings S7 { get; set; } = new();

        public bool IsModbus =>
            Mode == CommunicationMode.ModbusRs485 ||
            Mode == CommunicationMode.ModbusTcpIp;

        public bool IsSerial =>
            Mode == CommunicationMode.ModbusRs485 ||
            Mode == CommunicationMode.SerialRs232;

        public bool IsTcp =>
            Mode == CommunicationMode.ModbusTcpIp ||
            Mode == CommunicationMode.SiemensS7;
    }

    public class ModbusCommonSettings
    {
        /// <summary>
        /// Slave ID / Unit ID / Station ID.
        /// For Modbus TCP this can still be used as Unit Identifier.
        /// </summary>
        public int SlaveId { get; set; } = 1;

        public int TimeoutMs { get; set; } = 1000;

        public int RetryCount { get; set; } = 1;

        public int PollingIntervalMs { get; set; } = 1000;
    }

    public class SerialPortSettings
    {
        public string PortName { get; set; } = "COM1";

        public int BaudRate { get; set; } = 9600;

        public Parity Parity { get; set; } = Parity.None;

        public int DataBits { get; set; } = 8;

        public StopBits StopBits { get; set; } = StopBits.One;
    }

    public class TcpIpSettings
    {
        public string IpAddress { get; set; } = "192.168.0.1";

        public int Port { get; set; } = 502;

        public bool KeepAlive { get; set; } = true;
    }

    public class S7Settings
    {
        public int Rack { get; set; } = 0;

        public int Slot { get; set; } = 1;

        public int ConnectionType { get; set; } = 1;

        public int TimeoutMs { get; set; } = 3000;
    }

    public class RegisterDefinition
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Internal unique register name.
        /// Example: FlowRate, Totalizer, ResetTotalizer
        /// </summary>
        public string RegisterName { get; set; } = string.Empty;

        /// <summary>
        /// UI display name.
        /// Example: Flow Rate, Totalizer
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Coil / DiscreteInput / InputRegister / HoldingRegister
        /// </summary>
        public RegisterArea RegisterArea { get; set; } = RegisterArea.HoldingRegister;

        /// <summary>
        /// User-visible Modbus address.
        /// Example: 40001, 30001, 1 etc.
        /// </summary>
        public int Address { get; set; }

        /// <summary>
        /// Zero-based address if needed internally.
        /// Example: for 40001 this may be 0.
        /// </summary>
        public int StartAddress { get; set; }

        /// <summary>
        /// Siemens S7 DB address.
        /// Examples: DB1.DBX10.2, DB1.DBW2, DB1.DBD0, DB1.DBS10:20.
        /// </summary>
        public string S7Address { get; set; } = string.Empty;

        /// <summary>
        /// Data type of this register.
        /// </summary>
        public RegisterDataType DataType { get; set; } = RegisterDataType.UInt16;

        /// <summary>
        /// ReadOnly / WriteOnly / ReadWrite
        /// </summary>
        public RegisterAccess Access { get; set; } = RegisterAccess.ReadOnly;

        /// <summary>
        /// ModbusRegister / VMreg.
        /// VMreg is a virtual memory register calculated from other register values.
        /// </summary>
        public RegisterDefinitionType Type { get; set; } = RegisterDefinitionType.ModbusRegister;

        /// <summary>
        /// Expression used when Type is VMreg.
        /// Example: (40001_Tag + 40002_Tag + 40003_Tag) / 3
        /// </summary>
        public string Expression { get; set; } = string.Empty;

        /// <summary>
        /// Function code used for read.
        /// Example: 1,2,3,4
        /// </summary>
        public int? FunctionCodeRead { get; set; }

        /// <summary>
        /// Function code used for write.
        /// Example: 5,6,15,16
        /// </summary>
        public int? FunctionCodeWrite { get; set; }

        /// <summary>
        /// Number of registers occupied.
        /// Bool / Int16 / UInt16 = 1
        /// Int32 / UInt32 / Float = 2
        /// Int64 / UInt64 / Double = 4
        /// String can vary. Char = 1 byte/register.
        /// </summary>
        public int RegisterCount { get; set; } = 1;

        /// <summary>
        /// Multiply raw value by Scale.
        /// Example: raw 1234, scale 0.1 => 123.4
        /// </summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>
        /// FinalValue = (RawValue * Scale) + Offset
        /// </summary>
        public double Offset { get; set; } = 0.0;

        public string Unit { get; set; } = string.Empty;

        /// <summary>
        /// Display format like "0.00", "0.000", etc.
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Byte order for multi-register values.
        /// Mainly useful for Int32, Float, Double, etc.
        /// </summary>
        public ByteOrderType ByteOrder { get; set; } = ByteOrderType.Default;

        /// <summary>
        /// Bit index used when a single bit is mapped from a register.
        /// Optional.
        /// </summary>
        public int? BitIndex { get; set; }

        public bool IsAlarmTag { get; set; } = false;

        public bool IsLogged { get; set; } = true;

        public bool IsVisible { get; set; } = true;

        public string Category { get; set; } = string.Empty;

        public object? DefaultValue { get; set; }

        public double? MinValue { get; set; }

        public double? MaxValue { get; set; }

        public Dictionary<string, string> ExtraProperties { get; set; } = new();

        public string UniqueKey =>
            Type == RegisterDefinitionType.S7Register && !string.IsNullOrWhiteSpace(S7Address)
                ? $"S7_{S7Address}_{RegisterName}"
                : $"{RegisterArea}_{Address}_{RegisterName}";

        public void AutoSetDefaults()
        {
            RegisterCount = GetDefaultRegisterCount(DataType);

            if (!FunctionCodeRead.HasValue)
                FunctionCodeRead = GetDefaultReadFunctionCode(RegisterArea);

            if (!FunctionCodeWrite.HasValue)
                FunctionCodeWrite = GetDefaultWriteFunctionCode(RegisterArea, Access);
        }

        public static int GetDefaultRegisterCount(RegisterDataType dataType)
        {
            switch (dataType)
            {
                case RegisterDataType.Bool:
                case RegisterDataType.Int16:
                case RegisterDataType.UInt16:
                    return 1;

                case RegisterDataType.Int32:
                case RegisterDataType.UInt32:
                case RegisterDataType.Float:
                    return 2;

                case RegisterDataType.Int64:
                case RegisterDataType.UInt64:
                case RegisterDataType.Double:
                    return 4;

                case RegisterDataType.String:
                case RegisterDataType.Char:
                    return 1;

                default:
                    return 1;
            }
        }

        public static int? GetDefaultReadFunctionCode(RegisterArea area)
        {
            switch (area)
            {
                case RegisterArea.Coil:
                    return 1;
                case RegisterArea.DiscreteInput:
                    return 2;
                case RegisterArea.HoldingRegister:
                    return 3;
                case RegisterArea.InputRegister:
                    return 4;
                default:
                    return null;
            }
        }

        public static int? GetDefaultWriteFunctionCode(RegisterArea area, RegisterAccess access)
        {
            if (access == RegisterAccess.ReadOnly)
                return null;

            switch (area)
            {
                case RegisterArea.Coil:
                    return 5;
                case RegisterArea.HoldingRegister:
                    return 6;
                case RegisterArea.DiscreteInput:
                case RegisterArea.InputRegister:
                    return null;
                default:
                    return null;
            }
        }
    }
}
