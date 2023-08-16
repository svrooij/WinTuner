﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Parsing;
using System.Reflection;

namespace WingetIntune;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var parser = new CommandLineBuilder(new Commands.WinGetRootCommand())
            .UseHost(_ => Host.CreateDefaultBuilder(),
                           host =>
                           {
                               host.ConfigureAppConfiguration((context, config) =>
                               {
                                   if (AssemblyFolder() != Environment.CurrentDirectory)
                                   {
                                       config.Sources.Insert(0, new JsonConfigurationSource { Path = Path.Combine(AssemblyFolder() + "appsettings.json"), Optional = true, ReloadOnChange = true });
                                   }

                               });
                               host.ConfigureServices(services =>
                               {
                                   services.AddWingetServices();
                               });
                           })
            .UseDefaults()
            .Build();
        return await parser.InvokeAsync(args);
    }

    private static string AssemblyFolder()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyLocation = assembly.Location;
        var assemblyFolder = Path.GetDirectoryName(assemblyLocation);
        return assemblyFolder!;
    }
}
