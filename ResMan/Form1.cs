using System.Diagnostics;


namespace ResMan
{
    public partial class Form1 : Form
    {
        private const string HELP_URL = "https://wikipedia.org";

        private readonly string[] args = [];

        public Form1()
        {
            InitializeComponent();

            var rawArgs = Environment.GetCommandLineArgs();

            if (rawArgs != null && rawArgs.Length > 1)
            {
                args = [.. rawArgs.Skip(1)];
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Hide();

            if (args.Length == 0)
            {
                var mainForm = new Main();
                mainForm.Show();

                return;
            }

            // process arguments in these formats:

            // only modify refresh rate (e.g. `ResMan.exe 120Hz C:\Path\To\Game.exe`)
            // ResMan.exe RefreshRateHz C:\Path\To\Game.exe

            // only modify scaling (e.g. `ResMan.exe 150% C:\Path\To\Game.exe`)
            // ResMan.exe Scaling% C:\Path\To\Game.exe

            // modify only resolution (e.g. `ResMan.exe 1920x1080 C:\Path\To\Game.exe`)
            // ResMan.exe ResolutionWidthxResolutionHeight C:\Path\To\Game.exe

            // modify resolution and scaling (e.g. `ResMan.exe 1920x1080 150% C:\Path\To\Game.exe`)
            // ResMan.exe ResolutionWidthxResolutionHeight Scaling% C:\Path\To\Game.exe

            // modify resolution and refresh rate (e.g. `ResMan.exe 1920x1080 120Hz C:\Path\To\Game.exe`)
            // ResMan.exe ResolutionWidthxResolutionHeight RefreshRateHz C:\Path\To\Game.exe

            // modify resolution, scaling and refresh rate (e.g. `ResMan.exe 1920x1080 150% 120Hz C:\Path\To\Game.exe`)
            // ResMan.exe ResolutionWidthxResolutionHeight Scaling% RefreshRateHz C:\Path\To\Game.exe

            if (args.Length < 2 || args.Length > 4)
            {
                ConfigurationError("Invalid number of arguments (expected 2, 3 or 4). Please provide the correct arguments.");
                return;
            }

            var setup = new RunnerSetup();
            ParseArgsIntoRunnerSetup(ref setup);

            var runner = new Runner(setup);
            runner.Run();

            Application.Exit();
        }

        private void ParseArgsIntoRunnerSetup(ref RunnerSetup setup)
        {
            foreach (var arg in args)
            {
                if (setup.Resolution == null && arg.Contains('x'))
                {
                    var split = arg.Split('x', StringSplitOptions.RemoveEmptyEntries);

                    if (split.Length == 2)
                    {
                        if (int.TryParse(split[0], out int width) && int.TryParse(split[1], out int height))
                        {
                            setup.Resolution = new Size(width, height);
                        }
                        else
                        {
                            ConfigurationError($"Invalid resolution: {arg}. Expected format is WidthxHeight (e.g. 1920x1080).");
                            return;
                        }

                    }
                    else
                    {
                        ConfigurationError($"Invalid resolution: {arg}. Expected format is WidthxHeight (e.g. 1920x1080).");
                        return;
                    }
                }
                else if (setup.RefreshRate == null && arg.EndsWith("Hz", StringComparison.OrdinalIgnoreCase))
                {
                    var refreshRateStr = arg[..^2];

                    if (int.TryParse(refreshRateStr, out int refreshRate))
                    {
                        setup.RefreshRate = refreshRate;
                    }
                    else
                    {
                        ConfigurationError($"Invalid refresh rate: {arg}. Expected format is RefreshRateHz (e.g. 120Hz).");
                        return;
                    }
                }
                else if (setup.ScalingPercentage == null && arg.EndsWith("%", StringComparison.OrdinalIgnoreCase))
                {
                    var scalingStr = arg[..^1];

                    if (int.TryParse(scalingStr, out int scalingPercentage))
                    {
                        setup.ScalingPercentage = scalingPercentage;
                    }
                    else
                    {
                        ConfigurationError($"Invalid scaling: {arg}. Expected format is Scaling% (e.g. 150%).");
                        return;
                    }
                }
                else if (setup.ExecutablePath.Length == 0 && arg.Any(c => !char.IsDigit(c)))
                {
                    if (!File.Exists(arg))
                    {
                        ConfigurationError($"Executable not found at path: \"{arg}\". Please provide a valid path to the game executable.");
                        return;
                    }

                    setup.ExecutablePath = arg;
                }
                else
                {
                    ConfigurationError($"Could not figure out what \"{arg}\" means. Please provide valid arguments for resolution, refresh rate and/or scaling.");
                    return;
                }
            }

            if (setup.ExecutablePath == null)
            {
                ConfigurationError("No executable path provided. Please provide a valid path to the game executable.");
                return;
            }
        }

        private void ConfigurationError(string message)
        {
            var stringifiedArgs = string.Join(' ', args.Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg));

            var page = new TaskDialogPage
            {
                Caption = "ResMan",
                Heading = "Configuration error",
                Text = message,
                Expander = new TaskDialogExpander
                {
                    Text = stringifiedArgs,
                    ExpandedButtonText = "Received configuration",
                },
                Footnote = new TaskDialogFootnote
                {
                    Text = "Click the Help button to view the documentation.",
                    Icon = TaskDialogIcon.Information,
                },
                Icon = TaskDialogIcon.Error,
                Buttons = { TaskDialogButton.Help, TaskDialogButton.OK },
            };

            page.HelpRequest += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = HELP_URL,
                        UseShellExecute = true
                    });
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open help URL: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            TaskDialog.ShowDialog(page);

            Application.Exit();
        }
    }
}
