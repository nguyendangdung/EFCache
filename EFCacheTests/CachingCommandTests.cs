﻿// Copyright (c) Pawel Kadluczka, Inc. All rights reserved. See License.txt in the project root for license information.

namespace EFCache
{
    using Moq;
    using Moq.Protected;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data;
    using System.Data.Common;
    using System.Data.Entity.Core.Metadata.Edm;
    using System.Diagnostics;
    using System.Linq;
    using Xunit;

    public class CachingCommandTests : TestBase
    {
        [Fact]
        public void CachingCommand_initialized_correctly()
        {
            var command = Mock.Of<DbCommand>();
            var commandTreeFacts = new CommandTreeFacts(null, true, true);
            var transactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object;
            var cachingPolicy = Mock.Of<CachingPolicy>();

            var cachingCommand = new CachingCommand(command, commandTreeFacts, transactionHandler, cachingPolicy);

            Assert.Same(command, cachingCommand.WrappedCommand);
            Assert.Same(commandTreeFacts, cachingCommand.CommandTreeFacts);
            Assert.Same(transactionHandler, cachingCommand.CacheTransactionHandler);
            Assert.Same(cachingPolicy, cachingCommand.CachingPolicy);
        }

        [Fact]
        public void Cancel_invokes_Cancel_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();

            new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(null, true, true),
                new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                Mock.Of<CachingPolicy>()).Cancel();

            mockCommand.Verify(c => c.Cancel(), Times.Once);
        }

        [Fact]
        public void CommandText_invokes_CommandText_getter_and_setter_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand.Setup(c => c.CommandText)
                .Returns("xyz");

            var cachingCommand = 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(null, true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>());

            Assert.Equal("xyz", cachingCommand.CommandText);

            cachingCommand.CommandText = "abc";
            mockCommand.VerifySet(c => c.CommandText = "abc");
        }

        [Fact]
        public void CommandTimeout_invokes_CommandTimeout_getter_and_setter_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand.Setup(c => c.CommandTimeout)
                .Returns(42);

            var cachingCommand = 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(null, true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>());

            Assert.Equal(42, cachingCommand.CommandTimeout);

            cachingCommand.CommandTimeout = 99;
            mockCommand.VerifySet(c => c.CommandTimeout = 99);
        }

        [Fact]
        public void CommandType_invokes_CommandType_getter_and_setter_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand.Setup(c => c.CommandType)
                .Returns(CommandType.StoredProcedure);

            var cachingCommand = 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(null, true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>());

            Assert.Equal(CommandType.StoredProcedure, cachingCommand.CommandType);

            cachingCommand.CommandType = CommandType.TableDirect;
            mockCommand.VerifySet(c => c.CommandType = CommandType.TableDirect);
        }


        [Fact]
        public void DbConnection_invokes_Connection_getter_and_setter_on_wrapped_command()
        {
            var connection = new Mock<DbConnection>().Object;

            var mockCommand = new Mock<DbCommand>();
            mockCommand.Protected()
                .Setup<DbConnection>("DbConnection")
                .Returns(connection);

            var cachingCommand = 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(null, true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>());

            Assert.Same(connection, cachingCommand.Connection);

            cachingCommand.Connection = connection;
            mockCommand.Protected()
                .VerifySet<DbConnection>("DbConnection", Times.Once(), connection);
        }

        [Fact]
        public void DbTransaction_invokes_Transaction_getter_and_setter_on_wrapped_command()
        {
            var transaction = new Mock<DbTransaction>().Object;

            var mockCommand = new Mock<DbCommand>();
            mockCommand.Protected()
                .Setup<DbTransaction>("DbTransaction")
                .Returns(transaction);

            var cachingCommand = 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(null, true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>());

            Assert.Same(transaction, cachingCommand.Transaction);

            cachingCommand.Transaction = transaction;
            mockCommand.Protected() 
                .VerifySet<DbTransaction>("DbTransaction", Times.Once(), transaction);
        }

        [Fact]
        public void UpdatedRowSource_invokes_UpdatedRowSource_getter_and_setter_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand.Setup(c => c.UpdatedRowSource)
                .Returns(UpdateRowSource.FirstReturnedRecord);

            var cachingCommand = 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(null, true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>());

            Assert.Equal(UpdateRowSource.FirstReturnedRecord, cachingCommand.UpdatedRowSource);

            cachingCommand.UpdatedRowSource = UpdateRowSource.OutputParameters;
            mockCommand.VerifySet(c => c.UpdatedRowSource = UpdateRowSource.OutputParameters);
        }

        [Fact]
        public void DbParameterCollection_invokes_Parameters_getter_on_wrapped_command()
        {
            var parameterCollection = new Mock<DbParameterCollection>().Object;

            var mockCommand = new Mock<DbCommand>();
            mockCommand.Protected()
                .Setup<DbParameterCollection>("DbParameterCollection")
                .Returns(parameterCollection);

            var cachingCommand = 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(null, true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>());

            Assert.Same(parameterCollection, cachingCommand.Parameters);
        }

        [Fact]
        public void ExecuteDbDataReader_invokes_ExecuteReader_method_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Protected()
                .Setup<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>())
                .Returns(Mock.Of<DbDataReader>());

            new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(new List<EntitySetBase>().AsReadOnly(), true, true),
                new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                Mock.Of<CachingPolicy>())
                .ExecuteReader(CommandBehavior.SequentialAccess);

            mockCommand
                .Protected()
                .Verify<DbDataReader>(
                    "ExecuteDbDataReader", Times.Once(), CommandBehavior.SequentialAccess);
        }

        [Fact]
        public void ExecuteNonQuery_invokes_ExecuteNonQuery_method_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Setup(c => c.ExecuteNonQuery())
                .Returns(42);

            Assert.Equal(42, 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(new List<EntitySetBase>().AsReadOnly(), true, false),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>()).ExecuteNonQuery());

            mockCommand.Verify(c => c.ExecuteNonQuery(), Times.Once);
        }

        [Fact]
        public void ExecuteScalar_invokes_ExecuteScalar_method_on_wrapped_command()
        {
            var retValue = new object();

            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Setup(c => c.ExecuteScalar())
                .Returns(retValue);

            Assert.Same(retValue, 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(new List<EntitySetBase>().AsReadOnly(), true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>()).ExecuteScalar());

            mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);
        }

        [Fact]
        public void Prepare_invokes_Prepare_method_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();

            new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(null, true, false),
                new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                Mock.Of<CachingPolicy>()).Prepare();

            mockCommand.Verify(c => c.Prepare(), Times.Once);
        }

        [Fact]
        public void CreateDbParameter_invokes_CreateDbParameter_method_on_wrapped_command()
        {
            var dbParam = new Mock<DbParameter>().Object;

            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Protected()
                .Setup<DbParameter>("CreateDbParameter")
                .Returns(dbParam);

            Assert.Same(dbParam, 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(null, true, false),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>()).CreateParameter());
            
            mockCommand
                .Protected()
                .Verify("CreateDbParameter", Times.Once());
        }

        [Fact]
        public void ExecuteDbDataReader_consumes_results_and_creates_CachingReader_if_query_cacheable()
        {
            var mockReader = CreateMockReader(1);
            var mockCommand = 
                CreateMockCommand(reader: mockReader.Object); 
                
            var cachingCommand = 
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(new List<EntitySetBase>().AsReadOnly(), true, false),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    new DefaultCachingPolicy());
            var reader = cachingCommand.ExecuteReader();
            
            Assert.IsType<CachingReader>(reader);
            mockReader
                .Protected()
                .Verify("Dispose", Times.Once(), true);

            Assert.True(reader.Read());
            Assert.Equal("int", reader.GetDataTypeName(0));
            Assert.Equal(typeof(int), reader.GetFieldType(0));
            Assert.Equal("Id", reader.GetName(0));
        }

        [Fact]
        public void ExecuteDbDataReader_does_not_create_CachingReader_if_query_non_cacheable()
        {
            var mockReader = CreateMockReader(1);
            var mockCommand =
                CreateMockCommand(reader: mockReader.Object); 

            var cachingCommand =
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(new List<EntitySetBase>().AsReadOnly(), true, true),
                    new Mock<CacheTransactionHandler>(Mock.Of<ICache>()).Object,
                    Mock.Of<CachingPolicy>());

            using (var reader = cachingCommand.ExecuteReader())
            {
                Assert.IsNotType<CachingReader>(reader);
                mockReader
                    .Protected()
                    .Verify("Dispose", Times.Never(), true);
            }
        }

        [Fact]
        public void Results_cached_for_cacheable_queries()
        {
            var mockReader = CreateMockReader(1);
            var transaction = Mock.Of<DbTransaction>();

            var mockCommand =
                CreateMockCommand(CreateParameterCollection(
                        new[] { "Param1", "Param2" },
                        new object[] { 123, "abc" }), 
                        mockReader.Object,
                        transaction); 

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var slidingExpiration = new TimeSpan(20, 0 ,0);
            var absoluteExpiration = DateTimeOffset.Now.AddMinutes(20);
            var mockCachingPolicy = new Mock<CachingPolicy>();
            mockCachingPolicy
                .Setup(p => p.GetExpirationTimeout(
                            It.IsAny<ReadOnlyCollection<EntitySetBase>>(), 
                            out slidingExpiration, out absoluteExpiration));
            mockCachingPolicy
                .Setup(p => p.CanBeCached(It.IsAny<ReadOnlyCollection<EntitySetBase>>()))
                .Returns(true);

            int minCachableRows = 0, maxCachableRows = int.MaxValue;
            mockCachingPolicy
                .Setup(p => p.GetCacheableRows(It.IsAny<ReadOnlyCollection<EntitySetBase>>(), 
                    out minCachableRows, out maxCachableRows));

            var cachingCommand = new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(
                    CreateEntitySets("ES1", "ES2"), isQuery: true, usesNonDeterministicFunctions: false),
                    mockTransactionHandler.Object,
                    mockCachingPolicy.Object);

            cachingCommand.ExecuteReader();

            mockTransactionHandler.Verify(
                h => h.PutItem(
                    transaction,
                    "Query_Param1=123_Param2=abc",
                    It.Is<CachedResults>(r => r.Results.Count == 1 && r.RecordsAffected == 1 && r.TableMetadata.Length == 1),
                    It.Is<IEnumerable<string>>(es => es.SequenceEqual(new [] { "ES1", "ES2"})),
                    slidingExpiration,
                    absoluteExpiration),
                Times.Once());
        }

        [Fact]
        public void Results_not_cached_for_non_cacheable_queries()
        {
            var reader = new Mock<DbDataReader>().Object;
            var mockCommand = CreateMockCommand(reader: reader);

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var cachingCommand = new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(
                    CreateEntitySets("ES1", "ES2"), isQuery: true, usesNonDeterministicFunctions: true),
                    mockTransactionHandler.Object,
                    Mock.Of<CachingPolicy>());

            using (var r = cachingCommand.ExecuteReader())
            {
                Assert.Same(reader, r);

                object value;

                mockTransactionHandler.Verify(
                    c => c.GetItem(It.IsAny<DbTransaction>(), It.IsAny<string>(), out value),
                    Times.Never());

                mockTransactionHandler.Verify(
                    h => h.PutItem(
                        It.IsAny<DbTransaction>(),
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.IsAny<IEnumerable<string>>(),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<DateTime>()),
                    Times.Never());
            }
        }

        [Fact]
        public void Results_not_cached_if_results_non_cacheable_per_caching_policy()
        {
            var reader = new Mock<DbDataReader>().Object;
            var mockCommand = CreateMockCommand(reader: reader);

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var cachingCommand = new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(
                    CreateEntitySets("ES1", "ES2"), isQuery: true, usesNonDeterministicFunctions: false),
                    mockTransactionHandler.Object,
                    Mock.Of<CachingPolicy>());

            using (var r = cachingCommand.ExecuteReader())
            {
                Assert.Same(reader, r);

                object value;

                mockTransactionHandler.Verify(
                    c => c.GetItem(It.IsAny<DbTransaction>(), It.IsAny<string>(), out value),
                    Times.Never());

                mockTransactionHandler.Verify(
                    h => h.PutItem(
                        It.IsAny<DbTransaction>(),
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.IsAny<IEnumerable<string>>(),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<DateTime>()),
                    Times.Never());
            }
        }

        [Fact]
        public void Results_not_cached_if_too_many_or_to_few_rows()
        {
            var cacheableRowLimits = new[]
            {
                new { MinCacheableRows = 0, MaxCacheableRows = 4 },
                new { MinCacheableRows = 6, MaxCacheableRows = 100 }
            };

            foreach (var cachableRowLimit in cacheableRowLimits)
            {
                var mockCommand = CreateMockCommand(reader: CreateMockReader(5).Object);

                var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

                var minCacheableRows = cachableRowLimit.MinCacheableRows;
                var maxCacheableRows = cachableRowLimit.MaxCacheableRows;

                var mockCachingPolicy = new Mock<CachingPolicy>();
                mockCachingPolicy
                    .Setup(p => p.GetCacheableRows(
                        It.IsAny<ReadOnlyCollection<EntitySetBase>>(),
                        out minCacheableRows, out maxCacheableRows));
                mockCachingPolicy
                    .Setup(p => p.CanBeCached(It.IsAny<ReadOnlyCollection<EntitySetBase>>()))
                    .Returns(true);

                var cachingCommand = new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(
                        CreateEntitySets("ES1", "ES2"), isQuery: true, usesNonDeterministicFunctions: false),
                    mockTransactionHandler.Object,
                    mockCachingPolicy.Object);

                cachingCommand.ExecuteReader();

                mockTransactionHandler.Verify(
                    h => h.PutItem(
                        It.IsAny<DbTransaction>(),
                        It.IsAny<string>(),
                        It.IsAny<CachedResults>(),
                        It.IsAny<IEnumerable<string>>(),
                        It.IsAny<TimeSpan>(),
                        It.IsAny<DateTime>()),
                    Times.Never());
            }
        }

        [Fact]
        public void ExecuteReader_on_wrapped_command_not_invoked_for_cached_results()
        {
            var mockCommand = CreateMockCommand();

            object value = new CachedResults(new ColumnMetadata[0], new List<object[]>(), 42);

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());
            mockTransactionHandler
                .Setup(h => h.GetItem(It.IsAny<DbTransaction>(), It.IsAny<string>(), out value))
                .Returns(true);

            var cachingCommand = new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(new List<EntitySetBase>().AsReadOnly(), true, false),
                mockTransactionHandler.Object,
                new DefaultCachingPolicy());

            using(var reader = cachingCommand.ExecuteReader(CommandBehavior.SequentialAccess))
            {
                Assert.Equal(42, reader.RecordsAffected);
            }

            mockCommand
                .Protected()
                .Verify<DbDataReader>(
                    "ExecuteDbDataReader", Times.Never(), ItExpr.IsAny<CommandBehavior>());
        }

        [Fact]
        public void ExecuteScalar_caches_result_for_cacheable_command()
        {
            var retValue = new object();
            var transaction = Mock.Of<DbTransaction>();

            var mockCommand = 
                CreateMockCommand(
                    CreateParameterCollection(new[] { "P1", "P2" }, new object[] { "ZZZ", 123 }),
                    transaction: transaction);

            mockCommand
                .Setup(c => c.ExecuteScalar())
                .Returns(retValue);
            mockCommand
                .Setup(c => c.CommandText)
                .Returns("Exec");

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var slidingExpiration = new TimeSpan(20, 0, 0);
            var absoluteExpiration = DateTimeOffset.Now.AddMinutes(20);
            var mockCachingPolicy = new Mock<CachingPolicy>();
            mockCachingPolicy
                .Setup(p => p.GetExpirationTimeout(
                            It.IsAny<ReadOnlyCollection<EntitySetBase>>(),
                            out slidingExpiration, out absoluteExpiration));
            mockCachingPolicy
                .Setup(p => p.CanBeCached(It.IsAny<ReadOnlyCollection<EntitySetBase>>()))
                .Returns(true);

            var result =
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(CreateEntitySets("ES1", "ES2"), true, false),
                    mockTransactionHandler.Object,
                    mockCachingPolicy.Object).ExecuteScalar();

            Assert.Same(retValue, result);
            object value;
            mockTransactionHandler.Verify(
                h => h.GetItem(transaction, "Exec_P1=ZZZ_P2=123", out value), Times.Once);
 
            mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);

            mockTransactionHandler.Verify(
                h => h.PutItem(
                    transaction,
                    "Exec_P1=ZZZ_P2=123", 
                    retValue, 
                    new[] {"ES1", "ES2"}, 
                    slidingExpiration, 
                    absoluteExpiration), 
                    Times.Once);
        }

        [Fact]
        public void ExecuteScalar_returns_cached_result_if_exists()
        {
            var retValue = new object();

            var transaction = Mock.Of<DbTransaction>();

            var mockCommand =
                CreateMockCommand(
                    CreateParameterCollection(new[] { "P1", "P2" }, new object[] { "ZZZ", 123 }),
                    transaction: transaction);

            mockCommand
                .Setup(c => c.ExecuteScalar())
                .Returns(retValue);
            mockCommand
                .Setup(c => c.CommandText)
                .Returns("Exec");

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());
            mockTransactionHandler
                .Setup(h => h.GetItem(transaction, "Exec_P1=ZZZ_P2=123", out retValue))
                .Returns(true);

            var result =
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(CreateEntitySets("ES1", "ES2"), true, false),
                    mockTransactionHandler.Object,
                    new DefaultCachingPolicy()).ExecuteScalar();

            Assert.Same(retValue, result);
            object value;
            mockTransactionHandler.Verify(
                h => h.GetItem(transaction, "Exec_P1=ZZZ_P2=123", out value), Times.Once);

            mockCommand.Verify(h => h.ExecuteScalar(), Times.Never);

            mockTransactionHandler.Verify(
                h => h.PutItem(
                    It.IsAny<DbTransaction>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<DateTime>()),
                    Times.Never);
        }

        [Fact]
        public void ExecuteScalar_does_not_cache_results_for_non_cacheable_queries()
        {
            var retValue = new object();

            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Setup(c => c.ExecuteScalar())
                .Returns(retValue);

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var result =
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(CreateEntitySets("ES1", "ES2"), true, true),
                    mockTransactionHandler.Object,
                    Mock.Of<CachingPolicy>()).ExecuteScalar();

            Assert.Same(retValue, result);
            object value;
            mockTransactionHandler.Verify(
                h => h.GetItem(It.IsAny<DbTransaction>(), It.IsAny<string>(), out value), Times.Never);

            mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);

            mockTransactionHandler.Verify(
                h => h.PutItem(
                    It.IsAny<DbTransaction>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<DateTime>()),
                    Times.Never);
        }

        [Fact]
        public void ExecuteScalar_does_not_cache_results_if_non_cacheable_per_CachingPolicy()
        {
            var retValue = new object();

            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Setup(c => c.ExecuteScalar())
                .Returns(retValue);
            mockCommand
                .Setup(c => c.CommandText)
                .Returns("Exec");
            mockCommand
                .Protected()
                .Setup<DbParameterCollection>("DbParameterCollection")
                .Returns(CreateParameterCollection(new[] { "P1", "P2" }, new object[] { "ZZZ", 123 }));

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var result =
                new CachingCommand(
                    mockCommand.Object,
                    new CommandTreeFacts(CreateEntitySets("ES1", "ES2"), true, false),
                    mockTransactionHandler.Object,
                    Mock.Of<CachingPolicy>()).ExecuteScalar();

            Assert.Same(retValue, result);
            object value;
            mockTransactionHandler.Verify(
                h => h.GetItem(It.IsAny<DbTransaction>(), It.IsAny<string>(), out value), Times.Never);

            mockCommand.Verify(c => c.ExecuteScalar(), Times.Once);

            mockTransactionHandler.Verify(
                h => h.PutItem(
                    It.IsAny<DbTransaction>(),
                    It.IsAny<string>(),
                    It.IsAny<object>(),
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<DateTime>()),
                    Times.Never);
        }


        [Fact]
        public void ExecuteNonQuery_invalidates_cache_for_given_entity_sets_if_any_affected_records()
        {
            var transaction = Mock.Of<DbTransaction>();
            var mockCommand = CreateMockCommand(transaction: transaction);
            mockCommand
                .Setup(c => c.ExecuteNonQuery())
                .Returns(1);

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var rowsAffected = new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(CreateEntitySets("ES1", "ES2"), true, true),
                mockTransactionHandler.Object,
                Mock.Of<CachingPolicy>()).ExecuteNonQuery();

            Assert.Equal(rowsAffected, 1);
            mockCommand.Verify(c => c.ExecuteNonQuery(), Times.Once());
            mockTransactionHandler
                .Verify(h => h.InvalidateSets(transaction, new[] { "ES1", "ES2" }), Times.Once());
        }

        [Fact]
        public void ExecuteNonQuery_does_not_invalidate_cache_if_no_records_affected()
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Setup(c => c.ExecuteNonQuery())
                .Returns(0);

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var rowsAffected = new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(CreateEntitySets("ES1", "ES2"), true, true),
                mockTransactionHandler.Object,
                Mock.Of<CachingPolicy>()).ExecuteNonQuery();

            Assert.Equal(rowsAffected, 0);
            mockCommand.Verify(c => c.ExecuteNonQuery(), Times.Once());
            mockTransactionHandler
                .Verify(h => h.InvalidateSets(It.IsAny<DbTransaction>(), It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Fact]
        public void ExecuteNonQuery_does_not_invalidate_cache_if_no_entitysets_affected()
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Setup(c => c.ExecuteNonQuery())
                .Returns(1);

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            var rowsAffected = new CachingCommand(
                mockCommand.Object,
                new CommandTreeFacts(new List<EntitySetBase>().AsReadOnly(), true, true),
                mockTransactionHandler.Object,
                Mock.Of<CachingPolicy>()).ExecuteNonQuery();

            Assert.Equal(rowsAffected, 1);
            mockCommand.Verify(c => c.ExecuteNonQuery(), Times.Once());
            mockTransactionHandler
                .Verify(h => h.InvalidateSets(It.IsAny<DbTransaction>(), It.IsAny<IEnumerable<string>>()), Times.Never());
        }

        [Fact]
        public void Dispose_invokes_Dispose_on_wrapped_command()
        {
            var mockCommand = new Mock<DbCommand>();

            var mockTransactionHandler = new Mock<CacheTransactionHandler>(Mock.Of<ICache>());

            new CachingCommand(mockCommand.Object,
                new CommandTreeFacts(new List<EntitySetBase>().AsReadOnly(), true, true),
                mockTransactionHandler.Object,
                Mock.Of<CachingPolicy>()).Dispose();

            mockCommand.Protected().Verify("Dispose", Times.Once(), true);
        }

        private static Mock<DbCommand> CreateMockCommand(DbParameterCollection parameterCollection = null, DbDataReader reader = null, DbTransaction transaction = null)
        {
            var mockCommand = new Mock<DbCommand>();
            mockCommand
                .Setup(c => c.CommandText)
                .Returns("Query");

            mockCommand
                .Protected()
                .Setup<DbParameterCollection>("DbParameterCollection")
                .Returns(parameterCollection ?? CreateParameterCollection(new string[0], new object[0]));

            if (reader != null)
            {
                mockCommand
                    .Protected()
                    .Setup<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>())
                    .Returns(reader);
            }

            mockCommand
                .Protected()
                .Setup<DbTransaction>("DbTransaction")
                .Returns(transaction ?? Mock.Of<DbTransaction>());

            return mockCommand;
        }

        private static Mock<DbDataReader> CreateMockReader(int resultCount)
        {
            var mockReader = new Mock<DbDataReader>();
            mockReader
                .Setup(r => r.FieldCount)
                .Returns(1);
            mockReader
                .Setup(r => r.GetDataTypeName(0))
                .Returns("int");
            mockReader
                .Setup(r => r.GetFieldType(0))
                .Returns(typeof(int));
            mockReader
                .Setup(r => r.GetName(0))
                .Returns("Id");
            mockReader
                .Setup(r => r.FieldCount)
                .Returns(1);
            mockReader
                .Setup(r => r.RecordsAffected)
                .Returns(resultCount);

            mockReader
                .Setup(r => r.GetValues(It.IsAny<object[]>()))
                .Callback((object[] values) => values[0] = 1);

            mockReader
                .Setup(r => r.Read())
                .Returns(() => resultCount-- > 0);

            return mockReader;
        }

        private static DbParameterCollection CreateParameterCollection(string[] names, object[] values)
        {
            Debug.Assert(names.Length == values.Length, "names.Length is not equal values.Length");

            var parameters = new List<DbParameter>(names.Length);

            for (var i = 0; i < names.Length; i++)
            {
                var mockParameter = new Mock<DbParameter>();
                mockParameter
                    .Setup(p => p.ParameterName)
                    .Returns(names[i]);
                mockParameter
                    .Setup(p => p.Value)
                    .Returns(values[i]);

                parameters.Add(mockParameter.Object);
            }

            var mockParameterCollection = new Mock<DbParameterCollection>();
            mockParameterCollection.As<IEnumerable>();
            mockParameterCollection
                .Setup(c => c.GetEnumerator())
                .Returns(parameters.GetEnumerator());

            return mockParameterCollection.Object;
        }

        private static ReadOnlyCollection<EntitySetBase> CreateEntitySets(params string[] setNames)
        {
            var entitySets = new List<EntitySetBase>();

            foreach (var setName in setNames)
            {
                var entityType =
                    EntityType.Create(setName + "EntityType", "ns", DataSpace.CSpace,
                    new string[0], new EdmMember[0], null);

                entitySets.Add(EntitySet.Create(setName, "ns", null, null, entityType, null));
            }

            return entitySets.AsReadOnly();
        }
    }
}
