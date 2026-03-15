namespace Main.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDemeterServices(this IServiceCollection services)
        {
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<SensorDataStateContainer>();

            return services;
        }
    }
}