using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Text;
using System.Net;

using S7CommPlusDriver;
using S7CommPlusDriver.ClientApi;
using System.Runtime.InteropServices;


class Program
{
    static string deviceIP = "";
    static S7CommPlusConnection deviceConn = new S7CommPlusConnection();
    static bool ipValid = false;
    static bool isConnected = false;

    

    static int Main(string[] args)
    {
        // Define root command and root command options
        RootCommand rootCommand = new("Check the RUN status of a PLC");
        rootCommand.Options.Add(
            new Option<string>("--ip") { 
                Description = "The IP address of the PLC to check.",
                Required = true
            }
        );
        rootCommand.Options.Add(
            new Option<string>("--username") { 
                Description = "The username for authentication.",
                DefaultValueFactory = ParseResult => ""
            }
        );
        rootCommand.Options.Add(
            new Option<string>("--password") { 
                Description = "The password for authentication.",
                DefaultValueFactory = ParseResult => ""
            }
        );

        // Define subcommands for the root command
        Command customPulseCheck = new("custom", "check the RUN mode of the target PLC by checking a specified subset of tags");
        Command clockPulseCheck = new("clock", "check the RUN mode of the target PLC by checking one of the specificed system clock tags");


        rootCommand.Subcommands.Add(
            new Command("custom", "check the RUN mode of the target PLC by checking a specified subset of tags")
        );

        // Define command-line subcommands
        

        // Define command-line options
        Option<String> ipOption = new("--ip") {
            Description = "The IP address to connect to.",
            Required = true
        };
        Option<String> usernameOption = new("--username") {
            Description = "The username for authentication.",
            DefaultValueFactory = ParseResult => ""
        };
        Option<string> passwordOption = new("--password") {
            Description = "The password for authentication.",
            DefaultValueFactory = ParseResult => ""
        };


        rootCommand.Subcommands.Add(customPulseCheck);
        rootCommand.Subcommands.Add(clockPulseCheck);
        

        rootCommand.Options.Add(ipOption);
        rootCommand.Options.Add(usernameOption);
        rootCommand.Options.Add(passwordOption);

        // Parse the command-line arguments
        ParseResult parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count > 0) {
            foreach (ParseError parseError in parseResult.Errors) {
                Console.Error.WriteLine($"Error: {parseError.Message}");
            }
            return 1;
        }
        if (parseResult.GetValue(ipOption) is String parsedIP && validateIP(parsedIP)) {
            deviceIP = parsedIP;
        } else {
            Console.Error.WriteLine("Error: Valid IP address is required.");
            return 1;
        }


        

        isConnected = ConnectToIP(deviceIP);
        if (!isConnected) {
            Console.WriteLine("Failed to connect to the device. Exiting.");
            return 1;
        }

        ulong hashA = GenerateDeviceHash();
        System.Threading.Thread.Sleep(500); // PLC scan cycles are ~150ms long so 200 ms wait should be plenty of time for change to occur
        ulong hashB = GenerateDeviceHash();
        if (hashA == hashB) {
            Console.WriteLine($"Device hash is consistent: {hashA:X16}");
            Console.WriteLine("Device is in STOP mode or is inactive");
        } else {
            Console.WriteLine($"Device hash is inconsistent: {hashA:X16} vs {hashB:X16}");
            Console.WriteLine("Device is in RUN mode");
        }

        Disconnect();
        return 0;
    }

    static bool validateIP(string ipAddress) {
        IPAddress ip;
        if (!IPAddress.TryParse(ipAddress, out ip)) {
            Console.WriteLine($"Invalid IP address: {ipAddress}");
            return false;
        }
        if (!ip.AddressFamily.Equals(System.Net.Sockets.AddressFamily.InterNetwork)) {
            Console.WriteLine($"Invalid IP address: {ipAddress}. Only IPv4 addresses are supported.");
            return false;
        }
        return true;
    }

    static bool ConnectToIP(string ipAddress)
    {
        Console.WriteLine($"Connecting to IP address: {ipAddress}...");
        int connectResult = deviceConn.Connect(ipAddress);
        if (connectResult != 0) {
            Console.WriteLine($"Connection to device @ {ipAddress} failed with error code: {connectResult}");
            return false;
        }  
        Console.WriteLine($"Connection to device @ {ipAddress} successful!");
        return true;
    }

    static ulong GenerateDeviceHash() {
        Console.WriteLine("Generating device hash...");

        // Browse the target device and get its tags
        List<VarInfo> varInfos = new List<VarInfo>();
        List<PlcTag> tags = new List<PlcTag>();
        deviceConn.Browse(out varInfos);
        foreach (VarInfo varInfo in varInfos) {
            PlcTag tag = PlcTags.TagFactory(
                    varInfo.Name,
                    new ItemAddress(varInfo.AccessSequence),
                    varInfo.Softdatatype
                );
            tags.Add(tag);
        }

        // Read targets devices tags
        int readResult = PlcTags.ReadTags(deviceConn, tags);
        if (readResult != 0) {
            Console.WriteLine($"Tag read from device @ {deviceIP} failed with error code: {readResult}");
        }
        Console.WriteLine($"Tag read from device @ {deviceIP} successful!");

        XxHash64 hasher = new XxHash64();
        foreach (PlcTag tag in tags) {
            // DEV NOTE:
            /* Some tag types do not have a Value property, so we need to check for null before trying to get the value 
             * and some tags that DO have a Value property return null for their value*/
            object tagValue = tag.GetType().GetProperty("Value")?.GetValue(tag) ?? null; 
            string strRepr = tagValue?.ToString() ?? string.Empty;
            hasher.Append(MemoryMarshal.Cast<char, byte>(strRepr.AsSpan()));
        }
        ulong hashValue = hasher.GetCurrentHashAsUInt64();

        // DEBUGGING
        Console.WriteLine($"Hash value: {hashValue:X16}");

        return hashValue;
    }

    static void Disconnect()
    {
        if (!isConnected) {
            Console.WriteLine("Cannot disconnect from device. Not connected to any device.");
            return;
        }
        deviceConn.Disconnect();
        Console.WriteLine("Disconnected from the device.");
    }
}


//OLD CODE:
// DEBUGGING
//Console.WriteLine($"Found {varInfos.Count} variables on the device.");