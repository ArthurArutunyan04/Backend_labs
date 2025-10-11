using System.Text;
using Models.Dto.V1.Requests;
using Models.Dto.V1.Responses;
using Common;


public class Client(HttpClient client)
{
    public async Task<V1CreateAuditLogResponse> LogOrder(V1CreateAuditLogRequest request, CancellationToken token)
    {
        var msg = await client.PostAsync("api/v1/audit/log-order", new StringContent(request.ToJson(), Encoding.UTF8, "application/json"), token);
        if (msg.IsSuccessStatusCode)
        {
            var content = await msg.Content.ReadAsStringAsync(cancellationToken: token);
            return content.FromJson<V1CreateAuditLogResponse>();
        }

        throw new HttpRequestException();
    }
}