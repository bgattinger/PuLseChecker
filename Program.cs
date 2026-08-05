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
using System.Threading.Tasks;


class Program
{
    static S7CommPlusConnection plcConn = new S7CommPlusConnection();
    static string plcIP = "0.0.0.0";
    static bool plcConnected = false;

    static async Task<int> Main(string[] args)
    {
        // Define root command
        RootCommand rootCommand = new("Check the RUN status of a PLC");

        // Define root command options
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
        Option<string[]> tagsOption = new("--tags") {
            Description = "The tags to read form to peform the pulse check",
            DefaultValueFactory = ParseResult => [],
            AllowMultipleArgumentsPerToken = true
        };
        Option<int> delayOption = new("--delay") {
            Description = "The delay in milliseconds to wait between reading the tags for the pulse check",
            DefaultValueFactory = ParseResult => 500 // PLC scan cycles are ~150ms long so 200 ms wait should be plenty of time for change to occur
        };

        // Add root command options
        rootCommand.Options.Add(ipOption);
        rootCommand.Options.Add(usernameOption);
        rootCommand.Options.Add(passwordOption);
        rootCommand.Options.Add(tagsOption);

        // Define root command action
        Action<String, String, String, String[], int> rootAction = (ip, username, password, tagSymbols, delay) => {
            // validate IP address
            if (!validateIP(ip)) {
                // Invalid IP address, print error and exit
                Console.Error.WriteLine("Error: Valid IP address is required.");
                return;
            }
            // Else: IP address valid, continue with execution

            // Connect to PLC
            // DEV NOTE: Need to handle if the usenrame and password have been specified and pass them to the connectToPLC function
            S7CommPlusConnection plcConn = connectToPlc(ip);
            if (plcConn == null) {
                // Connection to the plc failed, print error and exit
                Console.WriteLine("Failed to connect to the device. Exiting.");
                return;
            }
            // Else: connection to PLC successful, continue with execution

            ulong hashA, hashB;
            if (tagSymbols.Length <= 0) {
                // Execute default pulse check on all tags if no tags are specified
                hashA = generateDeviceHash(plcConn);
                System.Threading.Thread.Sleep(delay); 
                hashB = generateDeviceHash(plcConn);
            } else {
                // Execute custom pulse check with specified tags
                hashA = generateDeviceHash(plcConn, tagSymbols);
                System.Threading.Thread.Sleep(delay);
                hashB = generateDeviceHash(plcConn, tagSymbols);
            }
            if (hashA == hashB) {
                Console.WriteLine($"Device hash is consistent: {hashA:X16}");
                Console.WriteLine("Device is in STOP mode or is inactive");
            } else {
                Console.WriteLine($"Device hash is inconsistent: {hashA:X16} vs {hashB:X16}");
                Console.WriteLine("Device is in RUN mode");
            }

            // Disconnect from target PLC
            Disconnect(plcConn);
            return;
        };

        // Set root command action
        rootCommand.SetAction((ParseResult parseResult) => {
            // Validate command-line arguments
            if (parseResult.Errors.Count > 0) {
                // Invalid root command-line arguments, print errors and exit
                foreach (ParseError parseError in parseResult.Errors) {
                    Console.Error.WriteLine($"Error: {parseError.Message}");
                }
                return;
            }

            // Parse command-line arguments
            var ip = parseResult.GetValue(ipOption);
            var username = parseResult.GetValue(usernameOption);
            var password = parseResult.GetValue(passwordOption);
            var tags = parseResult.GetValue(tagsOption);
            var delay = parseResult.GetValue(delayOption);

            // Execute root command action with parsed arguments
            rootAction(ip, username, password, tags, delay);
        });

        // Execute root command
        return await rootCommand.Parse(args).InvokeAsync();
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

    static S7CommPlusConnection connectToPlc(string ipAddress)
    {
        S7CommPlusConnection plcConn = new S7CommPlusConnection();

        Console.WriteLine($"Connecting to IP address: {ipAddress}...");
        int connectResult = plcConn.Connect(ipAddress);

        if (connectResult != 0) {
            Console.WriteLine($"Connection to device @ {ipAddress} failed with error code: {connectResult}");
            return null;
        }  
        Console.WriteLine($"Connection to device @ {ipAddress} successful!");

        return plcConn;
    }

    static ulong generateDeviceHash(S7CommPlusConnection plcConn) {
        Console.WriteLine("Generating device hash...");

        // Browse all tags of the target PLC
        List<VarInfo> varInfos = new List<VarInfo>();
        List<PlcTag> tags = new List<PlcTag>();
        plcConn.Browse(out varInfos);
        foreach (VarInfo varInfo in varInfos) {
            PlcTag tag = PlcTags.TagFactory(
                    varInfo.Name,
                    new ItemAddress(varInfo.AccessSequence),
                    varInfo.Softdatatype
                );
            tags.Add(tag);
        }

        // Read targets devices tags
        int readResult = PlcTags.ReadTags(plcConn, tags);
        if (readResult != 0) {
            Console.WriteLine($"Tag read from device @ {plcIP} failed with error code: {readResult}");
        }
        Console.WriteLine($"Tag read from device @ {plcIP} successful!");

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

    static ulong generateDeviceHash(S7CommPlusConnection plcConn, string[] tagSymbols) {
        Console.WriteLine("Generating device hash...");

        // Browse the specified tags of the target PLC
        List<PlcTag> tags = new List<PlcTag>();
        foreach (string tagSymbol in tagSymbols) {
            PlcTag tag = plcConn.getPlcTagBySymbol(tagSymbol);
            tags.Add(tag);
        }

        // Read targets devices tags
        int readResult = PlcTags.ReadTags(plcConn, tags);
        if (readResult != 0) {
            Console.WriteLine($"Tag read from device @ {plcIP} failed with error code: {readResult}");
        }
        Console.WriteLine($"Tag read from device @ {plcIP} successful!");

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

    static int getPlcOpState(out int opState) {
        opState = -1;
        // DEBUGGING
        //S7CommPlusConnection.CpuInfo cpuInfo = new S7CommPlusConnection.CpuInfo();
        //int anotherRes = plcConn.GetCpuInfos(out cpuInfo);
        int res = plcConn.RunGetVarSubstreamedRequest(Ids.NativeObjects_theCPUexecUnit_Rid, (ushort)Ids.CPUexecUnit_operatingStateReq, out var pValue);
        if (res != 0) {
            // Failed to get the operating state
            return res;
        }
        // DEBUGGING
        //Console.WriteLine($"Type of PValue returned is: {pValue.GetType()}");

        if (pValue is ValueDInt vd) {
            opState = vd.GetValue();
            return 0;
        }

        try {
            opState = ((ValueDInt)pValue).GetValue();
        } catch {
            return S7Consts.errCliInvalidPlcAnswer;
        }

        return opState;
    }

    static int getPlcOpState_v2(out int opState) {
        opState = -1;
        int res = plcConn.RunExploreRequest(Ids.NativeObjects_theCPUexecUnit_Rid, new uint[] { (uint)Ids.CPUexecUnit_operatingStateReq }, out var objects, 0, 0);
        if (res == 0 && objects != null && objects.Count > 0) {
            var obj = objects[0];
            var obj_attrs = obj.Attributes;

        }
        return 0;
    }

    static void Disconnect(S7CommPlusConnection plcConn)
    {
        plcConn.Disconnect();
        Console.WriteLine("Disconnected from the device.");
    }
}