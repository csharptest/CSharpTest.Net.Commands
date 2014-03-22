#region Copyright 2008-2014 by Roger Knapp, Licensed under the Apache License, Version 2.0
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
using CSharpTest.Net.Commands;
using NUnit.Framework;

#pragma warning disable 618 //CSharpTest.Net.Utils.ArgumentList.Join(params string[])' is obsolete
#pragma warning disable 1591
namespace CSharpTest.Net.CommandsTest
{
	[TestFixture]
	[Category("TestArgumentList")]
	public partial class TestArgumentList
	{
		#region TestFixture SetUp/TearDown
		[TestFixtureSetUp]
		public virtual void Setup()
		{
		}

		[TestFixtureTearDown]
		public virtual void Teardown()
		{
		}
		#endregion

		[Test]
		public void Test()
		{
			ArgumentList args = new ArgumentList("-test=value", "/Test", "\"/other:value\"");
			Assert.AreEqual(2, args.Count);

			Assert.AreEqual(1, args[0].Count);
			Assert.AreEqual("test", args[0].Name);
			Assert.AreEqual("value", args[1].Value);

			Assert.AreEqual(1, args[1].Count);
			Assert.AreEqual("other", args[1].Name);
			Assert.AreEqual("value", args[1].Value);
			
			string[] keys = args.Keys;
			Assert.AreEqual(2, keys.Length);
			Assert.AreEqual("other", keys[0]);//alpha-sorted
			Assert.AreEqual("test", keys[1]);
			Assert.AreEqual(0, new ArgumentList("unnamed").Keys.Length);
			Assert.AreEqual(0, new ArgumentList(/*empty*/).Keys.Length);

			ArgumentList.DefaultComparison = StringComparer.Ordinal;
			Assert.AreEqual(StringComparer.Ordinal, ArgumentList.DefaultComparison);
			
			ArgumentList.NameDelimeters = new char[] { '=' };
			Assert.AreEqual('=', ArgumentList.NameDelimeters[0]);

			ArgumentList.PrefixChars = new char[] { '/' };
			Assert.AreEqual('/'	, ArgumentList.PrefixChars[0]);

			args = new ArgumentList("-test=value", "/Test", "\"/other:value\"");
			Assert.AreEqual(2, args.Count);
			Assert.AreEqual(0, args[0].Count);
			Assert.AreEqual("Test", args[0].Name);
			Assert.AreEqual(null, args[1].Value);

			Assert.AreEqual(1, args.Unnamed.Count);
			foreach(string sval in args.Unnamed)
				Assert.AreEqual("-test=value", sval);

			Assert.AreEqual(0, args[1].Count);
			Assert.AreEqual("other:value", args[1].Name);
			Assert.AreEqual(null, args[1].Value);

			args.Unnamed = new string[0];
			Assert.AreEqual(0, args.Unnamed.Count);

			args.Add("other", "value");
			Assert.AreEqual(null, (string)args["Test"]);
			Assert.AreEqual("value", (string)args["other"]);
			Assert.AreEqual("value", (string)args.SafeGet("other"));
			Assert.IsNotNull(args.SafeGet("other-not-existing"));
			Assert.AreEqual(null, (string)args.SafeGet("other-not-existing"));

			string test;
			ArgumentList.Item item;

			args = new ArgumentList();
			Assert.AreEqual(0, args.Count);
			Assert.IsFalse(args.TryGetValue(String.Empty, out item));
			args.Add(String.Empty, null);
			Assert.IsTrue(args.TryGetValue(String.Empty, out item));

			args = new ArgumentList();
			Assert.AreEqual(0, args.Count);
			Assert.IsFalse(args.TryGetValue(String.Empty, out test));
			args.Add(String.Empty, null);
			Assert.IsTrue(args.TryGetValue(String.Empty, out test));

			test = item;
			Assert.IsNull(test);

			string[] testarry = item;
			Assert.IsNotNull(testarry);
			Assert.AreEqual(0, testarry.Length);

			item.Value = "roger";
			Assert.AreEqual("roger", item.Value);
			Assert.AreEqual(1, item.Values.Length);
			Assert.AreEqual("roger", item.Values[0]);

			Assert.Contains("roger", item.ToArray());
			Assert.AreEqual(1, item.ToArray().Length);

			item.AddRange(new string[] { "wuz", "here" });
			Assert.AreEqual(3, item.Values.Length);
			Assert.AreEqual("roger wuz here", String.Join(" ", item));

			item.Values = new string[] { "roger", "was", "here" };
			Assert.AreEqual("roger was here", String.Join(" ", item));

			KeyValuePair<string, string[]> testkv = item;
			Assert.AreEqual(String.Empty, testkv.Key);
			Assert.AreEqual(3, testkv.Value.Length);
			Assert.AreEqual("roger was here", String.Join(" ", testkv.Value));
		}
		
		[Test]
		public void TestUnnamed()
		{
			ArgumentList args = new ArgumentList("some", "/thing", "else");
			Assert.AreEqual(2, args.Unnamed.Count);
			Assert.AreEqual(1, args.Count);
			Assert.IsTrue(args.Contains("thing"));
			Assert.AreEqual("some", args.Unnamed[0]);
			Assert.AreEqual("else", args.Unnamed[1]);

			args.Unnamed.RemoveAt(0);
			Assert.AreEqual(1, args.Unnamed.Count);
			Assert.AreEqual("else", args.Unnamed[0]);

			args.Clear();
			Assert.AreEqual(0, args.Count);
			Assert.AreEqual(1, args.Unnamed.Count);

			args.Unnamed.Clear();
			Assert.AreEqual(0, args.Unnamed.Count);
		}

		[Test]
		public void TestParseRemove()
		{
			//reset
			ArgumentList.PrefixChars = new char[] { '/', '-' };
			ArgumentList.NameDelimeters = new char[] { '=', ':' };

			string[] arguments = new string[] { "bla", "/one=1", "/two", "-", "/", "", "-three:3", "/four : 4", "/5", "/5:" };

			int count = arguments.Length;
			string value;
			Assert.IsTrue(ArgumentList.Remove(ref arguments, "one", out value), "found item in array");
			Assert.AreEqual(--count, arguments.Length, "was removed from array?");
			Assert.AreEqual("1", value, "Extracted value correctly?");

			Assert.IsTrue(ArgumentList.Remove(ref arguments, "two", out value), "found item in array");
			Assert.AreEqual(--count, arguments.Length, "was removed from array?");
			Assert.IsNull(value, "Extracted value correctly?");

			Assert.IsTrue(ArgumentList.Remove(ref arguments, "three", out value), "found item in array");
			Assert.AreEqual(--count, arguments.Length, "was removed from array?");
			Assert.AreEqual("3", value, "Extracted value correctly?");

			Assert.IsFalse(ArgumentList.Remove(ref arguments, "four", out value), "not found in array");
			Assert.IsTrue(ArgumentList.Remove(ref arguments, "four ", out value), "found item in array");
			Assert.AreEqual(--count, arguments.Length, "was removed from array?");
			Assert.AreEqual(" 4", value, "Extracted value correctly?");

			Assert.IsTrue(ArgumentList.Remove(ref arguments, "5", out value), "found item in array");
			Assert.AreEqual(--count, arguments.Length, "was removed from array?");
			Assert.IsNull(value, "Extracted value correctly?");
			Assert.IsTrue(ArgumentList.Remove(ref arguments, "5", out value), "found item in array");
			Assert.AreEqual(--count, arguments.Length, "was removed from array?");
			Assert.AreEqual("", value, "Extracted value correctly?");
		}
		
		[Test]
		public void TestParseJoin()
		{
			// all of these result in three argument values and should re-join exactly as appears
			string[] test_valid_strings = new string[] {
				"a b c",
				"a b \"c c\"",
				"a b \" c \"",
				"a \"b\"\"b\" c",
				"a \"\"\"b\"\"\" c",
			};

			foreach (string testinput in test_valid_strings)
			{
				string[] result = ArgumentList.Parse(testinput);
				Assert.AreEqual(3, result.Length, "failed to find three values");
				string joined = ArgumentList.Join(result);
				Assert.AreEqual(testinput, joined, "failed to parse/join correctly");
			}

			//the following do not re-join exactly:
			Assert.AreEqual("a b c", ArgumentList.Join(ArgumentList.Parse("a \"b\" c")), "failed to parse/join correctly");
			Assert.AreEqual("a \"b\"\"b\" c", ArgumentList.Join(ArgumentList.Parse("a b\"b c")), "failed to parse/join correctly");
			Assert.AreEqual("a b c", ArgumentList.Join(ArgumentList.Parse("a b \"c")), "failed to parse/join correctly");
		}
	}

	[TestFixture]
	[Category("TestArgumentList")]
	public partial class TestArgumentListNegative
	{
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestCTor()
		{
			new ArgumentList((string[])null);
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestParseNull()
		{
			ArgumentList.Parse(null);
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestJoinNull()
		{
			ArgumentList.Join(null);
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestDefaultComparison()
		{
			ArgumentList.DefaultComparison = null;
		}

		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestNameDelimeters()
		{
			ArgumentList.NameDelimeters = null;
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestPrefixChars()
		{
			ArgumentList.PrefixChars = null;
		}

		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentOutOfRangeException))]
		public void TestNameDelimeters2()
		{
			ArgumentList.NameDelimeters = new char[0];
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentOutOfRangeException))]
		public void TestPrefixChars2()
		{
			ArgumentList.PrefixChars = new char[0];
		}

		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestAddRange()
		{
			new ArgumentList().AddRange(null);
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestAddRange2()
		{
			new ArgumentList().AddRange(new string[] { "1", null, "2" });
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestAdd()
		{
			new ArgumentList().Add(null, null);
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestTryGetValue()
		{
			ArgumentList.Item item;
			new ArgumentList().TryGetValue(null, out item);
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestTryGetValue2()
		{
			string item;
			new ArgumentList().TryGetValue(null, out item);
		}
		[Test]
        [ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestValueAssignment()
		{
			ArgumentList.Item item = null;
			KeyValuePair<string, string[]> kv = item;
		}
		[Test]
		[ExpectedException(ExpectedException = typeof(ArgumentNullException))]
		public void TestItemNameNull()
		{
			ArgumentList.Item item = new ArgumentList.Item(null, null);
		}
	}
}
