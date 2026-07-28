using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Text;

using S7CommPlusDriver;
using S7CommPlusDriver.ClientApi;
using System.Runtime.InteropServices;


class Program
{
    static S7CommPlusConnection deviceConn = new S7CommPlusConnection();
    static bool isConnected = false;

    static int Main(string[] args)
    {
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

        // Create a root command and add the options
        RootCommand rootCommand = new("A simple command-line application.");
        rootCommand.Options.Add(ipOption);
        rootCommand.Options.Add(usernameOption);
        rootCommand.Options.Add(passwordOption);

        // Parse the command-line arguments
        ParseResult parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count == 0 && parseResult.GetValue(ipOption) is String parsedIP) {
            ConnectToIP(parsedIP);
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
        foreach (ParseError parseError in parseResult.Errors) {
            Console.Error.WriteLine($"Error: {parseError.Message}");
        }
        return 1;

    }

    static void ConnectToIP(string ipAddress)
    {
        Console.WriteLine($"Connecting to IP address: {ipAddress}...");
        int connectResult = deviceConn.Connect(ipAddress);
        if (connectResult == 0) {
            Console.WriteLine($"Connection to device @ {ipAddress} successful!");
            isConnected = true;
        } else {
            Console.WriteLine($"Connection failed with error code: {connectResult}");
        }
    }

    static ulong GenerateDeviceHash() {
        if (!isConnected) {
            Console.WriteLine("Cannot generate device hash. Not connected to any device.");
            return 0;
        }

        // Browse the target device
        List<VarInfo> varInfos = new List<VarInfo>();
        deviceConn.Browse(out varInfos);

        // DEBUGGING
        Console.WriteLine($"Found {varInfos.Count} variables on the device.");

        XxHash64 hasher = new XxHash64();
        foreach (VarInfo varInfo in varInfos) {
            PlcTag tag = PlcTags.TagFactory(
                    varInfo.Name,
                    new ItemAddress(varInfo.AccessSequence),
                    varInfo.Softdatatype
                );
            object tagValue = tag.GetType().GetProperty("Value").GetValue(tag);

            // DEBUGGING
            if (tag.Name.Contains("UP_TIME")) {
                Console.WriteLine($"Tag: {tag.Name}, Tag Type: {tag.GetType()}, Tag Value: {tag.ToString()}, Tag value data type: {tagValue?.GetType().ToString() ?? "null"}");
            }

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


/*
 // Browse the target device
            List<VarInfo> varInfos = new List<VarInfo>();
            deviceConn.Browse(out varInfos);
            Console.WriteLine($"Found {varInfos.Count} variables on the device.");

            // Iterate through varInfo list produced by browse and build PlcTag objects for each, extract thier values and append them to the hasher
            List<PlcTag> tags = new List<PlcTag>();
            List<Object> tagVals = new List<object>();
            XxHash64 hasher = new XxHash64();
            foreach (VarInfo varInfo in varInfos) {
                PlcTag tag = PlcTags.TagFactory(
                    varInfo.Name,
                    new ItemAddress(varInfo.AccessSequence),
                    varInfo.Softdatatype
                );
                object tagValue = tag.GetType().GetProperty("Value").GetValue(tag);
                tags.Add(tag);
                tagVals.Add(tagValue);

                string strRepr = tagValue?.ToString() ?? string.Empty;
                hasher.Append(MemoryMarshal.Cast<char, byte>(strRepr.AsSpan()));

                // DEBUGGING
                //if (tagValue == null) {
                //    Console.WriteLine($"Tag: {tag.Name}, Tag Value: {tagValue}, Tag value data type: {tagValue?.GetType().ToString() ?? "null"}");
                //}
                //Console.WriteLine($"Tag: {tag.Name}, Tag Value: {tagValue}, Tag value data type: {tagValue?.GetType().ToString() ?? "null"}");
                //PropertyInfo tagValueType = tag.GetType().GetProperty("Value");
                //object tagValue = tagValueType.GetValue(tag);
                //Console.WriteLine($"Tag Type: {tagValueType}, Tag Value: {tagValue}");
 */