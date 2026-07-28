using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;

using S7CommPlusDriver;
using S7CommPlusDriver.ClientApi;


class Program
{
    static int Main(string[] args)
    {
        Option<String> ipOption = new("--ip") {
            Description = "The IP address to connect to.",
        };

        Option<String> usernameOption = new("--username") {
            Description = "The username for authentication.",
        };

        Option<string> passwordOption = new("--password") {
            Description = "The password for authentication.",
        };

        RootCommand rootCommand = new("A simple command-line application.");
        rootCommand.Options.Add(ipOption);

        ParseResult parseResult = rootCommand.Parse(args);
        if (parseResult.Errors.Count == 0 && parseResult.GetValue(ipOption) is String parsedIP) {
            ConnectToIP(parsedIP);
            return 0;
            
        }
        foreach (ParseError parseError in parseResult.Errors) {
            Console.Error.WriteLine($"Error: {parseError.Message}");
        }
        return 1;

    }

    static void ConnectToIP(string ipAddress)
    {
        // Implement the logic to connect to the specified IP address.
        Console.WriteLine($"Connecting to IP address: {ipAddress}");
        // Add your connection logic here.
    }
}
