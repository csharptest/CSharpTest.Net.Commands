#region Copyright 2009-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections.Generic;
using NUnit.Framework;
using CSharpTest.Net.Commands;
using System.IO;
using System.ComponentModel;
using System.Runtime.Serialization.Formatters.Binary;
using System.Diagnostics;

#pragma warning disable 1591

namespace CSharpTest.Net.CommandsTest
{
	[TestFixture]
	public partial class TestCmdInterpreter
	{
		delegate void Action();
		#region TestFixture SetUp/TearDown
		[TestFixtureSetUp]
		public virtual void Setup()
		{
		}

		[TestFixtureTearDown]
		public virtual void Teardown()
		{
			Environment.ExitCode = 0;
		}
		#endregion

        [DebuggerNonUserCode]
	    private static int WindowHeight
	    {
	        get
	        {
	            int windowHeight;
	            try
	            {
	                windowHeight = Console.WindowHeight;
	            }
	            catch (System.IO.IOException)
	            {
	                windowHeight = 25;
	            }
	            return windowHeight;
	        }
	    }

	    /// <summary> Used to provide a set of test commands </summary>
		class TestCommands
		{
			string _data;
			[Option("SomeData", "SD", DefaultValue = "", Description = "Stores some data.")]
			public string SomeData
			{
				get { return _data; }
				set { _data = value; }
			}

            [Option("Other"), IgnoreMember]
            public string ThisIsIgnored { get { throw new NotImplementedException(); } set { } }

            [Command("Hidden"), IgnoreMember]
            public void ThisIsAlsoIgnored() { }

			int _otherdata = 0;
			[Option("Other")]
			[AliasName("alias")]
			//OptionAttribute takes precedence, but the following also works.
			[System.ComponentModel.DisplayName("ingored-due-to-OptionAttribute")]
			[System.ComponentModel.Description("description")]
			[System.ComponentModel.Category("category")]
			[System.ComponentModel.Browsable(false)]
			[System.ComponentModel.DefaultValue(-1)]
			public int OtherData
			{
				get { return _otherdata; }
				set { _otherdata = value; }
			}

			public string ReadOnlyDoesntAppear { get { return _data; } }
			public string WriteOnlyDoesntAppear { set { _data = value; } }

			[Command("Hidden", Visible = false)]
			[AliasName("myhiddencommand")]
			[AliasName("")] // <= ignored if null or empty
			public void Hidden(ICommandInterpreter ci, [AllArguments] string[] args)
			{
				Console.WriteLine("Hidden Runs: {0}", String.Join(" ", args));
				ci.Run(args);
			}

			[Command( 
				DisplayName="Count",
				AliasNames = new string[0],
				Category="",
				Description = "Count Description.",
				Visible = true
				)]
			public void Count(
				[Argument("number", "n", Description = "The number to count to or from.")]
				int number,
				[Argument("backwards", DefaultValue=false, Description="Count backwards")]
				bool backwards,
				// Arguments that are of type string[] can be specified more than once on a command-line
				// for example /t:x /t:y /t:z will result in the array string[3] { "x", "y", "z" }
				[Argument("text", "t", DefaultValue = new string[0], Description = "A piece of text to append")]
				string[] text,
				// any method can recieve an ICommandInterpreter, once encountered in args all remaining
				// must be qualified with /name= format.
				ICommandInterpreter ci,
				// any method can also take the complete argument list, however, it should always appear
				// after all other arguments since.  It must always be decorated with [AllArugments]. Any
				// command with this parameter will not complain about unknown arguments.
				[AllArguments] string[] allargs
				)
			{
				int st, end, offset;
				if (!backwards)
				{ st = 1; end = number; offset = 1; }
				else
				{ st = number; end = 1; offset = -1; }
	
				for (int i = st; true; i += offset)
				{
					if( text.Length == 0 )
						Console.WriteLine("{0}", i);
					else
						Console.WriteLine("{0} {1}", i, text[(i-1) % text.Length]);

					if (i == end)
						break;
				}
			}

			public void BlowUp([DefaultValue(false)] bool apperror)
			{
				if( apperror )
					throw new ApplicationException("BlowUp");
				throw new Exception("BlowUp");
			}

			//undecorated should work just fine
			public void ForXtoYbyZ(int start, int end, int increment)
			{
				for (int i = start; true; i += increment)
				{
					Console.WriteLine("{0}", i);
					if (i == end)
						break;
				}
			}

	        public void NullableDefaultArgs(long lval, int? ival = 0, TraceLevel? enumVal = null, [Argument(Visible = false)]string sval = "SomeText")
	        {
	        }
		}

		class StaticTestFilter
		{
			static int lineNo = 0;
			// implements a filter than runs for all commands...
			[CommandFilter] // <= implied by method signature, exact signature required for all filters
			public static void AddLineNumbers(ICommandInterpreter ci, ICommandChain chain, string[] args)
			{
				string line;

				if (chain == null) 
				{
					// not possible UNLESS you add this filter to the list of commands which is not recommended
					// since it would generally be easier to just add another method to handle this if/else branch
					// for you.  However, since it is technically possible to do so this will be tested.
					Console.WriteLine("{0}", lineNo);
				}
				else
				{
					bool addLineNumbers = ArgumentList.Remove(ref args, "linenumbers", out line);

					TextWriter stdout = Console.Out;
					StringWriter swout = new StringWriter();

					if( addLineNumbers )
						Console.SetOut(swout);

					chain.Next(args); // <= Unless we want to prevent this command from executing, we must call next()
		
					if(addLineNumbers)
					{
						StringReader r = new StringReader(swout.ToString());

						while (null != (line = r.ReadLine()))
							stdout.WriteLine("{0}: {1}", ++lineNo, line);
					}
				}
			}
		}

		private static string Capture(CommandInterpreter ci, string input)
		{
			TextWriter stdout = Console.Out, stderr = Console.Error;
			TextReader stdin = Console.In;
			try
			{
				StringWriter sw = new StringWriter();
                Console.SetOut(sw);
                StringWriter swe = new StringWriter();
                Console.SetError(swe);
				Console.SetIn(new StringReader(input));

				ci.Prompt = String.Empty;
				ci.Run(Console.In);
			    sw.WriteLine(swe.ToString());
				return sw.ToString().Trim();
			}
			finally
			{
				Console.SetOut(stdout);
				Console.SetError(stderr);
				Console.SetIn(stdin);
			}
		}

		[Test]
		public void TestAddCommands()
		{
			CommandInterpreter ci = new CommandInterpreter(DefaultCommands.None, new TestCommands());
			Assert.AreEqual(2, ci.Options.Length);
			Assert.AreEqual("Other", ci.Options[0].DisplayName);
			Assert.AreEqual("SomeData", ci.Options[1].DisplayName);

			Assert.AreEqual(4, ci.Commands.Length);
			Assert.AreEqual("BlowUp", ci.Commands[0].DisplayName);
			Assert.AreEqual("Count", ci.Commands[1].DisplayName); // <= alpha-sorted
			Assert.AreEqual("ForXtoYbyZ", ci.Commands[2].DisplayName);
			Assert.AreEqual("Hidden", ci.Commands[3].DisplayName);

			foreach (ICommand c in ci.Commands)
				ci.RemoveCommand(c);
			Assert.AreEqual(0, ci.Commands.Length);

			ci = new CommandInterpreter(DefaultCommands.None);
			Assert.AreEqual(0, ci.Options.Length);
			Assert.AreEqual(0, ci.Commands.Length);

			ci.AddHandler(typeof(StaticTestFilter));
			Assert.AreEqual(0, ci.Options.Length); // the type StaticTestFilter contains filters and no commands/options
			Assert.AreEqual(0, ci.Commands.Length);

			ci.AddHandler(new TestCommands());
			Assert.AreEqual(2, ci.Options.Length);
			Assert.AreEqual(4, ci.Commands.Length);
        }

        [Test]
        public void TestHtmlHelp()
        {
            CommandInterpreter ci = new CommandInterpreter(DefaultCommands.Help, new TestCommands());
            string helptext = ci.GetHtmlHelp(null);
            Assert.IsTrue(0 == helptext.IndexOf("<html>"));
            Assert.IsTrue(helptext.Contains("COMMAND"));
            Assert.IsTrue(helptext.IndexOf("SOMEDATA",StringComparison.OrdinalIgnoreCase) >= 0);
        }

		[Test]
		public void TestHelpText()
		{
			CommandInterpreter ci = new CommandInterpreter(DefaultCommands.None, new TestCommands());
			string helptext = Capture(ci, "Help");
			Assert.AreNotEqual(0, Environment.ExitCode);
			Assert.IsTrue(helptext.Contains("Invalid"));

			Environment.ExitCode = 0;
			ci = new CommandInterpreter(DefaultCommands.Help, new TestCommands());
			helptext = Capture(ci, "Help");
			Assert.AreEqual(0, Environment.ExitCode);
			Assert.IsFalse(helptext.Contains("Invalid"));
			Assert.IsFalse(helptext.Contains("HIDDEN")); // <= not listed
			Assert.IsTrue(helptext.Contains("COUNT"));
			Assert.IsTrue(helptext.Contains("SOMEDATA"));
			Assert.IsTrue(helptext.Contains("FORXTOYBYZ"));

			//empty command-string displays help, the EXIT/QUIT are always available when running
			//interactive mode via .Run(TextReader), which is what Capture(...) does.
			Assert.AreEqual(helptext, Capture(ci, Environment.NewLine + "EXIT"));

			helptext = Capture(ci, "Help hidden"); // <= still has detailed help
			Assert.AreEqual(0, Environment.ExitCode);
			Assert.IsTrue(helptext.Contains("HIDDEN"));
			Assert.IsTrue(helptext.Contains("MYHIDDENCOMMAND")); // <= alias names display for details
			Assert.IsFalse(helptext.Contains("COUNT"));
			Assert.IsFalse(helptext.Contains("SOMEDATA"));
			Assert.IsFalse(helptext.Contains("FORXTOYBYZ"));

			helptext = Capture(ci, "Help Help");
			Assert.AreEqual(0, Environment.ExitCode);
			Assert.IsTrue(helptext.Contains("HELP"));
			Assert.IsTrue(helptext.Contains("[/name=]String"));

			helptext = Capture(ci, "Help SOMEDATA");
			Assert.AreEqual(0, Environment.ExitCode);
			Assert.IsTrue(helptext.Contains("SOMEDATA"));
			Assert.IsTrue(helptext.Contains("SD"));
		}
        [Test]
        public void TestSetPersistOption()
        {
            Assert.AreEqual(DefaultCommands.Get | DefaultCommands.Set | DefaultCommands.Help, DefaultCommands.Default);
            TestCommands cmds = new TestCommands();
            CommandInterpreter ci = new CommandInterpreter(cmds);
            cmds.OtherData = 42;
            Assert.AreEqual("42", Capture(ci, "GET Other"));
            cmds.SomeData = "one-two-three";
            Assert.AreEqual("one-two-three", Capture(ci, "GET SomeData"));

            string options = Capture(ci, "SET");
            cmds.OtherData = 0;
            cmds.SomeData = String.Empty;
            Assert.AreEqual("0", Capture(ci, "GET Other"));
            Assert.AreEqual(String.Empty, Capture(ci, "GET SomeData"));

            TextReader input = Console.In;
            try
            {
                Console.SetIn(new StringReader(options));//feed the output of SET back to SET
                ci.Run("SET", "/readInput");
            }
            finally { Console.SetIn(input); }

            //should now be restored
            Assert.AreEqual("42", Capture(ci, "GET Other"));
            Assert.AreEqual("one-two-three", Capture(ci, "GET SomeData"));
        }

		[Test]
		public void TestGetSetOption()
		{
			Assert.AreEqual(DefaultCommands.Get | DefaultCommands.Set | DefaultCommands.Help, DefaultCommands.Default);
			//defaults the DefaultCommands to Get/Set/Help via the enum value of DefaultCommands.Default
			CommandInterpreter ci = new CommandInterpreter(new TestCommands());

			Assert.AreEqual(2, ci.Options.Length);
			Assert.AreEqual("Other", ci.Options[0].DisplayName);
			Assert.AreEqual(typeof(int), ci.Options[0].Type);
			Assert.AreEqual("SomeData", ci.Options[1].DisplayName);
			Assert.AreEqual(typeof(String), ci.Options[1].Type);

			TextWriter stdout = Console.Out;
			try
			{
				Console.SetOut(new StringWriter());// <= Get will also write to console

				Assert.AreEqual(-1, ci.Get("other"));//default was applied
				ci.Set("other", 1);
				Assert.AreEqual(1, ci.Get("other"));

				Assert.AreNotEqual("abc", ci.Get("somedata"));
				ci.Set("somedata", "abc");
				Assert.AreEqual("abc", ci.Get("somedata"));
			}
			finally
			{ Console.SetOut(stdout); }

			string result;
			result = Capture(ci, "GET Somedata");
			Assert.AreEqual("abc", result);
			//Set without args lists options
			result = Capture(ci, "SET");
			Assert.IsTrue(result.ToUpper().Contains("somedata".ToUpper()));
			//Set without value returns the current value
			result = Capture(ci, "SET somedata");
			Assert.AreEqual("abc", result);
			result = Capture(ci, "SET somedata 123");
			Assert.AreEqual("", result);
			result = Capture(ci, "GET somedata");
			Assert.AreEqual("123", result);
		}

		[Test]
		public void TestCommandRun()
		{
			string result;
			CommandInterpreter ci = new CommandInterpreter(DefaultCommands.Get | DefaultCommands.Set, new TestCommands());

			result = Capture(ci, "Count 2");
			Assert.AreEqual("1\r\n2", result);

			result = Capture(ci, "Count /backwards 2");
			Assert.AreEqual("2\r\n1", result);
			result = Capture(ci, "Count 2 /backwards");
			Assert.AreEqual("2\r\n1", result);
			result = Capture(ci, "Count -n:2 /backwards:true");
			Assert.AreEqual("2\r\n1", result);

			result = Capture(ci, "Count 2 /t:a /t:b");
			Assert.AreEqual("1 a\r\n2 b", result);

			//Argument not found:
			result = Capture(ci, "Count");
			Assert.AreEqual("The value for number is required.", result);

//#warning Broken?
			//Non-ApplicationExcpetion dumps stack:
			ci.ErrorLevel = 0;
			result = Capture(ci, "BlowUp false");
			Assert.AreNotEqual(0, ci.ErrorLevel);
			Assert.IsTrue(result.Contains("System.Exception: BlowUp"), "Expected \"System.Exception: BlowUp\" in {0}", result);

			//ApplicationExcpetion dumps message only:
			ci.ErrorLevel = 0;
			result = Capture(ci, "BlowUp true");
			Assert.AreNotEqual(0, ci.ErrorLevel);
			Assert.AreEqual("BlowUp", result);
		}

		[Test]
		public void TestMacroExpand()
		{
			string result;
			CommandInterpreter ci = new CommandInterpreter(DefaultCommands.Echo | DefaultCommands.Prompt, new TestCommands());

			ci.Set("SomeData", "TEST_Data");
			result = Capture(ci, "ECHO $(SOMEDATA)");
			Assert.AreEqual("TEST_Data", result);

			ci.Set("SomeData", "TEST Data");
			result = Capture(ci, "ECHO $(SOMEDATA)");
			Assert.AreEqual("\"TEST Data\"", result); // <= Echo will quote & escape while-space and quotes "

			result = Capture(ci, "ECHO $(MissingProperty)");
			Assert.AreEqual("Unknown option specified: MissingProperty", result);

			result = Capture(ci, "ECHO $$(MissingProperty) $$(xx x$$y $$ abc"); // <= escape '$' with '$$'
			Assert.AreEqual("$(MissingProperty) $(xx x$y $ abc", result); // <= extra '$' was removed.
		}

		class ErrorReader : TextReader
		{
			public override string ReadLine()
			{ throw new NotImplementedException(); }
		}

		[Test]
		public void TestLoopErrors()
		{
			TextWriter stdout = Console.Out, stderr = Console.Error;
			try
			{
				StringWriter sw = new StringWriter();
				Console.SetError(sw);
				Console.SetOut(sw);

				string result;
				CommandInterpreter ci = new CommandInterpreter(new TestCommands());

				ci.Prompt = "$(MissingProperty)";
				ci.Run(new StringReader("EXIT"));
				result = sw.ToString();
				Assert.IsTrue(result.StartsWith("Unknown option specified: MissingProperty"));
				ci.Prompt = String.Empty;

				sw.GetStringBuilder().Length = 0;//clear
				ci.Run(new ErrorReader());

				result = sw.ToString();
				Assert.IsTrue(result.StartsWith(typeof(NotImplementedException).FullName));
			}
			finally
			{
				Console.SetOut(stdout);
				Console.SetError(stderr);
			}
		}

		[Test]
		public void TestCommandFilters()
		{
			string result;
			CommandInterpreter ci = new CommandInterpreter(DefaultCommands.None, new TestCommands(), typeof(StaticTestFilter));
			Assert.AreEqual(1, ci.Filters.Length);
			Assert.AreEqual("AddLineNumbers", ci.Filters[0].DisplayName);

			result = Capture(ci, "Count 2 /linenumbers");
			Assert.AreEqual("1: 1\r\n2: 2", result);

			int cmds = ci.Commands.Length;
			ci.AddCommand(ci.Filters[0]);
			Assert.AreEqual(cmds + 1, ci.Commands.Length);

			result = Capture(ci, "AddLineNumbers");
			Assert.AreEqual("2", result);
		}

		private Char GetSpace() { return ' '; }

		[Test]
		public void TestBuiltInMore()
		{
			string result;
			CommandInterpreter ci = new CommandInterpreter(
				DefaultCommands.More | DefaultCommands.PipeCommands,
				new TestCommands());

			//replace the keystroke wait
			ci.ReadNextCharacter = GetSpace;

            string input = String.Format("Count {0} | MORE", (int)(WindowHeight * 1.5));

			result = Capture(ci, input);

			StringReader sr = new StringReader(result);
			int moreFound = 0;
			int index = 0;
			string line;
			while( null != (line = sr.ReadLine()))
			{
				if( line == "-- More --" )
					moreFound++;
				else
					Assert.AreEqual((++index).ToString(), line);
			}

			Assert.AreEqual(1, moreFound);
		}

		[Test]
		public void TestBuiltInFind()
		{
			string result;
			CommandInterpreter ci = new CommandInterpreter(
				DefaultCommands.Echo | DefaultCommands.Find | DefaultCommands.PipeCommands,
				new TestCommands());

		    result = Capture(ci, "Count 220 |FIND \"1\" |FIND \"0\" | FIND /V \"3\" | FIND /V \"4\" | FIND /V \"5\" | FIND /V \"6\" | FIND /V \"7\" | FIND /V \"8\" | FIND /V \"9\"");
			Assert.AreEqual("10\r\n100\r\n101\r\n102\r\n110\r\n120\r\n201\r\n210", result);

			result = Capture(ci, "ECHO ABC | FIND \"abc\" |");
			Assert.AreEqual(String.Empty, result);

			result = Capture(ci, "ECHO ABC | FIND /I \"abc\" |");
			Assert.AreEqual("ABC", result);
		}

		[Test]
		public void TestBuiltInRedirect()
		{
			string tempPath = Path.GetTempFileName();
			string tempPath2 = Path.GetTempFileName();
			try
			{
				string result;
				CommandInterpreter ci = new CommandInterpreter(
					DefaultCommands.Find | DefaultCommands.PipeCommands | DefaultCommands.IORedirect,
					new TestCommands());

				//Redirect output:
				result = Capture(ci, "Count 100 > " + tempPath);
				Assert.AreEqual(String.Empty, result);
				Assert.AreEqual(100, File.ReadAllLines(tempPath).Length);

				result = Capture(ci, "Find \"1\" -f:" + tempPath + " |Find \"0\" > " + tempPath2);
				Assert.AreEqual(String.Empty, result);
				Assert.AreEqual("10\r\n100", File.ReadAllText(tempPath2).Trim());

				//Redirect input:
				result = Capture(ci, "Find \"1\" |Find \"0\" <" + tempPath + " >" + tempPath2);
				Assert.AreEqual(String.Empty, result);
				Assert.AreEqual("10\r\n100", File.ReadAllText(tempPath2).Trim());

				//Change precedence and watch it fail:
				Assert.IsTrue(ci.FilterPrecedence.StartsWith("<") || ci.FilterPrecedence.StartsWith(">"));
				ci.FilterPrecedence = ci.FilterPrecedence.TrimStart('<', '>');

				result = Capture(ci, "Find \"1\" |Find \"0\" <" + tempPath + " >" + tempPath2);
				Assert.AreEqual(String.Empty, result);
				result = File.ReadAllText(tempPath2).Trim();
				Assert.AreEqual("10\r\n20\r\n30\r\n40\r\n50\r\n60\r\n70\r\n80\r\n90\r\n100", result);
			}
			finally
			{
				File.Delete(tempPath);
				File.Delete(tempPath2);
			}
		}

		[Test]
		public void TestAttributes()
		{
			CommandInterpreter ci = new CommandInterpreter(new TestCommands());

			IOption option = ci.Options[0];

			//[Option("Other")]
			Assert.AreEqual("Other", option.DisplayName);
			Assert.AreEqual(typeof(int), option.Type);
			//[AliasName("alias")]
			//[System.ComponentModel.DisplayName("ingored-due-to-OptionAttribute")]
			Assert.AreEqual(2, option.AllNames.Length);
			Assert.IsTrue(new List<string>(option.AllNames).Contains("Other"));
			Assert.IsTrue(new List<string>(option.AllNames).Contains("alias"));
			//[System.ComponentModel.Description("description")]
			Assert.AreEqual("description", option.Description);
			//[System.ComponentModel.Category("category")]
			Assert.AreEqual("category", option.Category);
			//[System.ComponentModel.Browsable(false)]
			Assert.AreEqual(false, option.Visible);
			//[System.ComponentModel.DefaultValue(-1)]
			Assert.AreEqual(-1, option.Value);

			{
				CommandFilterAttribute a = new CommandFilterAttribute();
				Assert.IsFalse(a.Visible);
				a.Visible = true;
				Assert.IsFalse(a.Visible);
			}
			{
				CommandAttribute a = new CommandAttribute();
				a.DisplayName = "test";
				a.AliasNames = new string[] { "alias" };
				Assert.AreEqual("test,alias", String.Join(",", a.AllNames));
				IDisplayInfo di = a;
				di.Help();//no-op
			}
			{
				AllArgumentsAttribute a = new AllArgumentsAttribute();
				Assert.AreEqual(typeof(AllArgumentsAttribute), a.GetType());
			}
		}

        [Test]
	    public void TestNullableDefaultArgs()
	    {
			CommandInterpreter ci = new CommandInterpreter(new TestCommands());

	        ICommand cmd;
	        Assert.IsTrue(ci.TryGetCommand("NullableDefaultArgs", out cmd));

	        var args = cmd.Arguments;
            Assert.AreEqual(4, args.Length);
            
            Assert.AreEqual("lval", args[0].DisplayName);
            Assert.AreEqual(true, args[0].Required);

            Assert.AreEqual("ival", args[1].DisplayName);
            Assert.AreEqual(false, args[1].Required);
            Assert.AreEqual(0, args[1].DefaultValue);

            Assert.AreEqual("enumVal", args[2].DisplayName);
            Assert.AreEqual(false, args[2].Required);
            Assert.AreEqual(null, args[2].DefaultValue);

            Assert.AreEqual("sval", args[3].DisplayName);
            Assert.AreEqual(false, args[3].Required);
            Assert.AreEqual(false, args[3].Visible);
            Assert.AreEqual("SomeText", args[3].DefaultValue);

            ci.Run("NullableDefaultArgs", "1");
            Assert.AreEqual(0, ci.ErrorLevel);
	    }

	    [Test]
		public void EnsureSerializationOfException()
		{
			InterpreterException ex = null;
			try { throw new InterpreterException("TEST"); }
			catch (InterpreterException e) { ex = e; }

			Assert.IsNotNull(ex);
			BinaryFormatter bf = new BinaryFormatter();
			
			using( MemoryStream ms = new MemoryStream() )
			{
				bf.Serialize(ms, ex);

				ms.Position = 0;
				ex = (InterpreterException)bf.Deserialize(ms);
				Assert.AreEqual("TEST", ex.Message);
			}
		}

		[Test][ExpectedException(typeof(InvalidOperationException))]
		public void FailConsoleIO()
		{
			CommandInterpreter ci = new CommandInterpreter();
			ci.ReadNextCharacter();
		}
	}
}
