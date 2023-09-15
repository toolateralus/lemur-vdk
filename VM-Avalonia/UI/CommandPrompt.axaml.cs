﻿using VM.JS;
using VM.FS;

using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Avalonia.Interactivity;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Diagnostics;
using VM.Lang;
using Avalonia.Threading;
using System.Text.RegularExpressions;

namespace VM.Avalonia
{
    public partial class CommandPrompt : UserControl
    {
        private JavaScriptEngine? Engine;
        private List<string> commandHistory = new List<string>();
        private int historyIndex = -1; 
        private string tempInput = ""; 
        public Computer Computer;
        public static string? DesktopIcon => FileSystem.GetResourcePath("commandprompt.png");

        public Action<string> OnSend { get; internal set; }
        public static string? LastSentInput;
        string LastSentBuffer = "";
        private long io_handle_write, io_handle_clear, io_handle_read;

        public CommandPrompt()
        {
            InitializeComponent(true);
            
            KeyDown += CommandPrompt_PreviewKeyDown;
            
            input.Focus();

            output.TextChanged += Output_TextChanged;
            
            // subscribe to the computers IO stream
            // and setup our 'WriteLine' equivalent.
            // Note : as long as you don't 'RedirectOutput'
            // Console.WriteLine() will always print to the terminal,
            // as well as any of your sources.
            // as well as IO.ReadLine().

            // Initialize a dictionary to store line counts
            Dictionary<string, int> lineCounts = new Dictionary<string, int>();

            io_handle_write = IO.AddOutput((o) => {
            var str = o?.ToString();
            Dispatcher.UIThread.Invoke(() => {
                if (!string.IsNullOrEmpty(str)) {
                    string[] lines = output.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0) {
                        string lastLine = lines[lines.Length - 1];

                        // Check if the last line starts with a number followed by "x "
                        if (Regex.IsMatch(lastLine, @"^\d+x\s")) {
                            // Extract the count from the last line
                            Match match = Regex.Match(lastLine, @"(\d+)x");
                            if (match.Success) {
                                int count = int.Parse(match.Groups[1].Value);
                                count++;
                                str = $"{count}x {str}";
                                output.Text = output.Text.Substring(0, output.Text.LastIndexOf(lastLine)) + str;
                            }
                        } else {
                            // If the last line doesn't have a count, add "1x" to the new line
                            str = $"1x {str}";
                            output.Text += Environment.NewLine + str;
                        }
                    } else {
                        // If there are no lines in the output, add "1x" to the new line
                        str = $"1x {str}";
                        output.Text += str;
                    }
                }
            });
        });



        io_handle_clear = IO.AddClearHandler(()=>{
            output.Text = "";
            DrawTextBox("type 'help' for commands, \nor enter any valid single-line java script to interact with the environment. \nby default, results of expressions get printed to this console.");
        });
            
            IO.CSTREAM?.Invoke();
        }
        public void OnClosing() {

            IO.RemoveOutput(io_handle_write);
            IO.RemoveClearHandler(io_handle_clear);
        }

        private void Output_TextChanged(object? sender, EventArgs e)
        {
            // TODO: Fix scrolling behavior here
            //output.ScrollToLine(output.Text.Length);
        }

        public void DrawTextBox(string content)
        {
            List<string> contentLines = content.Split('\n').ToList();
            int maxContentWidth = GetMaxContentWidth(contentLines);
            int boxWidth = maxContentWidth + 6; // Account for box characters

            void DrawBoxTop()
            {
                output.Text += ("\n╔");
                for (int i = 0; i < boxWidth; ++i)
                {
                    output.Text += ("═");
                }
                output.Text += ("╗\n");
            }

            void DrawBoxBottom()
            {
                output.Text += ("╚");
                for (int i = 0; i < boxWidth; ++i)
                {
                    output.Text += ("═");
                }
                output.Text += ("╝");
            }

            DrawBoxTop();

            foreach (string line in contentLines)
            {
                output.Text += ("║" + PadCenter(line, boxWidth) + "║\n");
            }

            DrawBoxBottom();
        }

        private int GetMaxContentWidth(List<string> contentLines)
        {
            int maxWidth = 0;
            foreach (string line in contentLines)
            {
                maxWidth = Math.Max(maxWidth, line.Length);
            }
            return maxWidth;
        }

        private string PadCenter(string text, int width)
        {
            int padding = (width - text.Length) / 2;
            return text.PadLeft(padding + text.Length).PadRight(width);
        }

        public void LateInit(Computer computer)
        {
            this.Computer = computer;
            Engine = computer.JavaScriptEngine;

            output.FontFamily = new(computer.Config.Value<string>("FONT") ?? "Consolas");
            input.FontFamily = new(computer.Config.Value<string>("FONT") ?? "Consolas");
        }
        private async void CommandPrompt_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.F5)
            {
                await Send(e);
            }
            ManageCommandHistoryKeys(e);
        }

        private async Task Send(RoutedEventArgs? e)
        {
            OnSend?.Invoke(input.Text ?? "");

            var cmd_success = Computer.CommandLine.TryCommand(input.Text??"");

            if (cmd_success)
                return;
            // this goes before the execution so when it hangs up it doesnt 
            // enter a space

            if (e != null && e.RoutedEvent != null)
                e.Handled = true;

            const string ExecutingString = "\nExecuting...";

            output.Text += (ExecutingString);

            await ExecuteJavaScript(code : input.Text ?? "", timeout : Computer.Config.Value<int?>("CMD_LINE_TIMEOUT") ?? 50_000);

            output.Text += ("\nDone executing.");

            input.Clear();

            LastSentInput = LastSentBuffer;
            LastSentBuffer = output.Text;
        }

        private void ManageCommandHistoryKeys(KeyEventArgs e)
        {
            if (e.Key == Key.Up)
            {
                if (historyIndex == -1)
                {
                    tempInput = input.Text;
                }

                if (historyIndex < commandHistory.Count - 1)
                {
                    historyIndex++;
                    input.Text = commandHistory[commandHistory.Count - 1 - historyIndex];
                }
            }

            if (e.Key == Key.Down)
            {
                if (historyIndex >= 0)
                {
                    historyIndex--;
                    if (historyIndex == -1)
                    {
                        input.Text = tempInput;
                    }
                    else
                    {
                        input.Text = commandHistory[commandHistory.Count - 1 - historyIndex];
                    }
                }
            }

         
        }

        private async Task ExecuteJavaScript(string code, int timeout = int.MaxValue)
        {
            if (Computer.CommandLine.TryCommand(code))
            {
                return;
            }

            if (commandHistory.Count > 100)
            {
                commandHistory.RemoveAt(0);
            }

            commandHistory.Add(code);

            input.Clear();

            try
            {
                using var cts = new CancellationTokenSource();
                var executionTask = Engine?.Execute(code, cts.Token);

                var timeoutTask = Task.Delay(timeout);

                if (executionTask == null)
                    throw new NullReferenceException("No execution task was found, the engine probably was disposed while in use.");

                var completedTask = await Task.WhenAny(executionTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    cts.Cancel(); // Cancel the execution task
                    this.output.Text += ("\nExecution timed out.");
                }
                else
                {
                    var result = await executionTask;
                    string? output = result?.ToString();

                    if (!string.IsNullOrEmpty(output))
                    {
                        this.output.Text += ("\n" + output);
                    }
                }
            }
            catch (Exception ex)
            {
                this.output.Text += (ex.Message + Environment.NewLine);
            }
        }

        private void Grid_PointerPressed(object sender, RoutedEventArgs e)
        {
            input.Focus();
        }
    }
}
