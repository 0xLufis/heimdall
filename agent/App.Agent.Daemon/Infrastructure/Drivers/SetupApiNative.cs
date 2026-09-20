namespace App.Agent.Daemon.Infrastructure.Drivers;

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

[SupportedOSPlatform("windows")]
public static class SetupApiNative
{
    /// <summary>
    /// Standard Windows Device Setup Class GUIDs defined by Microsoft in the Windows Driver Kit (WDK) header &lt;devguid.h&gt;.
    /// Device setup classes specify the class of a device that SetupDi APIs enumerate and configure.
    /// These are immutable, standardized operating system constants.
    /// </summary>
    public static class DeviceSetupClasses
    {
        /// <summary>
        /// Class = Net (Network Adapters, NICs, Virtual Network Interfaces).
        /// Defined in &lt;devguid.h&gt; as GUID_DEVCLASS_NET.
        /// </summary>
        public static readonly Guid Net = new("{4d36e972-e325-11ce-bfc1-08002be10318}");

        /// <summary>
        /// Class = System (System devices, HAL, PCI buses, system timers).
        /// Defined in &lt;devguid.h&gt; as GUID_DEVCLASS_SYSTEM.
        /// </summary>
        public static readonly Guid System = new("{4d36e97d-e325-11ce-bfc1-08002be10318}");

        /// <summary>
        /// Class = Ports (Serial and Parallel communication ports, COM ports).
        /// Defined in &lt;devguid.h&gt; as GUID_DEVCLASS_PORTS.
        /// </summary>
        public static readonly Guid Ports = new("{4d36e978-e325-11ce-bfc1-08002be10318}");

        /// <summary>
        /// Class = USB (Universal Serial Bus host controllers and root hubs).
        /// Defined in &lt;devguid.h&gt; as GUID_DEVCLASS_USB.
        /// </summary>
        public static readonly Guid Usb = new("{36fc9e60-c465-11cf-8056-444553540000}");

        /// <summary>
        /// Class = DiskDrive (Hard disk drives, SSDs, storage media).
        /// Defined in &lt;devguid.h&gt; as GUID_DEVCLASS_DISKDRIVE.
        /// </summary>
        public static readonly Guid DiskDrive = new("{4d36e967-e325-11ce-bfc1-08002be10318}");

        /// <summary>
        /// Class = Display (Display adapters, graphics processing units).
        /// Defined in &lt;devguid.h&gt; as GUID_DEVCLASS_DISPLAY.
        /// </summary>
        public static readonly Guid Display = new("{4d36e968-e325-11ce-bfc1-08002be10318}");

        /// <summary>
        /// Class = HIDClass (Human Interface Devices such as keyboards, mice, industrial touch panels).
        /// Defined in &lt;devguid.h&gt; as GUID_DEVCLASS_HIDCLASS.
        /// </summary>
        public static readonly Guid HidClass = new("{745a17a0-74d3-11d0-b6fe-00a0c90f57da}");
    }

    /// <summary>
    /// Standard Windows Device Interface Class GUIDs defined by Microsoft in WDK headers
    /// (&lt;ntddser.h&gt;, &lt;usbiodef.h&gt;, etc.). Device interfaces allow applications to bind to device functional endpoints.
    /// </summary>
    public static class DeviceInterfaceClasses
    {
        /// <summary>
        /// Interface for COM / serial communication ports.
        /// Defined in &lt;ntddser.h&gt; as GUID_DEVINTERFACE_COMPORT.
        /// </summary>
        public static readonly Guid ComPort = new("{86e0d1e0-8089-11d0-9ce4-08003e301f73}");

        /// <summary>
        /// Interface for raw USB devices.
        /// Defined in &lt;usbiodef.h&gt; as GUID_DEVINTERFACE_USB_DEVICE.
        /// </summary>
        public static readonly Guid UsbDevice = new("{a5dcbf10-6530-11d2-901f-00c04fb951ed}");

        /// <summary>
        /// Interface for network adapter device endpoints.
        /// Defined in &lt;netpnp.h&gt; as GUID_DEVINTERFACE_NET.
        /// </summary>
        public static readonly Guid Net = new("{cac88484-7409-4c96-8265-d2978a5f8e52}");

        /// <summary>
        /// Interface for disk storage devices.
        /// Defined in &lt;ntddstor.h&gt; as GUID_DEVINTERFACE_DISK.
        /// </summary>
        public static readonly Guid Disk = new("{53f56307-b6bf-11d0-94f2-00a0c91efb8b}");
    }

    // Standard public aliases maintaining backward compatibility across driver discovery routines
    public static readonly Guid GUID_DEVCLASS_NET = DeviceSetupClasses.Net;
    public static readonly Guid GUID_DEVCLASS_SYSTEM = DeviceSetupClasses.System;
    public static readonly Guid GUID_DEVCLASS_PORTS = DeviceSetupClasses.Ports;
    public static readonly Guid GUID_DEVCLASS_USB = DeviceSetupClasses.Usb;
    public static readonly Guid GUID_DEVINTERFACE_COMPORT = DeviceInterfaceClasses.ComPort;

    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint DIGCF_ALLCLASSES = 0x00000004;

    public const uint SPDRP_DEVICEDESC = 0x00000000;
    public const uint SPDRP_HARDWAREID = 0x00000001;
    public const uint SPDRP_SERVICE = 0x00000004;
    public const uint SPDRP_CLASS = 0x00000007;
    public const uint SPDRP_CLASSGUID = 0x00000008;
    public const uint SPDRP_DRIVER = 0x00000009;
    public const uint SPDRP_MFG = 0x0000000B;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid,
        string? Enumerator,
        IntPtr hwndParent,
        uint Flags);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInfo(
        IntPtr DeviceInfoSet,
        uint MemberIndex,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Property,
        out uint PropertyRegDataType,
        byte[] PropertyBuffer,
        uint PropertyBufferSize,
        out uint RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("cfgmgr32.dll", SetLastError = true)]
    public static extern uint CM_Get_DevNode_Status(
        out uint pdwStatus,
        out uint pdwProblemNumber,
        uint dnDevInst,
        uint ulFlags);
}
