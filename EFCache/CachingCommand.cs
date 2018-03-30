// Copyright (c) Pawel Kadluczka, Inc. All rights reserved. See License.txt in the project root for license information.

namespace EFCache
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    internal class CachingCommand : DbCommand, ICloneable
    {
        private readonly CachingPolicy _cachingPolicy;

        public CachingCommand(DbCommand command, CommandTreeFacts commandTreeFacts, CacheTransactionHandler cacheTransactionHandler, CachingPolicy cachingPolicy)
        {
            Debug.Assert(command != null, "command is null");
            Debug.Assert(commandTreeFacts != null, "commandTreeFacts is null");
            Debug.Assert(cacheTransactionHandler != null, "cacheTransactionHandler is null");
            Debug.Assert(cachingPolicy != null, "cachingPolicy is null");

            WrappedCommand = command;
            CommandTreeFacts = commandTreeFacts;
            CacheTransactionHandler = cacheTransactionHandler;
            _cachingPolicy = cachingPolicy;
        }

        internal CommandTreeFacts CommandTreeFacts { get; }

        internal CacheTransactionHandler CacheTransactionHandler { get; }

        internal CachingPolicy CachingPolicy => _cachingPolicy;

        internal DbCommand WrappedCommand { get; }

        private bool IsCacheable
        {
            get
            {
                return CommandTreeFacts.IsQuery &&
                       (IsQueryAlwaysCached ||
                       !CommandTreeFacts.UsesNonDeterministicFunctions &&
                       !IsQueryBlacklisted &&
                       _cachingPolicy.CanBeCached(CommandTreeFacts.AffectedEntitySets, CommandText,
                           Parameters.Cast<DbParameter>()
                               .Select(p => new KeyValuePair<string, object>(p.ParameterName, p.Value))));
            }
        }

        private bool IsQueryBlacklisted => BlacklistedQueriesRegistrar.Instance.IsQueryBlacklisted(
            CommandTreeFacts.MetadataWorkspace, CommandText);

        private bool IsQueryAlwaysCached => AlwaysCachedQueriesRegistrar.Instance.IsQueryCached(
            CommandTreeFacts.MetadataWorkspace, CommandText);

        public override void Cancel()
        {
            WrappedCommand.Cancel();
        }

        public override string CommandText
        {
            get => WrappedCommand.CommandText;
            set => WrappedCommand.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => WrappedCommand.CommandTimeout;
            set => WrappedCommand.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => WrappedCommand.CommandType;
            set => WrappedCommand.CommandType = value;
        }

        protected override DbParameter CreateDbParameter()
        {
            return WrappedCommand.CreateParameter();
        }

        protected override DbConnection DbConnection
        {
            get => WrappedCommand.Connection;
            set => WrappedCommand.Connection = value;
        }

        protected override DbParameterCollection DbParameterCollection => WrappedCommand.Parameters;

        protected override DbTransaction DbTransaction
        {
            get => WrappedCommand.Transaction;
            set => WrappedCommand.Transaction = value;
        }

        public override bool DesignTimeVisible
        {
            get => WrappedCommand.DesignTimeVisible;
            set => WrappedCommand.DesignTimeVisible = value;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (!IsCacheable)
            {
                var result = WrappedCommand.ExecuteReader(behavior);

                if (!CommandTreeFacts.IsQuery)
                {
                    CacheTransactionHandler.InvalidateSets(Transaction, CommandTreeFacts.AffectedEntitySets.Select(s => s.Name));
                }

                return result;
            }

            var key = CreateKey();

            if (CacheTransactionHandler.GetItem(Transaction, key, out var value))
            {
                return new CachingReader((CachedResults)value);
            }

            using (var reader = WrappedCommand.ExecuteReader(behavior))
            {
                var queryResults = new List<object[]>();

                while (reader.Read())
                {
                    var values = new object[reader.FieldCount];
                    reader.GetValues(values);
                    queryResults.Add(values);
                }

                return HandleCaching(reader, key, queryResults);
            }
        }

#if !NET40
        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            if (!IsCacheable)
            {
                var result = await WrappedCommand.ExecuteReaderAsync(behavior, cancellationToken);

                if (!CommandTreeFacts.IsQuery)
                {
                    CacheTransactionHandler.InvalidateSets(Transaction, CommandTreeFacts.AffectedEntitySets.Select(s => s.Name));
                }

                return result;
            }

            var key = CreateKey();

            if (CacheTransactionHandler.GetItem(Transaction, key, out var value))
            {
                return new CachingReader((CachedResults)value);
            }

            using (var reader = await WrappedCommand.ExecuteReaderAsync(behavior, cancellationToken))
            {
                var queryResults = new List<object[]>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    var values = new object[reader.FieldCount];
                    reader.GetValues(values);
                    queryResults.Add(values);
                }

                return HandleCaching(reader, key, queryResults);
            }
        }
#endif

        private DbDataReader HandleCaching(DbDataReader reader, string key, List<object[]> queryResults)
        {
            var cachedResults =
                new CachedResults(
                    GetTableMetadata(reader), queryResults, reader.RecordsAffected);
            _cachingPolicy.GetCacheableRows(CommandTreeFacts.AffectedEntitySets, out int minCacheableRows,
                out int maxCachableRows);

            if (IsQueryAlwaysCached || (queryResults.Count >= minCacheableRows && queryResults.Count <= maxCachableRows))
            {
                _cachingPolicy.GetExpirationTimeout(CommandTreeFacts.AffectedEntitySets, out TimeSpan slidingExpiration,
                    out DateTimeOffset absoluteExpiration);

                CacheTransactionHandler.PutItem(
                    Transaction,
                    key,
                    cachedResults,
                    CommandTreeFacts.AffectedEntitySets.Select(s => s.Name),
                    slidingExpiration,
                    absoluteExpiration);
            }

            return new CachingReader(cachedResults);
        }

        protected override void Dispose(bool disposing)
        {
            WrappedCommand.GetType()
                .GetMethod("Dispose", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(WrappedCommand, new object[] { disposing });
        }

        private static ColumnMetadata[] GetTableMetadata(DbDataReader reader)
        {
            var columnMetadata = new ColumnMetadata[reader.FieldCount];

            for (var i = 0; i < reader.FieldCount; i++)
            {
                columnMetadata[i] =
                    new ColumnMetadata(
                        reader.GetName(i), reader.GetDataTypeName(i), reader.GetFieldType(i));
            }

            return columnMetadata;
        }

        public override int ExecuteNonQuery()
        {
            var recordsAffected = WrappedCommand.ExecuteNonQuery();

            InvalidateSetsForNonQuery(recordsAffected);

            return recordsAffected;
        }

#if !NET40
        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            var recordsAffected = await WrappedCommand.ExecuteNonQueryAsync(cancellationToken);

            InvalidateSetsForNonQuery(recordsAffected);

            return recordsAffected;
        }
#endif

        private void InvalidateSetsForNonQuery(int recordsAffected)
        {
            if (recordsAffected > 0 && CommandTreeFacts.AffectedEntitySets.Any())
            {
                CacheTransactionHandler.InvalidateSets(Transaction, CommandTreeFacts.AffectedEntitySets.Select(s => s.Name));
            }
        }

        public override object ExecuteScalar()
        {
            if (!IsCacheable)
            {
                return WrappedCommand.ExecuteScalar();
            }

            var key = CreateKey();


            if (CacheTransactionHandler.GetItem(Transaction, key, out object value))
            {
                return value;
            }

            value = WrappedCommand.ExecuteScalar();
            _cachingPolicy.GetExpirationTimeout(CommandTreeFacts.AffectedEntitySets, out TimeSpan slidingExpiration, out DateTimeOffset absoluteExpiration);

            CacheTransactionHandler.PutItem(
                Transaction,
                key,
                value,
                CommandTreeFacts.AffectedEntitySets.Select(s => s.Name),
                slidingExpiration,
                absoluteExpiration);

            return value;
        }

#if !NET40
        public override async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            if (!IsCacheable)
            {
                return await WrappedCommand.ExecuteScalarAsync(cancellationToken);
            }

            var key = CreateKey();


            if (CacheTransactionHandler.GetItem(Transaction, key, out object value))
            {
                return value;
            }

            value = await WrappedCommand.ExecuteScalarAsync(cancellationToken);
            _cachingPolicy.GetExpirationTimeout(CommandTreeFacts.AffectedEntitySets, out TimeSpan slidingExpiration, out DateTimeOffset absoluteExpiration);

            CacheTransactionHandler.PutItem(
                Transaction,
                key,
                value,
                CommandTreeFacts.AffectedEntitySets.Select(s => s.Name),
                slidingExpiration,
                absoluteExpiration);

            return value;
        }
#endif

        public override void Prepare()
        {
            WrappedCommand.Prepare();
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => WrappedCommand.UpdatedRowSource;
            set => WrappedCommand.UpdatedRowSource = value;
        }

        private string CreateKey()
        {
            return
                $"{Connection.Database}_{CommandText}_{string.Join("_", Parameters.Cast<DbParameter>().Select(p => $"{p.ParameterName}={p.Value}"))}";
        }

        public object Clone()
        {
            if (!(WrappedCommand is ICloneable cloneableCommand))
            {
                throw new InvalidOperationException("The underlying DbCommand does not implement the ICloneable interface.");
            }

            var clonedCommand = (DbCommand)cloneableCommand.Clone();
            return new CachingCommand(clonedCommand, CommandTreeFacts, CacheTransactionHandler, _cachingPolicy);
        }
    }
}
