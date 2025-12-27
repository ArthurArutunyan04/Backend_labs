using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;
using WebApp.DAL.Interfaces;
using WebApp.DAL.Models;
using WebApp.DAL.Repositories;

namespace WebApp.DAL
{
    public class UnitOfWork(IOptions<DbSettings> dbSettings) : IDisposable
    {
        private NpgsqlConnection _connection;

        private static NpgsqlDataSource? _dataSource;
        private static readonly object _dataSourceLock = new object();
        private static bool _typesLoaded = false;
        private static readonly object _typesLoadLock = new object();

        public async Task<NpgsqlConnection> GetConnection(CancellationToken token)
        {
            if (_connection is not null && _connection.State == ConnectionState.Open)
            {
                return _connection;
            }

            // Загружаем типы один раз через прямое подключение к postgres (не через pgbouncer)
            if (!_typesLoaded)
            {
                LoadTypesOnce(dbSettings.Value.MigrationConnectionString);
            }

            // Создаём DataSource один раз (thread-safe)
            if (_dataSource == null)
            {
                lock (_dataSourceLock)
                {
                    if (_dataSource == null)
                    {
                        var dataSourceBuilder = new NpgsqlDataSourceBuilder(dbSettings.Value.ConnectionString);
                        // MapComposite требует, чтобы типы были уже загружены
                        if (_typesLoaded)
                        {
                            dataSourceBuilder.MapComposite<V1OrderDal>("v1_order");
                            dataSourceBuilder.MapComposite<V1OrderItemDal>("v1_order_item");
                        }
                        _dataSource = dataSourceBuilder.Build();
                    }
                }
            }

            _connection = _dataSource.CreateConnection();
            _connection.StateChange += (sender, args) =>
            {
                if (args.CurrentState == ConnectionState.Closed)
                    _connection = null;
            };

            await _connection.OpenAsync(token);

            return _connection;
        }

        public async ValueTask<NpgsqlTransaction> BeginTransactionAsync(CancellationToken token)
        {
            _connection ??= await GetConnection(token);
            return await _connection.BeginTransactionAsync(token);
        }

        public void Dispose()
        {
            DisposeConnection();
            GC.SuppressFinalize(this);
        }

        ~UnitOfWork()
        {
            DisposeConnection();
        }

        private void DisposeConnection()
        {
            _connection?.Dispose();
            _connection = null;
        }

        private static void LoadTypesOnce(string migrationConnectionString)
        {
            if (_typesLoaded || string.IsNullOrEmpty(migrationConnectionString))
                return;

            lock (_typesLoadLock)
            {
                if (_typesLoaded)
                    return;

                try
                {
                    // Используем синхронный вызов внутри lock для загрузки типов
                    // Это необходимо, так как lock не может быть использован с await
                    using var initConnection = new NpgsqlConnection(migrationConnectionString);
                    initConnection.Open();
                    initConnection.ReloadTypes();
                    _typesLoaded = true;
                }
                catch
                {
                    // Если не удалось загрузить типы, продолжаем без них
                    _typesLoaded = true;
                }
            }
        }

        private IAuditLogOrderRepository? _auditLogOrderRepository;
        public IAuditLogOrderRepository AuditLogOrderRepository =>
            _auditLogOrderRepository ??= new AuditLogOrderRepository(this);
    }
}
