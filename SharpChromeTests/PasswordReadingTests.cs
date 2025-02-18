﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SharpChrome;
using SharpChrome.Commands;
using SharpChrome.Extensions;

namespace SharpChromeTests
{
    [TestClass]
    public class PasswordReadingTests: BaseTester
    {
        [TestMethod]
        public void TestLoginSync()
        {
            SharpChrome.Program.Main(new [] { LoginSync.CommandName });
        }

        [TestMethod]
        public async Task TestShowUsage()
        {
            //using var consoleOutput = Console.OpenStandardOutput(255);
            //var currentProcess = Process.GetCurrentProcess();
            //var consoleOutput = currentProcess.StandardOutput;
            SharpChrome.Program.Main(new [] { "help" });

            //var allConsoleOutput = await consoleOutput.ReadToEndAsync();
        }

        [TestMethod]
        public void TestGetLoginCommandChrome()
        {
            throw new NotSupportedException();
            SharpChrome.Program.Main(new [] { "logins", "/format:csv", "/browser:chrome" });
        }

        [TestMethod]
        public void TestReadingAndWritingLocalChromiumPasswords()
        {
            var cTempChrome = @"C:\temp\chrome";
            var browser = Browser.Chrome;
            var logins = SharpChrome.Chrome.ReadLocalChromiumLogins(cTempChrome, browser);

            var key = Chrome.GetChromiumStateKey(cTempChrome, browser);
            Chrome.WriteLocalChromiumLogins(@"C:\temp\edge", logins, key);
        }

        [TestMethod]
        public void TestReadingLocalChromiumPasswords()
        {
            var browser = Browser.Chrome;

            var logins = SharpChrome.Chrome.ReadLocalChromiumLogins(@"C:\temp\chrome", browser);

            Assert.IsNotNull(logins);
        }
    }
}
