﻿using System;
using SqlBulkHelpers.Tests;

namespace SqlBulkHelpers.IntegrationTests
{
    [TestClass]
    public class SchemaLoaderCacheTests : BaseTest
    {
        [TestMethod]
        public void TestSchemaLoaderCacheWithLazyLoadingFromMultipleConnectionProviders()
        {
            SqlBulkHelpersSchemaLoaderCache.ClearCache();
            ISqlBulkHelpersConnectionProvider sqlConnectionProvider = SqlConnectionHelper.GetConnectionProvider();

            List<ISqlBulkHelpersDBSchemaLoader> schemaLoadersList = new List<ISqlBulkHelpersDBSchemaLoader>
            {
                SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader(sqlConnectionProvider), //0
                SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader(sqlConnectionProvider), //1
                SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader(sqlConnectionProvider), //2
                SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader("SECOND_CONNECTION_TEST"), //3
            };

            Assert.IsNotNull(schemaLoadersList[0]);
            Assert.IsNotNull(schemaLoadersList[1]);
            Assert.IsNotNull(schemaLoadersList[2]);
            Assert.IsNotNull(schemaLoadersList[3]);

            Assert.AreEqual(schemaLoadersList[0], schemaLoadersList[1]);
            Assert.AreEqual(schemaLoadersList[1], schemaLoadersList[2]);
            Assert.AreEqual(schemaLoadersList[0], schemaLoadersList[2]);
            Assert.AreNotEqual(schemaLoadersList[2], schemaLoadersList[3]);

            //Validate that the second connection was never initialized!
            var secondConnectionSchemaLoader = (SqlBulkHelpersDBSchemaLoader)schemaLoadersList[3];
            Assert.IsNotNull(secondConnectionSchemaLoader);

            Assert.AreEqual(2, SqlBulkHelpersSchemaLoaderCache.Count);
        }

        [TestMethod]
        public async Task TestSchemaLoaderCacheWithExistingConnectionAsync()
        {
            SqlBulkHelpersSchemaLoaderCache.ClearCache();
            ISqlBulkHelpersConnectionProvider sqlConnectionProvider = SqlConnectionHelper.GetConnectionProvider();

            List<ISqlBulkHelpersDBSchemaLoader> schemaLoadersList = new List<ISqlBulkHelpersDBSchemaLoader>();

            using (var conn = await sqlConnectionProvider.NewConnectionAsync())
            {
                schemaLoadersList.Add(SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader(conn.ConnectionString));
            }

            using (var conn = await sqlConnectionProvider.NewConnectionAsync())
            {
                schemaLoadersList.Add(SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader(conn.ConnectionString));
            }

            using (var conn = await sqlConnectionProvider.NewConnectionAsync())
            {
                schemaLoadersList.Add(SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader(conn.ConnectionString));
            }

            Assert.IsNotNull(schemaLoadersList[0]);
            Assert.IsNotNull(schemaLoadersList[1]);
            Assert.IsNotNull(schemaLoadersList[2]);

            Assert.AreEqual(schemaLoadersList[0], schemaLoadersList[1]);
            Assert.AreEqual(schemaLoadersList[1], schemaLoadersList[2]);
            Assert.AreEqual(schemaLoadersList[0], schemaLoadersList[2]);

            Assert.AreEqual(1, SqlBulkHelpersSchemaLoaderCache.Count);
        }

        [TestMethod]
        public async Task TestSchemaLoaderCacheWithBadConnectionDueToPendingTransactionFor_v1_2()
        {
            //****************************************************************************************************
            // Check that Invalid Connection Fails as expected and Lazy continues to re-throw the Exception!!!
            //****************************************************************************************************
            await using var sqlConnInvalidWithTransaction = await SqlConnectionHelper.NewConnectionAsync().ConfigureAwait(false);
            //START a Pending Transaction which will NOT be available to the DBSchemaLoader (as of v1.2)!
            await using var sqlTrans = await sqlConnInvalidWithTransaction.BeginTransactionAsync().ConfigureAwait(false);

            var dbSchemaLoaderFromFactoryFuncInvalid = SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader($"SQL_INVALID_CONNECTION_CACHE_KEY::{Guid.NewGuid()}");

            Assert.IsNotNull(dbSchemaLoaderFromFactoryFuncInvalid);

            List<Exception> exceptions = new();
            var loopCount = 3;

            for (int x = 0; x < loopCount; x++)
            {
                try
                {
                    //Initial Call should result in SQL Exception due to Pending Transaction...
                    var tableDefinition = dbSchemaLoaderFromFactoryFuncInvalid.GetTableSchemaDefinition(
                        TestHelpers.TestTableNameFullyQualified, 
                        TableSchemaDetailLevel.ExtendedDetails,
                        sqlConnection: sqlConnInvalidWithTransaction
                    );
                    Assert.IsNotNull(tableDefinition);
                }
                catch (Exception exc)
                {
                    exceptions.Add(exc);
                }
            }

            var firstExc = exceptions.FirstOrDefault();
            Assert.AreEqual(loopCount, exceptions.Count);
            Assert.IsTrue(exceptions.TrueForAll(
                //Assert that ALL Exceptions (HResult, Message) are identical!
                (exc) => exc.HResult == firstExc.HResult && exc.Message == firstExc.Message
            ));

            //****************************************************************************************************
            // Check that New Connection works even with Old in Scope!
            //****************************************************************************************************
            //Create a NEW Connection now that is Valid without the Transaction (but the originals are STILL IN SCOPE...
            await using var sqlConnOkNewConnection = await SqlConnectionHelper.NewConnectionAsync().ConfigureAwait(false);

            var dbSchemaLoaderFromFactoryFuncOk = SqlBulkHelpersSchemaLoaderCache.GetSchemaLoader($"SQL_VALID_CONNECTION_CUSTOM_CACHE_KEY::{Guid.NewGuid()}");

            var validTableDefinition = dbSchemaLoaderFromFactoryFuncInvalid.GetTableSchemaDefinition(
                TestHelpers.TestTableNameFullyQualified,
                TableSchemaDetailLevel.ExtendedDetails,
                sqlConnection: sqlConnOkNewConnection
            );

            //Initial Call should result in SQL Exception due to Pending Transaction...
            Assert.IsNotNull(validTableDefinition);
        }
    }
}
