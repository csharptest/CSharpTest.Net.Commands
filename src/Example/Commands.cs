using System;
using System.Collections.Generic;
using System.Text;
using CSharpTest.Net.Commands;

/// <summary>
/// Add the commands and options to this class, you can move the namespace as needed with
/// most refactoring tools.  The class is referenced from Main.cs
/// </summary>
partial class Commands
{
    [Option("name", DefaultValue = "Foo", Description = "Set or get my name.")]
    public string MyName { get; set; }

    [Command("hello", Description = "Say hello world.")]
    public void Hello(string from = null, int? repeated = null)
    {
        for (int i = 0; i < repeated.GetValueOrDefault(1); i++)
            Console.WriteLine("Hello world, from {0}", from ?? MyName);
    }
}
