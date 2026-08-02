using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XoSoWeb.Lotterys.Dtos;
using XoSoWeb.Lotterys.Models;
using XoSoWeb.Shares;
using XoSoWeb.Shares.BaseRepository;

namespace XoSoWeb.Lotterys.Repositories
{
    public interface ILotteryRepositories : IBaseRepository<Lottery>
    {
        Task<PaginatedResultReact<object>> GetObjectTaskAsync(LotteryDto LotteryDto, int page, int pageSize, CancellationToken cancellationToken);
        Task<Lottery> ShowAsync(int id);
        Task<Lottery> CreateAsync(Lottery Lottery);
        Task<Lottery> UpdateAsync(Lottery Lottery);
        Task<Lottery> DeleteSoftAsync(Lottery Lottery);
    }
}
