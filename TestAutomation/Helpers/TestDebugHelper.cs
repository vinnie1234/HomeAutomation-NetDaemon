using Microsoft.Extensions.Configuration;
using NSubstitute;
using NSubstitute.Exceptions;
using NetDaemon.HassModel;
using NetDaemon.HassModel.Entities;

namespace TestAutomation.Helpers;

/// <summary>
/// Helper class for debugging test failures with detailed logging
/// </summary>
public static class TestDebugHelper
{
    private static readonly bool IsDebugEnabled = true; // Always enable for now

    private static bool GetDebugSetting()
    {
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            return configuration.GetValue<bool>("Testing:Debug", false);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Executes an assertion with debug logging for ReceivedCallsException
    /// </summary>
    /// <typeparam name="T">The substitute type</typeparam>
    /// <param name="substitute">The substitute object</param>
    /// <param name="assertion">The assertion to execute</param>
    /// <param name="testName">Name of the test for logging context</param>
    public static void AssertCallWithDebug<T>(T substitute, Action<T> assertion, string testName = "Unknown Test") where T : class
    {
        try
        {
            assertion(substitute);
        }
        catch (ReceivedCallsException ex)
        {
            if (IsDebugEnabled)
            {
                var debugInfo = new System.Text.StringBuilder();
                debugInfo.AppendLine($"========== TEST FAILURE DEBUG INFO ==========");
                debugInfo.AppendLine($"Test: {testName}");
                debugInfo.AppendLine($"Original Exception: {ex.Message}");
                debugInfo.AppendLine($"Substitute Type: {typeof(T).Name}");
                debugInfo.AppendLine();

                try
                {
                    // Use NSubstitute's extension method to get received calls - this works differently for different substitute types
                    var callsList = substitute.ReceivedCalls().ToList();
                    debugInfo.AppendLine($"Totaal ontvangen calls: {callsList.Count}");
                    debugInfo.AppendLine();

                    if (callsList.Any())
                    {
                        debugInfo.AppendLine("Ontvangen calls:");
                        for (int i = 0; i < callsList.Count; i++)
                        {
                            var call = callsList[i];
                            debugInfo.AppendLine($"  [{i + 1}] Method: {call.GetMethodInfo().Name}");
                            
                            var args = call.GetArguments();
                            if (args.Any())
                            {
                                debugInfo.AppendLine($"      Arguments ({args.Length}):");
                                for (int j = 0; j < args.Length; j++)
                                {
                                    var arg = args[j];
                                    var argValue = arg?.ToString() ?? "null";
                                    var argType = arg?.GetType().Name ?? "null";
                                    debugInfo.AppendLine($"        [{j}] {argType}: {argValue}");
                                }
                            }
                            else
                            {
                                debugInfo.AppendLine("      Arguments: None");
                            }
                            debugInfo.AppendLine();
                        }
                    }
                    else
                    {
                        debugInfo.AppendLine("Geen calls ontvangen!");
                    }
                    
                    // Try to get more detailed information if possible
                    if (substitute is IHaContext haContext)
                    {
                        debugInfo.AppendLine("Aanvullende informatie voor IHaContext:");
                        try
                        {
                            // Try to see if we can capture any actual service calls that were made
                            haContext.ReceivedWithAnyArgs().CallService(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ServiceTarget>(), Arg.Any<object>());
                            debugInfo.AppendLine("Er zijn wel CallService calls gemaakt (detectie via ReceivedWithAnyArgs)");
                        }
                        catch (ReceivedCallsException)
                        {
                            debugInfo.AppendLine("Geen CallService calls gedetecteerd");
                        }
                    }
                }
                catch (Exception reflectionEx)
                {
                    debugInfo.AppendLine($"Error trying to get received calls: {reflectionEx.Message}");
                }

                debugInfo.AppendLine($"============================================");
                
                // Throw new exception with debug info
                throw new ReceivedCallsException($"{ex.Message}\n\n{debugInfo}");
            }
            throw;
        }
    }

    /// <summary>
    /// Creates a debug-enabled assertion action for CallService
    /// </summary>
    /// <param name="domain">The service domain</param>
    /// <param name="service">The service name</param>
    /// <param name="target">The service target</param>
    /// <param name="data">The service data</param>
    /// <returns>An assertion action</returns>
    public static Action<IHaContext> CreateCallServiceAssertion(string domain, string service, ServiceTarget? target = null, object? data = null)
    {
        return haContext =>
        {
            if (target == null && data == null)
            {
                haContext.Received().CallService(domain, service, Arg.Any<ServiceTarget>(), Arg.Any<object>());
            }
            else if (target == null)
            {
                haContext.Received().CallService(domain, service, Arg.Any<ServiceTarget>(), data);
            }
            else if (data == null)
            {
                haContext.Received().CallService(domain, service, target, Arg.Any<object>());
            }
            else
            {
                haContext.Received().CallService(domain, service);
            }
        };
    }

    /// <summary>
    /// Creates a debug-enabled assertion action for CallService with entity ID matching
    /// </summary>
    /// <param name="domain">The service domain</param>
    /// <param name="service">The service name</param>
    /// <param name="entityId">The expected entity ID</param>
    /// <returns>An assertion action</returns>
    public static Action<IHaContext> CreateCallServiceEntityAssertion(string domain, string service, string entityId)
    {
        return haContext =>
        {
            haContext.Received().CallService(domain, service,
                Arg.Is<ServiceTarget>(t => t.EntityIds != null && t.EntityIds.Contains(entityId)),
                Arg.Any<object>());
        };
    }

    /// <summary>
    /// Creates a debug-enabled assertion action for verifying call count
    /// </summary>
    /// <param name="domain">The service domain</param>
    /// <param name="service">The service name</param>
    /// <param name="times">Expected number of calls</param>
    /// <returns>An assertion action</returns>
    public static Action<IHaContext> CreateCallCountAssertion(string domain, string service, int times = 1)
    {
        return haContext =>
        {
            haContext.Received(times).CallService(domain, service, Arg.Any<ServiceTarget>(), Arg.Any<object>());
        };
    }
}