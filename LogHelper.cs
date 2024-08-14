// -----------------------------------------------------------------------
//  <copyright file="LogHelper.cs" company="Proxus LTD">
//      Copyright (c) Proxus LTD. All rights reserved.
//  </copyright>
//  Solution: Proxus_MQTT_Bench
//  Project: Proxus_MQTT_Bench
//  <author>Volkan Alkılıç</author>
//  <date>03.08.2024</date>
//  -----------------------------------------------------------------------
//  WARNING: This file is licensed to Proxus LTD. Unauthorized copying,
//  modification, distribution or use is strictly prohibited.
//  -----------------------------------------------------------------------

namespace Proxus_MQTT_Bench;

public static class LogHelper
{
    public static void LogInformation(string message)
    {
        Console.WriteLine($"[INFO] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
    }

    public static void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[ERROR] {DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}");
        Console.ResetColor();
    }
}