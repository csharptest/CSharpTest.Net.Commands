CSharpTest.Net.Commands
=======================

CSharpTest.Net.Commands (moved from https://code.google.com/p/csharptest-net/)

## Changes ##

2014-03-09	Initial clone and extraction from existing library.

## Online Help ##

See the online help for CSharpTest.Net.Commands.CommandInterpreter
http://help.csharptest.net/?CSharpTest.Net.Library~CSharpTest.Net.Commands.CommandInterpreter_members.html

## Usage ##

The nuget package installs both a reference to the compiled assembly as well as a copy of the source code.  This allows users to either embed the source directly (stand-alone) and remove the reference, or to remove the source folder "Commands" and use the referenced library.

## Example ##

The following example program exposes a command-line that supports the "Example" command to print "Hello World" to std::out, and a Help command that describes the commands available.  See examples for more uses.

```
    class Program
    {
        public static void Example()
        {
            Console.WriteLine("Hello World");
        }

        [STAThread]
        static int Main(string[] args)
        {
            // Construct the CommandInterpreter and initialize
            ICommandInterpreter ci = new CommandInterpreter(
                DefaultCommands.Help,
                typeof(Program)
            );

            ci.Run(args);

            return ci.ErrorLevel;
        }
    }
```
