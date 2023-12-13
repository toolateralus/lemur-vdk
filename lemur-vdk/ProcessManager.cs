﻿using Lemur.GUI;
using Lemur.JavaScript.Api;
using Lemur.JS;
using Lemur.JS.Embedded;
using Lemur.Windowing;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Lemur
{
    public class ProcessManager
    {
        internal Dictionary<string, List<Process>> ProcessClassTable = [];
        internal string GetProcessClass(string identifier)
        {
            var processClass = "Unknown process";

            foreach (var procList in ProcessClassTable)
                foreach (var proc in from proc in procList.Value
                                     where proc.ID == identifier
                                     select proc)
                {
                    processClass = proc.Type;
                }

            return processClass;
        }
        internal IReadOnlyCollection<T> TryGetAllProcessesOfType<T>() where T : UserControl
        {
            List<T> contents = [];
            foreach (var process in ProcessClassTable.Values.SelectMany(i => i.Select(i => i))) // flatten array
            {
                process.UI.Dispatcher.Invoke(() =>
                {
                    if (process.UI.ContentsFrame is not Frame frame)
                        return;

                    if (frame.Content is not T instance)
                        return;

                    contents.Add(instance);
                });
            }
            return contents;
        }
        internal T? TryGetProcessOfType<T>() where T : UserControl
        {
            T? content = default(T);

            Computer.Current.Window.Dispatcher.Invoke(() =>
            {
                content = TryGetProcessOfTypeUnsafe<T>();
            });

            return content;
        }
        internal async Task CreateEventHandler(Engine engine, string identifier, string targetControl, string methodName, int type)
        {
            var process = GetProcess(identifier);
            var wnd = process.UI;

            // check if this event already exists
            var result = await engine.Execute($"{identifier} != null").ConfigureAwait(true);

            if (result is not bool ID_EXISTS || !ID_EXISTS)
            {
                Notifications.Now($"App not found : {identifier}..  that is NOT good...");
                return;
            }

            // check if this method already exists
            result = await engine.Execute($"{identifier}.{methodName} != null").ConfigureAwait(true);

            string processClass = process.Type.Replace(".app", "", StringComparison.CurrentCulture);

            if (result is not bool METHOD_EXISTS || !METHOD_EXISTS)
            {
                Notifications.Now($"'app.eventHandler(...)' threw an exception : {processClass}.{methodName} not found. Make sure {methodName} is defined and spelled correctly in both the hook function call and the definition.");
                return;
            }
            InteropEvent? eh = default;

            wnd.Dispatcher.Invoke(() =>
            {
                var content = process?.UI?.Engine?.AppModule?.GetUserContent();

                if (content == null)
                {
                    Notifications.Now($"control {identifier} not found!");
                    return;
                }

                FrameworkElement? element = null;

                if (targetControl.ToLower(CultureInfo.CurrentCulture).Trim() == "this")
                    element = content;
                else
                    element = app_t.FindControl(content, targetControl)!;


                if (element == null)
                {
                    Notifications.Now($"control {targetControl} of {content.Name} not found.");
                    return;
                }

                // this hooks up the event & finalizes setup.
                eh = new InteropEvent(element, (Event)type, engine, identifier, methodName);

            });

            if (GetProcess(identifier) is not Process p)
            {
                Notifications.Now("Creating an event handler failed : this is an engine bug. report it on GitHub if you'd like");
                return;
            }

            var disposed = false;

            /// this was an attempt to force close the app on too many errors
            /// stack overflow, trying to finally decouple UI.
            /// 
            //eh.OnEventDisposed += () => {
            //    if (disposed)
            //        return; 

            //    App.Current.Dispatcher.Invoke(app.Close);
            //    disposed = true;
            //};

            p.OnProcessTermination += () =>
            {
                if (disposed)
                    return;

                if (engine.EventHandlers.Contains(eh))
                    engine.EventHandlers.Remove(eh);

                eh?.ForceDispose();
                disposed = true;
            };

            engine.EventHandlers.Add(eh);
        }
        internal T? TryGetProcessOfTypeUnsafe<T>() where T : UserControl
        {
            T? matchingWindow = default(T);

            foreach (var pclass in ProcessClassTable)
                foreach (var proc in pclass.Value)
                    if (proc.UI.ContentsFrame is Frame frame && frame.Content is T instance)
                        matchingWindow = instance;

            return matchingWindow;
        }
        internal string GetNextProcessID()
        {
            return $"p{Computer.__procId++}";
        }
        internal Process? GetProcess(string pid)
        {
            foreach (var pclass in ProcessClassTable)
                if (pclass.Value.FirstOrDefault(p => p.ID == pid) is Process proc)
                    return proc;
            return null;
        }
        internal void TerminateProcess(string pID)
        {
            if (GetProcess(pID) is Process p)
                p.Terminate();
            else Notifications.Now($"Could not find process {pID}");
        }
        internal List<T> TryGetAllProcessesOfTypeUnsafe<T>()
        {
            List<T> contents = [];
            foreach (var process in ProcessClassTable.Values.SelectMany(i => i.Select(i => i))) // flatten array
            {
                if (process.UI.ContentsFrame is not Frame frame)
                    continue;

                if (frame.Content is not T instance)
                    continue;

                contents.Add(instance);
            }
            return contents;
        }
        internal static async Task<string> CreateJavaScriptBackend(string type, string processID, object[] cmdLineArgs, (string XAML, string JS) data, Engine engine)
        {
            var name = type.Split('.')[0];

            var JS = new string(data.JS);

            _ = await engine.Execute(JS).ConfigureAwait(true);

            string instantiation_code;


            if (cmdLineArgs.Length != 0)
            {
                var args = string.Join(", ", cmdLineArgs);
                instantiation_code = $"const {processID} = new {name}('{processID}, {args}')";
            }
            else
                instantiation_code = $"const {processID} = new {name}('{processID}')";



            return instantiation_code;
        }
        internal void GetProcessesOfType(string name, out List<Process> processes)
        {
            if (!ProcessClassTable.TryGetValue(name, out processes!))
                processes = [];
        }
        internal void RegisterNewProcess(Process process, out List<Process> procList)
        {
            GetProcessesOfType(process.Type, out procList);

            procList.Add(process);

            ProcessClassTable[process.Type] = procList;
        }
    }
}