using WebApp.DAL.Models;

namespace WebApp.DAL.Interfaces
{
    public interface IOrderRepository
    {
        Task<V1OrderDal[]> BulkInsert(V1OrderDal[] model, CancellationToken token);

        Task<V1OrderDal[]> Query(QueryOrdersDalModel model, CancellationToken token);

        Task<int> UpdateStatuses(long[] orderIds, string newStatus, CancellationToken token);
    }
}
