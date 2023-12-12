﻿using Lemur.FS;
using Lemur.GUI;
using Lemur.JavaScript.Network;
using Lemur.JS;
using Lemur.Types;
using Lemur.Windowing;
using Microsoft.Windows.Themes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Lemur.OS.Language
{
    public class JavaScriptPreProcessor
    {
        public static string MangleNames(string input)
        {
            var nameMap = new Dictionary<string, string>();
            string pattern = @"\b(class|function|var|let|const)\s+(\w+)";
            var counter = 0;
            return Regex.Replace(input, pattern, m =>
            {
                string key = m.Groups[2].Value;
                if (!nameMap.ContainsKey(key))
                    nameMap[key] = $"x{counter++}";
                return $"{m.Groups[1].Value} {nameMap[key]}";
            });
        }
        public static string InjectCommandLineArgs(string[] str_args, string jsCode)
        {
            const string ArgsArrayReplacement = "[/***/]";

            var index = jsCode.IndexOf(ArgsArrayReplacement);

            if (index != -1)
            {
                var args = jsCode.Substring(index, ArgsArrayReplacement.Length);

                var newArgs = $"[{string.Join("' ,'", str_args)}]";

                jsCode = jsCode.Replace(args, newArgs);
            }

            return jsCode;
        }
    }
    public record Command(string Identifier, CommandAction Action, params string[] Info); // oh well!

    public delegate void CommandAction(SafeList<object> args);

    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute(string identifier, params string[] Info) : Attribute
    {
        public readonly string[] Info = Info;
        public readonly string Identifier = identifier;
    }
    /// <summary>
    /// command parser &amp; execution engine for Lemur.
    /// </summary>
    public partial class CommandLine
    {
        public bool TryCommand(string termInput)
        {
            var inputs = termInput.Split(';');

            foreach (var input in inputs)
            {
                if (Commands.FirstOrDefault(c => c.Identifier == input) is Command _cmd && _cmd.Identifier != null && _cmd.Identifier != "NULL" && _cmd.Action != null)
                {
                    _cmd.Action.Invoke(new SafeList<object?>([]));
                    return true;
                }

                string[] split = input.Split(' ');

                if (split.Length == 0)
                    continue;

                string cmdName = split.First();
                var str_args = split[1..];

                if (Aliases.TryGetValue(cmdName, out var alias) && File.Exists(alias))
                {
                    var jsCode = File.ReadAllText(alias);

                    jsCode = JavaScriptPreProcessor.InjectCommandLineArgs(str_args, jsCode);

                    _ = Task.Run(async delegate { await Computer.Current.JavaScript.Execute(jsCode); });

                    return true;
                }

                return TryInvoke(cmdName, str_args);

            }

            return false; 
        }

       

        public bool TryInvoke(string name, params string[] args)
        {
            Command? cmd = Commands.FirstOrDefault(c => c.Identifier == name);
            cmd?.Action?.Invoke(args);
            return cmd?.Action != null;
        }
        public void Dispose()
        {
            Commands.Clear();
            Aliases.Clear();
        }
    }
    /// <summary>
    /// Set of base commands for Lemur. Feel free to expand / refine these.
    /// Check out SafeList to find out more.
    /// </summary>
    public partial class CommandLine : IDisposable
    {
        public List<Command> Commands = new();
        public Dictionary<string, string> Aliases = new();
        private bool Disposing;
        public CommandLine()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var bindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            var types = assembly.GetTypes();

            foreach (var type in types)
            {
                var methods = type.GetMethods(bindingFlags);

                foreach (var m in methods)
                {
                    var attr = m.GetCustomAttribute<CommandAttribute>(false);
                    if (attr != null)
                    {
                        var cmd = new Command(attr.Identifier, (CommandAction)Delegate.CreateDelegate(typeof(CommandAction), m), attr.Info);
                        Commands.Add(cmd);
                    }
                }
            }
        }
        [Command("move", "moves a file / changes its name")]
        private static void MoveFile(SafeList<object> obj)
        {
            if (obj[0] is not string a || obj[1] is not string b)
            {
                Notifications.Now("Bad arguments to : move");
                return;
            }

            FileSystem.Move(a, b);
            Notifications.Now($"Moved {a}->{b}");
        }
        [Command("mangler", "mangles a javascript file's names & identifiers, or a range of lines. usage : mangler <filename> <optional lineStart> <optional lineEnd>")]
        private static void Mangler(SafeList<object> obj)
        {
            if (obj.Length == 0)
            {
                Notifications.Now("mangler::error must provide a file for mangler");
                return;
            }

            if (obj[0] is string fileName)
            {
                fileName = FileSystem.GetResourcePath(fileName);
                if (!File.Exists (fileName))
                {
                    Notifications.Now("mangler::error : file did not exist");
                    return;
                } 
            } 
            else
            {
                Notifications.Now("mangler::error : invalid filename");
                return;
            }

            var data = File.ReadAllText(fileName);

            if (obj[1] is not string startIndex || !int.TryParse(startIndex, out var start))
            {
                File.WriteAllText(fileName, JavaScriptPreProcessor.MangleNames(data));
                return;
            }

            if (obj[2] is not string endIndex || !int.TryParse(endIndex, out var end))
            {
                Notifications.Now("invalid end index");
                return;
            }

            var lines = data.Split('\n')[start..end];

            var obfuscatedLines = JavaScriptPreProcessor.MangleNames(string.Join("\n", lines)).Split('\n');
            int line = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > start && i < end)
                    lines[i] = obfuscatedLines[line++];
            }

        }
        [Command("delete", "deletes a file / folder. use with caution!")]
        private static void DeleteFile(SafeList<object> obj)
        {
            if (obj[0] is string target)
            {
                FileSystem.Delete(target);
            }
            else
            {
                Notifications.Now("Invalid input parameters.");
            }
        }
        [Command("ls", "list's the current directory's contents, or list's the provided target directory's contents.")]
        private static void ListDir(SafeList<object> obj)
        {
            var terminal = Computer.TryGetProcessOfType<Terminal>();

            if (terminal == default)
            {
                Notifications.Now("You must have a cmd prompt open to display 'help' command results.");
                return;
            }

            var list = Computer.Current.FileSystem.DirectoryListing();

            var textList = string.Join("\n\t", list);

            var text = $"\n##Current Directory: {FileSystem.CurrentDirectory}###\n";
            terminal.output.AppendText(text + "\t");
            terminal.output.AppendText(textList);
        }
        [Command("cd", "changes the computer-wide current directory.")]
        private static void ChangeDir(SafeList<object> obj)
        {
            if (obj[0] is string Path)
            {
                FileSystem.ChangeDirectory(Path);
            }
            else
            {
                Notifications.Now("failed cd: bad arguments. usage : cd /path/to/wherever.. or cd .. (to go up one)");
            }
        }
        [Command("copy", "sets the command prompts font for this session. call this from a startup to set as default")]
        private static void CopyFile(SafeList<object> obj)
        {
            if (obj[0] is string path)
            {
                for (int i = 1; i < obj.Length; i++)
                {
                    if (obj[i] is not string destination)
                    {
                        Notifications.Now("failed copy: bad arguments. usage : copy source dest.. (one or many destinations)");
                        continue;
                    }

                    if (destination is null || string.IsNullOrEmpty(destination))
                    {
                        Notifications.Now($"Invalid path {destination} in Copy");
                        continue;
                    }
                    FileSystem.Copy(path, destination);
                    Notifications.Now($"Copied from {path}->{destination}");
                }
            }
            else
            {
                Notifications.Now("failed cpy: bad arguments. usage : copy source dest.. (one or many destinations)");
            }
        }
        [Command("mkdir", "creates a directory at the given path")]
        private static void MakeDir(SafeList<object> obj)
        {
            if (obj[0] is string Path)
            {
                FileSystem.NewDirectory(Path);
                Notifications.Now($"Created directory {Path}");
            }
            else
            {
                Notifications.Now("failed mkdir: bad arguments. usage : 'mkdir directory.'");
            }
        }
        [Command("root", "navigates the open file explorer to the root directory of the computer.")]
        public static void RootCmd(SafeList<object> obj)
        {
            FileSystem.ChangeDirectory(FileSystem.Root);
        }
        [Command("unhost", "if a server is currently running on this machine this halts any active connections and closes the sever.")]
        private static void StopHosting(SafeList<object> obj)
        {
            Computer.Current.NetworkConfiguration.StopHosting();
        }
        [Command("--kill-all", "kills all the running processes on the computer, specify an app name like terminal to kill only those app instances, if any.")]
        private static void KillAllProcesses(SafeList<object> obj)
        {
            List<string> toKill = [];

            if (obj != null && obj.Length == 1)
            {
                if (obj[0] is string name && Computer.ProcessClassTable.TryGetValue(name, out var procClass))
                    toKill.AddRange(procClass.Select(i => i.ID));
                else
                {
                    Notifications.Now($"No process with name {obj[0]} found.");
                    return;
                }
            }
            else
            {
                foreach (var procClass in Computer.ProcessClassTable.Values)
                    toKill.AddRange(procClass.Select(i => i.ID));
            }

            foreach (var pid in toKill)
            {
                var proc = Computer.GetProcess(pid);
                proc?.Terminate();
            }

        }
      
        [Command("host", "hosts a server on the provided <port>, none provided it will default to 8080")]
        private static void StartHosting(SafeList<object> obj)
        {
            Task.Run(async () =>
            {
                int? port = obj?[0] as int?;
                if (await Computer.Current.NetworkConfiguration.StartHosting(port ?? NetworkConfiguration.defaultPort))
                {
                    Notifications.Now($"Hosting on {Computer.Current.NetworkConfiguration.GetIPPortString()}");
                    return;
                }
                Notifications.Now($"Failed to begin hosting on {LANIPFetcher.GetLocalIPAddress().MapToIPv4()}:{port}");
            });
        }
        [Command("lp", "lists all the running processes")]
        private static void ListProcesses(SafeList<object> obj)
        {
            foreach (var item in Computer.ProcessClassTable)
            {
                Notifications.Now($"Process: {item.Key} \n\t PIDs: {string.Join(",", item.Value.Select(i => i.ID))}");
            }
        }
        [Command("ip", "fetches the local ip address of your internet connection")]
        private static void GetIPAddress(SafeList<object> obj)
        {
            var IP = LANIPFetcher.GetLocalIPAddress().MapToIPv4();
            Notifications.Now(IP.ToString());
        }
        [Command("edit", "reads / creates a .js file at provided path, and opens it in the text editor")]
        private static void EditTextFile(SafeList<object> obj)
        {
            if (obj[0] is string fileName)
            {

                if (FileSystem.GetResourcePath(fileName) is string AbsPath && !string.IsNullOrEmpty(AbsPath))
                {
                    if (!File.Exists(AbsPath))
                    {
                        var str = File.Create(AbsPath);
                        str.Close();
                    }
                    var wnd = Computer.Current.Window;
                    var tEdit = new Texed(AbsPath);
                    Computer.Current.OpenApp(tEdit, "texed.app", Computer.GetNextProcessID());
                }
            }
            else
            {
                Notifications.Now("Invalid input parameters.");
            }
        }
        [Command("config", "config <all|set|get|rm> <prop_name?> <value?>")]
        private static void ModifyConfig(SafeList<object> obj)
        {
            // I am not sure if this is even possible.
            Computer.Current.Config ??= [];

            if (obj is null)
                return;

            if (obj.Length == 0)
            {
                Notifications.Now("Invalid input parameters.");
                return;
            }

            if (obj[0] is string call)
            {
                switch (call)
                {
                    case "save":
                        Computer.SaveConfig(Computer.Current.Config.ToString());
                        return;
                    case "load":
                        Computer.LoadConfig();
                        return;
                    case "all":
                        var terminal = Computer.TryGetProcessOfType<Terminal>();

                        if (terminal == default)
                        {
                            Notifications.Now("You must have a cmd prompt open to display 'help' command results.");
                            return;
                        }

                        StringBuilder outputSb = new();

                        foreach (var kvp in Computer.Current.Config)
                            outputSb.Append($"\n {{{kvp.Key} : {kvp.Value}}}");

                        terminal.output.AppendText(outputSb.ToString());
                        break;
                }
                
                if (obj.Length < 2) 
                    return;

                var propname = (obj[1] as string);

                if (propname is null)
                {
                    Notifications.Now("Invalid input parameters.");
                    return;
                }

                propname = propname.ToUpper(CultureInfo.CurrentCulture);

                switch (call) 
                {
                    case "rm":

                        if (!Computer.Current.Config.TryGetValue(propname, out var propValue))
                        {
                            Notifications.Now($"Property '{propname}' not found in configuration.");
                            return;
                        }

                        Computer.Current.Config.Remove(propname);

                        break;


                    case "get":
                        var terminal = Computer.TryGetProcessOfType<Terminal>();

                        if (terminal == default)
                        {
                            Notifications.Now("You must have a cmd prompt open to display 'help' command results.");
                            return;
                        }

                        if (!Computer.Current.Config.TryGetValue(propname, out propValue))
                        {
                            Notifications.Now($"Property '{propname}' not found in configuration.");
                            return;
                        }

                        terminal.output.AppendText($"\n {{{propname} : {propValue}}}");
                        break;

                    case "set":
                        string arg = "";

                        arg += string.Join(" ", obj[2..]);

                        arg = arg.Trim();

                        if (arg.First() == '[' && arg.Last() == ']')
                        {
                            var clean = arg.Replace('[', ' ');
                            clean = clean.Replace(']', ' ');

                            var contents = clean.Split(',');

                            List<object> objectArgs = new();

                            foreach (var item in contents)
                            {
                                if (float.TryParse(item, out var floatingpt))
                                    objectArgs.Add(floatingpt);
                                else if (int.TryParse(item, out var integer))
                                    objectArgs.Add(integer);
                                else if (bool.TryParse(item, out var boolean))
                                    objectArgs.Add(boolean);
                            }

                            var jObject = JObject.FromObject(objectArgs);

                            Computer.Current.Config[propname] = jObject;
                        }

                        Computer.Current.Config[propname] = arg;
                        break;

                   
                }


            }


          

        }
        [Command("clear", "clears the terminal(s), if open.")]
        private static void ClearTerminal(SafeList<object> obj)
        {
            var cmd = Computer.TryGetProcessOfType<Terminal>()?.output;
            Computer.Current.Window.Dispatcher.Invoke(() => { cmd?.Clear(); });

            if (cmd is null)
                Notifications.Now("failed to clear - no cmd prompt open");
        }
        [Command("help", "prints these help listings")]
        private static void ShowHelp(SafeList<object> obj)
        {
            var terminal = Computer.TryGetProcessOfType<Terminal>();

            StringBuilder cmdbuilder = new();
            StringBuilder aliasbuilder = new();

            // todo: make this easier to read, add a manual.
            foreach (var item in Computer.Current.CmdLine.Commands)
                cmdbuilder?.Append($"\n{{{item.Identifier}}} \t\n\'{string.Join(",", item.Info)}\'");

            // todo: add better alias info
            foreach (var item in Computer.Current.CmdLine.Aliases)
                aliasbuilder.Append($"\n{item.Key} -> {item.Value.Split('\\').Last()}");


            terminal.Dispatcher.Invoke(() =>
            {
                terminal?.output.AppendText(" ### Native Commands ### ");
                terminal?.output.AppendText(cmdbuilder.ToString());

                terminal?.output.AppendText(" ### Command Aliases ### ");
                terminal?.output.AppendText(aliasbuilder.ToString());
            });


        }
        [Command("run", "runs a JavaScript file of specified path in the computers main engine.")]
        private static async void RunJsFile(SafeList<object> obj)
        {
            if (obj[0] is string path && FileSystem.GetResourcePath(path.Replace(".js", "") + ".js") is string AbsPath && File.Exists(AbsPath))
            {
                await Computer.Current.JavaScript.Execute(File.ReadAllText(AbsPath));
                Notifications.Now($"running {AbsPath}...");
            }
            else
            {
                Notifications.Now("failed run: bad args. usage : run 'path.js'");
            }
        }
       
       
     }
}

