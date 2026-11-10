using System.Reflection;
using Automation.apps;
using Automation.Configuration;
using Automation.Helpers;
using Automation.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetDaemon.AppModel;
using NSubstitute;

namespace TestAutomation.Helpers;

public static class Init
{
    extension(AppTestContext ctx)
    {
        public T InitApp<T>(params object[] additionalParams) where T : BaseApp
        {
            var logger = Substitute.For<ILogger<T>>();
            return ctx.Create<T>([ctx.HaContext, logger, ctx.Notify, ctx.Scheduler, ..additionalParams]);
        }

        public T InitAppWithStorage<T>(params object[] additionalParams) where T : BaseApp
        {
            var logger = Substitute.For<ILogger<T>>();
            var dataRepository = Substitute.For<IDataRepository>();
            return ctx.Create<T>([ctx.HaContext, logger, ctx.Notify, ctx.Scheduler, dataRepository, ..additionalParams]);
        }

        public async Task<T> InitAppAsync<T>(params object[] additionalParams) where T : BaseApp, IAsyncInitializable
        {
            var app = ctx.InitApp<T>(additionalParams);
            await app.InitializeAsync(CancellationToken.None);
            return app;
        }

        /// <summary>
        /// Creates the app under test. Constructor parameters the test did not supply are filled in
        /// automatically, so adding a new service to an app does not break every existing test.
        /// </summary>
        private T Create<T>(IEnumerable<object> suppliedParams) where T : BaseApp
        {
            var parameters = suppliedParams.ToList();
            var constructor = typeof(T)
                .GetConstructors()
                .OrderByDescending(x => x.GetParameters().Length)
                .First();

            foreach (var parameter in constructor.GetParameters().Skip(parameters.Count))
                parameters.Add(ctx.CreateParameter(parameter));

            return (T)constructor.Invoke([.. parameters]);
        }

        private object CreateParameter(ParameterInfo parameter)
        {
            var type = parameter.ParameterType;

            if (type == typeof(ICircadianLightingService)) return ctx.CircadianLightingService;
            if (type == typeof(ILivingRoomPresenceService)) return ctx.LivingRoomPresenceService;
            if (type == typeof(ISpotcast)) return ctx.Spotcast;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptions<>)) return CreateOptions(ctx, type);
            if (type.IsInterface) return Substitute.For([type], []);

            throw new InvalidOperationException(
                $"Cannot auto-create constructor parameter '{parameter.Name}' of type {type.Name}; pass it to InitApp explicitly.");
        }
    }

    private static object CreateOptions(AppTestContext ctx, Type optionsType)
    {
        var valueType = optionsType.GetGenericArguments()[0];
        var value = valueType == typeof(AppConfiguration)
            ? ctx.Config
            : Activator.CreateInstance(valueType)
              ?? throw new InvalidOperationException($"Cannot create default options for {valueType.Name}");

        return typeof(Options)
            .GetMethod(nameof(Options.Create))!
            .MakeGenericMethod(valueType)
            .Invoke(null, [value])!;
    }
}
