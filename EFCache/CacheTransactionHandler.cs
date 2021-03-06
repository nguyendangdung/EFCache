﻿// Copyright (c) Pawel Kadluczka, Inc. All rights reserved. See License.txt in the project root for license information.

namespace EFCache
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Data.Entity.Infrastructure.Interception;
    using System.Linq;

    public class CacheTransactionHandler : IDbTransactionInterceptor
    {
        private readonly ConcurrentDictionary<DbTransaction, List<string>> _affectedSetsInTransaction 
            = new ConcurrentDictionary<DbTransaction, List<string>>();
        private readonly ICache _cache;

        public CacheTransactionHandler(ICache cache)
        {
            if (cache == null)
            {
                throw new ArgumentNullException("cache");
            }

            _cache = cache;
        }

        public virtual bool GetItem(DbTransaction transaction, string key, DbInfo db, out object value)
        {
            if (transaction == null)
            {
                return _cache.GetItem(key, out value, db);
            }

            value = null;

            return false;
        }

        public virtual void PutItem(DbTransaction transaction, string key, object value,
            IEnumerable<string> dependentEntitySets, TimeSpan slidingExpiration,
            DateTimeOffset absoluteExpiration, DbInfo db)
        {
            if (transaction == null)
            {
                _cache.PutItem(key, value, dependentEntitySets, slidingExpiration, absoluteExpiration, db);
            }
        }

        public virtual void InvalidateSets(DbTransaction transaction, IEnumerable<string> entitySets, DbInfo db)
        {
            if (transaction == null)
            {
                _cache.InvalidateSets(entitySets, db);
            }
            else
            {
                AddAffectedEntitySets(transaction, entitySets);
            }
        }

        protected void AddAffectedEntitySets(DbTransaction transaction, IEnumerable<string> affectedEntitySets)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException("transaction");
            }

            if (affectedEntitySets == null)
            {
                throw new ArgumentNullException("affectedEntitySets");
            }

            var entitySets = _affectedSetsInTransaction.GetOrAdd(transaction, new List<string>());
            entitySets.AddRange(affectedEntitySets);
        }

        private IEnumerable<string> RemoveAffectedEntitySets(DbTransaction transaction)
        {
            List<string> affectedEntitySets;

            _affectedSetsInTransaction.TryRemove(transaction, out affectedEntitySets);

            return affectedEntitySets;
        }

        public void Committed(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
            var entitySets = RemoveAffectedEntitySets(transaction);

            if (entitySets != null)
            {
                _cache.InvalidateSets(entitySets.Distinct(), new DbInfo(interceptionContext.Connection.Database));
            }
        }

        public void Committing(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
        }

        public void ConnectionGetting(DbTransaction transaction, DbTransactionInterceptionContext<DbConnection> interceptionContext)
        {
        }

        public void ConnectionGot(DbTransaction transaction, DbTransactionInterceptionContext<DbConnection> interceptionContext)
        {
        }

        public void Disposed(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
        }

        public void Disposing(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
        }

        public void IsolationLevelGetting(DbTransaction transaction, DbTransactionInterceptionContext<IsolationLevel> interceptionContext)
        {
        }

        public void IsolationLevelGot(DbTransaction transaction, DbTransactionInterceptionContext<IsolationLevel> interceptionContext)
        {
        }

        public void RolledBack(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
            RemoveAffectedEntitySets(transaction);
        }

        public void RollingBack(DbTransaction transaction, DbTransactionInterceptionContext interceptionContext)
        {
        }
    }
}
