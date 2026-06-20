using Microsoft.Win32;
using System.Runtime.InteropServices;
using NM = ResMan.Services.NativeMethods;

namespace ResMan.Services
{
    public static class MonitorService
    {
        private static readonly int[] DpiVals = { 100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500 };

        private static NM.DEVMODE CreateDevMode() => new()
        {
            dmSize = (ushort)Marshal.SizeOf<NM.DEVMODE>()
        };

        private static NM.DISPLAY_DEVICE CreateDisplayDevice() => new()
        {
            cb = Marshal.SizeOf<NM.DISPLAY_DEVICE>()
        };

        public static MonitorInfo GetMainMonitorInfo()
        {
            var info = new MonitorInfo();

            // Get primary display device name
            var dd = CreateDisplayDevice();

            uint devIndex = 0;

            NM.DISPLAY_DEVICE primary = dd;

            while (NM.EnumDisplayDevices(null, devIndex, ref dd, 0))
            {
                if ((dd.StateFlags & NM.DisplayDeviceStateFlags.PrimaryDevice) != 0)
                {
                    primary = dd;
                    break;
                }

                devIndex++;
                dd = CreateDisplayDevice();
            }

            // Get current settings
            var dm = CreateDevMode();

            int res = NM.EnumDisplaySettings(primary.DeviceName, NM.ENUM_CURRENT_SETTINGS, ref dm);

            if (res != 0)
            {
                info = new MonitorInfo
                {
                    Width = (int)dm.dmPelsWidth,
                    Height = (int)dm.dmPelsHeight,
                    RefreshRate = (int)dm.dmDisplayFrequency,
                    ScalingPercentage = GetSystemScalingPercent()
                };
            }

            return info;
        }

        private static int GetSystemScalingPercent()
        {
            try
            {
                IntPtr primaryMonitor = NM.MonitorFromWindow(IntPtr.Zero, NM.MONITOR_DEFAULTTOPRIMARY);

                int hr = NM.GetDpiForMonitor(primaryMonitor, NM.MonitorDpiType.MDT_Effective_DPI, out uint dpiX, out uint dpiY);
                
                if (hr == 0)
                {
                    // DPI of 96 is 100%
                    return (int)Math.Round(dpiX * 100.0 / 96.0);
                }
            }
            catch
            {
            }

            // Fallback to reading HKCU LogPixels
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", false);

                if (key != null)
                {
                    var val = key.GetValue("LogPixels");

                    if (val is int lp)
                    {
                        return (int)Math.Round(lp * 100.0 / 96.0);
                    }
                }
            }
            catch
            {
            }

            return 100;
        }

        public static bool SetMainResolution(int width, int height, int refreshRate)
        {
            // Find primary device name
            var dd = CreateDisplayDevice();
            uint devIndex = 0;
            var primary = dd;

            while (NM.EnumDisplayDevices(null, devIndex, ref dd, 0))
            {
                if ((dd.StateFlags & NM.DisplayDeviceStateFlags.PrimaryDevice) != 0)
                {
                    primary = dd;
                    break;
                }

                devIndex++;
                dd = CreateDisplayDevice();
            }

            var dm = CreateDevMode();

            int r = NM.EnumDisplaySettings(primary.DeviceName, NM.ENUM_CURRENT_SETTINGS, ref dm);

            if (r == 0) return false;

            dm.dmPelsWidth = (uint)width;
            dm.dmPelsHeight = (uint)height;
            dm.dmDisplayFrequency = (uint)refreshRate;
            dm.dmFields = NM.DM_PELSWIDTH | NM.DM_PELSHEIGHT | NM.DM_DISPLAYFREQUENCY;

            int changeResult = NM.ChangeDisplaySettingsEx(
                primary.DeviceName,
                ref dm,
                IntPtr.Zero,
                NM.CDS_UPDATEREGISTRY,
                IntPtr.Zero
            );

            return changeResult == NM.DISP_CHANGE_SUCCESSFUL;
        }

        public static bool SetMainScaling(int scalingPercent)
        {
            // try to set LogPixels in registry first (legacy fallback)
            int dpi = PercentToDpi(scalingPercent);

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("Control Panel\\Desktop", true);

                key?.SetValue("LogPixels", dpi, RegistryValueKind.DWord);
            }
            catch
            {
                // ignore, try the system parameters API below
            }

            // try using SystemParametersInfo to apply DPI scaling override (preferred when available)
            try
            {
                int recommendedDpi = GetRecommendedDPIScaling();

                if (recommendedDpi > 0)
                {
                    int index = 0, recIndex = 0, setIndex = 0;

                    foreach (var scale in DpiVals)
                    {
                        if (recommendedDpi == scale)
                        {
                            recIndex = index;
                        }

                        if (scalingPercent == scale)
                        {
                            setIndex = index;
                        }

                        index++;
                    }

                    int relativeIndex = setIndex - recIndex;
                    int param = relativeIndex;
                    bool res = NM.SystemParametersInfo(
                        NM.SPI_SETLOGICALDPIOVERRIDE,
                        (uint)relativeIndex,
                        ref param,
                        NM.SPIF_UPDATEINIFILE | NM.SPIF_SENDCHANGE
                    );

                    if (res)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // fall through to notify change via WM_SETTINGCHANGE
            }

            // notify other apps about the change
            try
            {
                _ = NM.SendMessageTimeout(
                    new IntPtr(0xffff),
                    NM.WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "WindowMetrics",
                    NM.SMTO_ABORTIFHUNG,
                    5000,
                    out _
                );
            }
            catch
            {
            }

            return true;
        }

        private static int GetRecommendedDPIScaling()
        {
            int dpi = 0;

            try
            {
                bool retval = NM.SystemParametersInfo(NM.SPI_GETLOGICALDPIOVERRIDE, 0, ref dpi, 0);

                if (retval)
                {
                    // the system returns an index offset as a negative value; map to actual DPI percent
                    int idx = dpi * -1;

                    if (idx >= 0 && idx < DpiVals.Length)
                    {
                        return DpiVals[idx];
                    }
                }
            }
            catch
            {
            }

            return -1;
        }

        private static int PercentToDpi(int percent)
        {
            return percent switch
            {
                100 => 96,
                125 => 120,
                150 => 144,
                175 => 168,
                200 => 192,
                _ => (int)Math.Round(96 * percent / 100.0)
            };
        }
    }
}
