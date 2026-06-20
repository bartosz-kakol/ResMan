using ResMan.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ResMan
{
    public sealed class RunnerSetup
    {
        public string ExecutablePath { set; get; } = "";
        public Size? Resolution { set; get; } = null;
        public int? RefreshRate { set; get; } = null;
        public int? ScalingPercentage { set; get; } = null;
    }

    public sealed class RunnerMonitorInfoDiff
    {
        public required MonitorInfo Original { set; get; }
        public required MonitorInfo Target { set; get; }
        public bool ResolutionChanged { set; get; } = false;
        public bool RefreshRateChanged { set; get; } = false;
        public bool ScalingPercentageChanged { set; get; } = false;
    }

    public class Runner(RunnerSetup setup)
    {
        private RunnerMonitorInfoDiff? diff = null;

        private void CreateDiff()
        {
            var originalMonitorInfo = MonitorService.GetMainMonitorInfo();

            diff = new RunnerMonitorInfoDiff
            {
                Original = originalMonitorInfo,
                Target = new MonitorInfo
                {
                    Width = setup.Resolution?.Width ?? originalMonitorInfo.Width,
                    Height = setup.Resolution?.Height ?? originalMonitorInfo.Height,
                    RefreshRate = setup.RefreshRate ?? originalMonitorInfo.RefreshRate,
                    ScalingPercentage = setup.ScalingPercentage ?? originalMonitorInfo.ScalingPercentage,
                },
                ResolutionChanged = setup.Resolution != null,
                RefreshRateChanged = setup.RefreshRate != null,
                ScalingPercentageChanged = setup.ScalingPercentage != null,
            };
        }

        private void Rollback()
        {
            if (diff == null)
            {
                throw new InvalidOperationException("Original monitor info is not available yet. Call Run() first.");
            }

            MonitorService.SetMainResolution(diff.Original.Width, diff.Original.Height, diff.Original.RefreshRate);
            MonitorService.SetMainScaling(diff.Original.ScalingPercentage);
        }

        private void ApplySetup()
        {
            if (diff == null)
            {
                throw new InvalidOperationException("Original monitor info is not available yet. Call Run() first.");
            }

            if (diff.ResolutionChanged || diff.RefreshRateChanged)
            {
                MonitorService.SetMainResolution(diff.Target.Width, diff.Target.Height, diff.Target.RefreshRate);
            }

            if (diff.ScalingPercentageChanged)
            {
                MonitorService.SetMainScaling(diff.Target.ScalingPercentage);
            }
        }

        public void Run()
        {
            CreateDiff();

            try
            {
                ApplySetup();

                Task.Delay(1000).Wait();

                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = setup.ExecutablePath,
                });
                process!.WaitForExit();
            }
            finally
            {
                Rollback();
            }
        }
    }
}
