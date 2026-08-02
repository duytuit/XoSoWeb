using Microsoft.Extensions.DependencyInjection;
using XoSoWeb.Lotterys.Repositories;

namespace XoSoWeb.Lotterys
{
    public static class LotteryModule
    {
        public static IServiceCollection AddLotteryModule(this IServiceCollection services)
        {
            services.AddScoped<ILotteryRepositories, LotteryRepositories>();
            return services;
        }
    }
}
