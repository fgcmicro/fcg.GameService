using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using fcg.GameService.Domain.Elasticsearch;
using fcg.GameService.Domain.Models;

namespace fcg.GameService.Infrastructure.Elasticsearch;

public class ElasticClient<T>(IElasticSettings settings) : IElasticClient<T>
{
    private ElasticsearchClient? _client;
    private readonly object _lock = new();

    private ElasticsearchClient? GetClient()
    {
        if (_client != null)
            return _client;

        lock (_lock)
        {
            if (_client != null)
                return _client;

            // Valida se as configurações estão disponíveis
            if (string.IsNullOrWhiteSpace(settings.CloudId) || string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                return null;
            }

            try
            {
                _client = new ElasticsearchClient(settings.CloudId, new ApiKey(settings.ApiKey));
                return _client;
            }
            catch
            {
                return null;
            }
        }
    }

    public async Task<IReadOnlyCollection<T>> Get(ElasticLogRequest elasticLogRequest)
    {
        var client = GetClient();
        if (client == null)
        {
            return [];
        }

        SearchRequest request = new(elasticLogRequest.Index.ToLowerInvariant())
        {
            From = elasticLogRequest.Page,
            Size = elasticLogRequest.Size,
            Query = new MatchQuery(elasticLogRequest.Field, elasticLogRequest.Value)
        };

        SearchResponse<T> response = await client.SearchAsync<T>(request);

        if (response.IsValidResponse)
        {
            return response.Documents;
        }

        return [];
    }

    public async Task<bool> AddOrUpdate(T log, string index)
    {
        var client = GetClient();
        if (client == null)
        {
            return false;
        }

        IndexResponse response = await client.IndexAsync(log, x => x.Index(index.ToLowerInvariant()));

        if (response.IsValidResponse)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
