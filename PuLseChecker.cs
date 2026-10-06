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

        // DEV NOTE: want to add logging option to log output to a file
        //Option<bool> logOption = new Option<bool>("--log") {
        //    Description = "Whether to log the output to a file"
        //};

        // Define device command
        Argument<string> ipArgument = new Argument<string>("ip") {
            Description = "The IP address of the device to connect to"
        };
        Option<string> usernameOption = new Option<string>("--username") {
            Description = "The username for authentication"
        };
        Option<string> passwordOption = new Option<string>("--password") {
            Description = "The password for authentication"
        };
        Option<bool> cycleTimeOption = new Option<bool>("--cycle-time") {
            Description = "Whether to display the CPU cycle time"
        };
        Option<bool> cpuInfoOption = new Option<bool>("--cpu-info") {
            Description = "Whether to display additional CPU information"
        };
        Option<bool> memoryUsageOption = new Option<bool>("--memory-usage") {
            Description = "Whether to display the CPU memory usage"
        };
        Command deviceCommand = new Command("--device", "Check the CPU status of a device") {
            Arguments = {ipArgument},
            Options = {usernameOption, passwordOption, cycleTimeOption, cpuInfoOption, memoryUsageOption}
        };
        deviceCommand.SetAction(async (ParseResult parseResult) => {
            // Parse arguments and options
            string? ip = parseResult.GetValue(ipArgument);
            string username = parseResult.GetValue(usernameOption) ?? "";
            string password = parseResult.GetValue(passwordOption) ?? "";
            bool showCycleTime = parseResult.GetValue(cycleTimeOption);
            bool showCpuInfo = parseResult.GetValue(cpuInfoOption);
            bool showMemoryUsage = parseResult.GetValue(memoryUsageOption);

            // Validate ip address
            if (ip == null || !validateIP(ip)) {
                Console.Error.WriteLine("Error: IP address is required.");
                return;
            }

            try {
                // Connect to PLC
                Console.WriteLine(
                    "////////////////////////////////////////////" +
                    $"\n// Connecting to Device @ {ip}..."
                );
                await using var s7cp_client = new S7CommPlusClient(new S7CommPlusClientOptions {
                    Address = ip,
                    RequestTimeout = TimeSpan.FromSeconds(5),
                    AutoReconnect = false,
                    Username = username,
                    Password = password
                });
                await s7cp_client.ConnectAsync();
                Console.WriteLine(
                    $"// Connection to Device @ {ip} Successful!" +
                    "\n//--------------------------------------------" +
                    "\n// Getting Device Information..."
                );

                // get device CPU state
                var cpuState = await s7cp_client.GetCpuStateAsync();

                // get device CPU cycle time if option is set
                double cpuCycleTime_currentMilliseconds = 0.0;
                if (showCycleTime) {
                    var cycleTime = await s7cp_client.GetCpuCycleTimeAsync();
                    cpuCycleTime_currentMilliseconds = cycleTime.CurrentMilliseconds;
                }

                // get device CPU info if option is set
                string plcName = "";
                string projectName = "";
                System.Version cpuFirmware = null;
                if (showCpuInfo) {
                    var cpuInfo = await s7cp_client.GetCpuInfoAsync();
                    plcName = cpuInfo.PlcName;
                    projectName = cpuInfo.ProjectName;
                    cpuFirmware = cpuInfo.CpuFirmware;
                }

                // get device CPU memory usage if option is set
                List<(string, long, long)> memAreas = new List<(string name, long usedBytes, long totalBytes)>();
                if (showMemoryUsage) {
                    var memory = await s7cp_client.GetCpuMemoryUsageAsync();
                    foreach (var area in memory.Areas) {
                        memAreas.Add((area.Name, area.UsedBytes, area.TotalBytes));
                    }
                }

                // Disconnect from PLC
                Console.WriteLine(
                    $"// Successfully retrieved device information for device @ {ip}!" +
                    "\n//--------------------------------------------" +
                    "\n// Disconnecting from device..."
                );
                await s7cp_client.DisconnectAsync();
                Console.WriteLine(
                    $"// Disconnect from device @ {ip} successful!" +
                    "\n//--------------------------------------------" +
                    "\n// Device Information:" +
                    $"\n// CPU state: {cpuState.OperatingState}" +
                    (showCycleTime ? $"\n// CPU cycle time: {cpuCycleTime_currentMilliseconds} ms" : "") +
                    (showCpuInfo ? $"\n// Name: {plcName}\n// Program: {projectName}\n// Firmware: {cpuFirmware}" : "") +
                    (showMemoryUsage ? $"\n// CPU memory usage: \n" + string.Join("\n", memAreas.Select(area => $"//\t {area.Item1}: {area.Item2} bytes used out of {area.Item3} bytes total ({(((double)area.Item2 / (double)area.Item3) * 100):F2}% used)")) : "") +
                    "\n////////////////////////////////////////////"
                );

            } catch (Exception ex) {
                // Connection to the plc failed, print error and exit
                Console.WriteLine($"An error occured while attempting to get CPU state of device @ {ip}: {ex.Message}. Exiting.");
                return;
            }

            return;
        });

        // Define root command
        RootCommand rootCommand = new RootCommand("Check the CPU status of a device") { 
            Subcommands = { deviceCommand } 
        };

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

    static string generateDeviceOutputString (string ip, string username, string password, bool showCycleTime, bool showCpuInfo, bool showMemoryUsage) {
        StringBuilder outputString = new StringBuilder();
        outputString.AppendLine("////////////////////////////////////////////");
        outputString.AppendLine($"// Connecting to Device @ {ip}...");
        outputString.AppendLine($"// Username: {username}");
        outputString.AppendLine($"// Password: {password}");
        outputString.AppendLine($"// Show Cycle Time: {showCycleTime}");
        outputString.AppendLine($"// Show CPU Info: {showCpuInfo}");
        outputString.AppendLine($"// Show Memory Usage: {showMemoryUsage}");
        outputString.AppendLine("////////////////////////////////////////////");
        return outputString.ToString();
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

//CustomParser = (result) => {
//    // initialize a list to hold the parsed device tuples
//    var devices = new List<(string ip, string? username, string? password)>();


//    // group option tokens by thier parent option instance and parse them into device tuples to add to device list
//    var parent = result.Parent as CommandResult;
//    if (parent == null) {
//        // If the parent is null, return an empty list
//        return devices;
//    }

//    var deviceOptionResults = parent.Children.OfType<OptionResult>().Where(o => o.Option == Option);

//    foreach (OptionResult optRes in result.Parent?.Children.OfType<OptionResult>() ?? Enumerable.Empty<OptionResult>()) {
//        if (optRes.Option != result.Option) continue;

//        var tokens = result.Tokens.Select(t => t.Value).ToList();
//        if (tokens.Count < 1) continue;

//        string ip = (string)tokens[0];
//        string username = tokens.Count > 1 ? (string)tokens[1] : null;
//        string password = tokens.Count > 2 ? (string)tokens[2] : null;

//        devices.Add((ip, username, password));
//    }
//    return devices;
//}

//Option<IEnumerable<(string ip, string username, string password)>> deviceOption = new(name: "--device") {
//    Description = "The <IP, username, password> tuple of the device to connect to. Can be specified multiple times for multiple devices.",
//    //AllowMultipleArgumentsPerToken = true,
//    //Arity = new ArgumentArity(1, 3)
//};
//deviceOption.CustomParser = (result) => {
//    // initialize a list to hold the parsed device tuples
//    var devices = new List<(string ip, string username, string password)>();

//    // group option tokens by thier parent option instance and parse them into device tuples to add to device list
//    var parent = result.Parent as CommandResult;
//    if (parent == null) {
//        // If the parent is null, return an empty list
//        return devices;
//    }
//    foreach (OptionResult optRes in parent.Children.OfType<OptionResult>().Where(o => o.Option == deviceOption)) {
//        var tokens = optRes.Tokens.Select(t => t.Value).ToList();
//        if (tokens.Count < 1) continue;
//        string ip = tokens[0];
//        string username = tokens.Count > 1 ? tokens[1] : "";
//        string password = tokens.Count > 2 ? tokens[2] : "";
//        devices.Add((ip, username, password));
//    }
//    return devices;
//};

//Option<String> ipOption = new("--ip") {
//    Description = "The IP address to connect to.",
//    Required = true
//};
//Option<String> usernameOption = new("--username") {
//    Description = "The username for authentication.",
//    DefaultValueFactory = ParseResult => ""
//};
//Option<string> passwordOption = new("--password") {
//    Description = "The password for authentication.",
//    DefaultValueFactory = ParseResult => ""
//};

//rootCommand.Options.Add(deviceOption);
//rootCommand.Options.Add(ipOption);
//rootCommand.Options.Add(usernameOption);
//rootCommand.Options.Add(passwordOption);