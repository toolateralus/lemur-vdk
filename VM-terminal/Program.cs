﻿using System.Net.WebSockets;
using VM;
using VM.FS;



while(true)
{

    IO.Out("Enter a computer index, (probably just 0, unless you're multi-booting)");

    if(!uint.TryParse(Console.ReadLine()?.Trim(), out uint id))
    {
        IO.Out("Invalid ID. it must be an integer ie 1 , or 2, not 0xF or 1.1. press any key to continue");
        Console.ReadLine();
        Console.Clear();
        continue;
    }

    var computer = new Computer();

    computer.Boot(id);

    IO.Out($"Computer booted on id {id}");
    IO.Out("Welcome to the command line. type help for info.");
    

    while (true)
    {
        var input = Console.ReadLine()!;
        
        if (input.Trim().ToLower() == "restart"){
            break;
        }

        if (!computer.CommandLine.TryCommand(input)){
            await computer.JavaScriptEngine.Execute(input);
        }
    }

    
}