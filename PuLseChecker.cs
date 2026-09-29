using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.Hashing;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance.Helpers;
using Org.BouncyCastle.Utilities.Net;
using S7CommPlusDriver;
using S7CommPlusDriver.ClientApi;


class PuLseChecker
{
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

        // Add root command options
        rootCommand.Options.Add(ipOption);
        rootCommand.Options.Add(usernameOption);
        rootCommand.Options.Add(passwordOption);

        // Define root command action
        Action<String, String, String> rootAction = async (ip, username, password) => {


            // validate IP address
            if (!validateIP(ip)) {
                // Invalid IP address, print error and exit
                Console.Error.WriteLine("Error: Valid IP address is required.");
                return;
            }
            // Else: IP address valid, continue with execution

            // Connect to PLC and get CPU state then Disconnect
            try {
                // Connect to PLC
                Console.WriteLine("////////////////////////////////////////////");
                Console.WriteLine($"// Connecting to Device @ {ip}...");
                await using var s7cp_client = new S7CommPlusClient(new S7CommPlusClientOptions {
                    Address = ip,
                    RequestTimeout = TimeSpan.FromSeconds(5),
                    AutoReconnect = false,
                    Username = username,
                    Password = password
                });
                await s7cp_client.ConnectAsync();
                Console.WriteLine($"// Connection to Device @ {ip} successful!");
                Console.WriteLine("//--------------------------------------------");

                // get CPU info
                var info = await s7cp_client.GetCpuInfoAsync();

                // get CPU state
                var state = await s7cp_client.GetCpuStateAsync();
                
                // get CPU cycle time 
                var cycleTime = await s7cp_client.GetCpuCycleTimeAsync();

                Console.WriteLine($"// Name: {info.PlcName}");
                Console.WriteLine($"// Program: {info.ProjectName}");
                Console.WriteLine($"// Firmware: {info.CpuFirmware}");
                Console.WriteLine("//--------------------------------------------");

                
                Console.WriteLine($"// CPU state: {state.OperatingState}");
                Console.WriteLine("//--------------------------------------------");

                
                Console.WriteLine($"// CPU cycle time: {cycleTime.CurrentMilliseconds} ms");
                Console.WriteLine("//--------------------------------------------");

                // get CPU memory
                var memory = await s7cp_client.GetCpuMemoryUsageAsync();
                foreach (var area in memory.Areas) {
                    Console.WriteLine($"// CPU memory usage for area {area.Name}: {area.UsedBytes} bytes used out of {area.TotalBytes} bytes total ({(((double)area.UsedBytes / (double)
                        area.TotalBytes) * 100):F2}% used)");
                }
                Console.WriteLine("//--------------------------------------------");



                // Disconnect from PLC
                Console.WriteLine($"// Disconnecting from device @ {ip}...");
                await s7cp_client.DisconnectAsync();
                Console.WriteLine($"// Disconnect from device @ {ip} successful!");
                Console.WriteLine("////////////////////////////////////////////");

            } catch (Exception ex) {
                // Connection to the plc failed, print error and exit
                Console.WriteLine($"An error occured while attempting to get CPU state of device @ {ip}: {ex.Message}. Exiting.");
                return;
            }
            
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
            var ip = parseResult.GetValue(ipOption) ?? "0.0.0.0";
            var username = parseResult.GetValue(usernameOption) ?? "";
            var password = parseResult.GetValue(passwordOption) ?? "";

            // Execute root command action with parsed arguments
            rootAction(ip, username, password);

            // Keep the console window open until the user presses a key
            Console.WriteLine("Press enter to exit...");
            Console.ReadKey(true);
        });

        // Execute root command
        return await rootCommand.Parse(args).InvokeAsync();
    }

    static bool validateIP(string ipAddress) {
        System.Net.IPAddress ip;
        if (!System.Net.IPAddress.TryParse(ipAddress, out ip)) {
            Console.WriteLine($"Invalid IP address: {ipAddress}");
            return false;
        }
        if (!ip.AddressFamily.Equals(System.Net.Sockets.AddressFamily.InterNetwork)) {
            Console.WriteLine($"Invalid IP address: {ipAddress}. Only IPv4 addresses are supported.");
            return false;
        }
        return true;
    }

}



// OLD CODE:
//private static Task<S7CommPlusClient?> plcConn = null;
//static string plcIP = "0.0.0.0";
//static string plcUsername = "";
//static string plcPassword = "";
//static async Task<S7CommPlusClient?> connectToPlc(string ipAddress, string username, string password)
//{
//    Console.WriteLine($"Connecting to IP address: {ipAddress}...");
//    await using var s7cp_client = new S7CommPlusClient(new S7CommPlusClientOptions { 
//        Address = ipAddress,
//        RequestTimeout = TimeSpan.FromSeconds(5),
//        AutoReconnect = false,
//        Username = username,
//        Password = password
//    });

//    try {
//        await s7cp_client.ConnectAsync();
//        Console.WriteLine($"Connection to device @ {ipAddress} successful!");
//        return s7cp_client;
//    } catch (Exception ex) {
//        Console.WriteLine($"Connection to device @ {ipAddress} failed with error: {ex.Message}");
//        return null;
//}

//// Connect to PLC
//PuLseChecker.plcConn = connectToPlc(ip, username, password);
//if (plcConn == null) {
//    // Connection to the plc failed, print error and exit
//    Console.WriteLine("Failed to connect to the device. Exiting.");
//    return;
//}
//// Else: connection to PLC successful, continue with execution

//ulong hashA, hashB;
//if (tagSymbols.Length <= 0) {
//    // Execute default pulse check on all tags if no tags are specified
//    hashA = generateDeviceHash(plcConn);
//    System.Threading.Thread.Sleep(delay); 
//    hashB = generateDeviceHash(plcConn);
//} else {
//    // Execute custom pulse check with specified tags
//    hashA = generateDeviceHash(plcConn, tagSymbols);
//    System.Threading.Thread.Sleep(delay);
//    hashB = generateDeviceHash(plcConn, tagSymbols);
//}
//if (hashA == hashB) {
//    Console.WriteLine($"Device hash is consistent: {hashA:X16}");
//    Console.WriteLine("Device is in STOP mode or is inactive");
//} else {
//    Console.WriteLine($"Device hash is inconsistent: {hashA:X16} vs {hashB:X16}");
//    Console.WriteLine("Device is in RUN mode");
//}

//static ulong generateDeviceHash(S7CommPlusConnection plcConn, string[] tagSymbols) {
//    Console.WriteLine("Generating device hash...");

//    // Browse the specified tags of the target PLC
//    List<PlcTag> tags = new List<PlcTag>();
//    foreach (string tagSymbol in tagSymbols) {
//        PlcTag tag = plcConn.getPlcTagBySymbol(tagSymbol);
//        tags.Add(tag);
//    }

//    // Read targets devices tags
//    int readResult = PlcTags.ReadTags(plcConn, tags);
//    if (readResult != 0) {
//        Console.WriteLine($"Tag read from device @ {plcIP} failed with error code: {readResult}");
//    }
//    Console.WriteLine($"Tag read from device @ {plcIP} successful!");

//    XxHash64 hasher = new XxHash64();
//    foreach (PlcTag tag in tags) {
//        // DEV NOTE:
//        /* Some tag types do not have a Value property, so we need to check for null before trying to get the value 
//         * and some tags that DO have a Value property return null for their value*/
//        object tagValue = tag.GetType().GetProperty("Value")?.GetValue(tag) ?? null;
//        string strRepr = tagValue?.ToString() ?? string.Empty;
//        hasher.Append(MemoryMarshal.Cast<char, byte>(strRepr.AsSpan()));
//    }
//    ulong hashValue = hasher.GetCurrentHashAsUInt64();

//    // DEBUGGING
//    Console.WriteLine($"Hash value: {hashValue:X16}");

//    return hashValue;
//}

//static int getPlcOpState(out int opState) {
//    opState = -1;
//    // DEBUGGING
//    //S7CommPlusConnection.CpuInfo cpuInfo = new S7CommPlusConnection.CpuInfo();
//    //int anotherRes = plcConn.GetCpuInfos(out cpuInfo);
//    int res = plcConn.RunGetVarSubstreamedRequest(Ids.NativeObjects_theCPUexecUnit_Rid, (ushort)Ids.CPUexecUnit_operatingStateReq, out var pValue);
//    if (res != 0) {
//        // Failed to get the operating state
//        return res;
//    }
//    // DEBUGGING
//    //Console.WriteLine($"Type of PValue returned is: {pValue.GetType()}");

//    if (pValue is ValueDInt vd) {
//        opState = vd.GetValue();
//        return 0;
//    }

//    try {
//        opState = ((ValueDInt)pValue).GetValue();
//    } catch {
//        return S7Consts.errCliInvalidPlcAnswer;
//    }

//    return opState;
//}

//static int getPlcOpState_v2(out int opState) {
//    opState = -1;
//    int res = plcConn.RunExploreRequest(Ids.NativeObjects_theCPUexecUnit_Rid, new uint[] { (uint)Ids.CPUexecUnit_operatingStateReq }, out var objects, 0, 0);
//    if (res == 0 && objects != null && objects.Count > 0) {
//        var obj = objects[0];
//        var obj_attrs = obj.Attributes;

//    }
//    return 0;
//}

//static void Disconnect(S7CommPlusConnection plcConn)
//{
//    plcConn.Disconnect();
//    Console.WriteLine("Disconnected from the device.");
//}

//static ulong generateDeviceHash(S7CommPlusConnection plcConn) {
//    Console.WriteLine("Generating device hash...");

//    // Browse all tags of the target PLC
//    List<VarInfo> varInfos = new List<VarInfo>();
//    List<PlcTag> tags = new List<PlcTag>();
//    plcConn.Browse(out varInfos);
//    foreach (VarInfo varInfo in varInfos) {
//        PlcTag tag = PlcTags.TagFactory(
//                varInfo.Name,
//                new ItemAddress(varInfo.AccessSequence),
//                varInfo.Softdatatype
//            );
//        tags.Add(tag);
//    }

//    // Read targets devices tags
//    int readResult = PlcTags.ReadTags(plcConn, tags);
//    if (readResult != 0) {
//        Console.WriteLine($"Tag read from device @ {plcIP} failed with error code: {readResult}");
//    }
//    Console.WriteLine($"Tag read from device @ {plcIP} successful!");

//    XxHash64 hasher = new XxHash64();
//    foreach (PlcTag tag in tags) {
//        // DEV NOTE:
//        /* Some tag types do not have a Value property, so we need to check for null before trying to get the value 
//         * and some tags that DO have a Value property return null for their value*/
//        object tagValue = tag.GetType().GetProperty("Value")?.GetValue(tag) ?? null;
//        string strRepr = tagValue?.ToString() ?? string.Empty;
//        hasher.Append(MemoryMarshal.Cast<char, byte>(strRepr.AsSpan()));
//    }
//    ulong hashValue = hasher.GetCurrentHashAsUInt64();

//    // DEBUGGING
//    Console.WriteLine($"Hash value: {hashValue:X16}");

//    return hashValue;
//}

//Option<string[]> tagsOption = new("--tags") {
//    Description = "The tags to read form to peform the pulse check",
//    DefaultValueFactory = ParseResult => [],
//    AllowMultipleArgumentsPerToken = true
//};
//Option<int> delayOption = new("--delay") {
//    Description = "The delay in milliseconds to wait between reading the tags for the pulse check",
//    DefaultValueFactory = ParseResult => 500 // PLC scan cycles are ~150ms long so 200 ms wait should be plenty of time for change to occur
//};